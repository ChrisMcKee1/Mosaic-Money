using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Data;

namespace MosaicMoney.Api.Domain.Ledger.Ingestion;

public sealed record PlaidDeltaIngestionItemInput(
    string PlaidTransactionId,
    string Description,
    decimal Amount,
    DateOnly TransactionDate,
    string RawPayloadJson,
    bool IsAmbiguous,
    string? ReviewReason);

public sealed record PlaidDeltaIngestionRequest(
    Guid AccountId,
    string DeltaCursor,
    IReadOnlyList<PlaidDeltaIngestionItemInput> Items);

public sealed record PlaidDeltaIngestionItemResult(
    string PlaidTransactionId,
    Guid EnrichedTransactionId,
    bool RawDuplicate,
    IngestionDisposition Disposition,
    TransactionReviewStatus ReviewStatus,
    string? ReviewReason);

public sealed record PlaidDeltaIngestionResult(
    int RawStoredCount,
    int RawDuplicateCount,
    int InsertedCount,
    int UpdatedCount,
    int UnchangedCount,
    IReadOnlyList<PlaidDeltaIngestionItemResult> Items);

public sealed class PlaidDeltaIngestionService(MosaicMoneyDbContext dbContext)
{
    private const string Source = "plaid";
    private const string DefaultCursor = "no-cursor";
    private const string SupportedDeterministicScoreVersion = "mm-be-07a-v1";
    private const string SupportedTieBreakPolicy = "due_date_distance_then_amount_delta_then_latest_observed";

    public async Task<PlaidDeltaIngestionResult> IngestAsync(
        PlaidDeltaIngestionRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedCursor = NormalizeCursor(request.DeltaCursor);
        var now = DateTime.UtcNow;
        var recurringMatchContext = await BuildRecurringMatchContextAsync(request.AccountId, cancellationToken);

        var seenRawKeys = new HashSet<string>(StringComparer.Ordinal);
        var persistedRawKeys = new HashSet<string>(StringComparer.Ordinal);
        var rawRecordsByKey = new Dictionary<string, RawTransactionIngestionRecord>(StringComparer.Ordinal);
        var trackedTransactions = new Dictionary<string, EnrichedTransaction>(StringComparer.Ordinal);

        var itemResults = new List<PlaidDeltaIngestionItemResult>(request.Items.Count);

        var rawStoredCount = 0;
        var rawDuplicateCount = 0;
        var insertedCount = 0;
        var updatedCount = 0;
        var unchangedCount = 0;

        foreach (var item in request.Items)
        {
            var normalizedItem = NormalizeItem(item);
            var payloadHash = ComputeSha256(normalizedItem.RawPayloadJson);
            var rawKey = BuildRawKey(normalizedCursor, normalizedItem.PlaidTransactionId, payloadHash);

            var alreadySeenInRequest = seenRawKeys.Contains(rawKey);
            if (!rawRecordsByKey.TryGetValue(rawKey, out var rawRecord))
            {
                rawRecord = await dbContext.RawTransactionIngestionRecords.FirstOrDefaultAsync(
                    x => x.Source == Source
                        && x.DeltaCursor == normalizedCursor
                        && x.SourceTransactionId == normalizedItem.PlaidTransactionId
                        && x.PayloadHash == payloadHash,
                    cancellationToken);

                if (rawRecord is null)
                {
                    rawRecord = new RawTransactionIngestionRecord
                    {
                        Id = Guid.NewGuid(),
                        Source = Source,
                        DeltaCursor = normalizedCursor,
                        AccountId = request.AccountId,
                        SourceTransactionId = normalizedItem.PlaidTransactionId,
                        PayloadHash = payloadHash,
                        PayloadJson = normalizedItem.RawPayloadJson,
                        FirstSeenAtUtc = now,
                        LastSeenAtUtc = now,
                        LastProcessedAtUtc = now,
                    };

                    dbContext.RawTransactionIngestionRecords.Add(rawRecord);
                    rawStoredCount++;
                }
                else
                {
                    persistedRawKeys.Add(rawKey);
                }

                rawRecordsByKey[rawKey] = rawRecord;
            }

            var rawDuplicate = alreadySeenInRequest || persistedRawKeys.Contains(rawKey);
            if (rawDuplicate)
            {
                rawDuplicateCount++;
            }

            seenRawKeys.Add(rawKey);

            if (!trackedTransactions.TryGetValue(normalizedItem.PlaidTransactionId, out var transaction))
            {
                transaction = await dbContext.EnrichedTransactions.FirstOrDefaultAsync(
                    x => x.PlaidTransactionId == normalizedItem.PlaidTransactionId,
                    cancellationToken);

                if (transaction is not null)
                {
                    trackedTransactions[normalizedItem.PlaidTransactionId] = transaction;
                }
            }

            var disposition = IngestionDisposition.Unchanged;

            if (transaction is null)
            {
                transaction = CreateTransaction(request.AccountId, normalizedItem, now);
                dbContext.EnrichedTransactions.Add(transaction);
                trackedTransactions[normalizedItem.PlaidTransactionId] = transaction;

                insertedCount++;
                disposition = IngestionDisposition.Inserted;
            }
            else
            {
                var changed = ApplySourceUpdate(transaction, request.AccountId, normalizedItem, now);
                if (changed)
                {
                    updatedCount++;
                    disposition = IngestionDisposition.Updated;
                }
                else
                {
                    unchangedCount++;
                }
            }

            var linkedRecurring = TryApplyRecurringMatch(transaction, normalizedItem, recurringMatchContext, now);
            if (linkedRecurring && disposition == IngestionDisposition.Unchanged)
            {
                unchangedCount--;
                updatedCount++;
                disposition = IngestionDisposition.Updated;
            }

            rawRecord.LastSeenAtUtc = now;
            rawRecord.LastProcessedAtUtc = now;
            rawRecord.AccountId = request.AccountId;
            rawRecord.EnrichedTransactionId = transaction.Id;
            rawRecord.LastDisposition = disposition;
            rawRecord.LastReviewReason = transaction.ReviewReason;

            itemResults.Add(new PlaidDeltaIngestionItemResult(
                normalizedItem.PlaidTransactionId,
                transaction.Id,
                rawDuplicate,
                disposition,
                transaction.ReviewStatus,
                transaction.ReviewReason));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new PlaidDeltaIngestionResult(
            rawStoredCount,
            rawDuplicateCount,
            insertedCount,
            updatedCount,
            unchangedCount,
            itemResults);
    }

    private static EnrichedTransaction CreateTransaction(
        Guid accountId,
        PlaidDeltaIngestionItemInput item,
        DateTime now)
    {
        var transaction = new EnrichedTransaction
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            PlaidTransactionId = item.PlaidTransactionId,
            Description = item.Description,
            Amount = decimal.Round(item.Amount, 2),
            TransactionDate = item.TransactionDate,
            ExcludeFromBudget = false,
            IsExtraPrincipal = false,
            CreatedAtUtc = now,
            LastModifiedAtUtc = now,
        };

        if (ShouldRouteToNeedsReview(item))
        {
            transaction.ReviewStatus = TransactionReviewStatus.NeedsReview;
            transaction.ReviewReason = ResolveReviewReason(item);
        }
        else
        {
            transaction.ReviewStatus = TransactionReviewStatus.None;
            transaction.ReviewReason = null;
        }

        return transaction;
    }

    private static bool ApplySourceUpdate(
        EnrichedTransaction transaction,
        Guid accountId,
        PlaidDeltaIngestionItemInput item,
        DateTime now)
    {
        var changed = false;

        if (transaction.AccountId != accountId)
        {
            transaction.AccountId = accountId;
            changed = true;
        }

        if (!string.Equals(transaction.Description, item.Description, StringComparison.Ordinal))
        {
            transaction.Description = item.Description;
            changed = true;
        }

        var roundedAmount = decimal.Round(item.Amount, 2);
        if (transaction.Amount != roundedAmount)
        {
            transaction.Amount = roundedAmount;
            changed = true;
        }

        if (transaction.TransactionDate != item.TransactionDate)
        {
            transaction.TransactionDate = item.TransactionDate;
            changed = true;
        }

        if (ShouldRouteToNeedsReview(item))
        {
            var reason = ResolveReviewReason(item);
            if (transaction.ReviewStatus != TransactionReviewStatus.NeedsReview)
            {
                transaction.ReviewStatus = TransactionReviewStatus.NeedsReview;
                transaction.ReviewReason = reason;
                changed = true;
            }
            else if (string.IsNullOrWhiteSpace(transaction.ReviewReason))
            {
                transaction.ReviewReason = reason;
                changed = true;
            }
        }

        if (changed)
        {
            transaction.LastModifiedAtUtc = now;
        }

        return changed;
    }

    private static bool ShouldRouteToNeedsReview(PlaidDeltaIngestionItemInput item)
    {
        return item.IsAmbiguous
            || string.IsNullOrWhiteSpace(item.Description)
            || item.Amount == 0
            || item.TransactionDate == default;
    }

    private static string ResolveReviewReason(PlaidDeltaIngestionItemInput item)
    {
        if (!string.IsNullOrWhiteSpace(item.ReviewReason))
        {
            return item.ReviewReason.Trim();
        }

        if (item.Amount == 0)
        {
            return "ingestion_amount_zero";
        }

        if (item.TransactionDate == default)
        {
            return "ingestion_missing_transaction_date";
        }

        if (string.IsNullOrWhiteSpace(item.Description))
        {
            return "ingestion_missing_description";
        }

        return "ambiguous_source_payload";
    }

    private static string NormalizeCursor(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? DefaultCursor
            : value.Trim();
    }

    private static PlaidDeltaIngestionItemInput NormalizeItem(PlaidDeltaIngestionItemInput item)
    {
        return item with
        {
            PlaidTransactionId = item.PlaidTransactionId.Trim(),
            Description = item.Description.Trim(),
            ReviewReason = string.IsNullOrWhiteSpace(item.ReviewReason)
                ? null
                : item.ReviewReason.Trim(),
            RawPayloadJson = item.RawPayloadJson,
        };
    }

    private static string ComputeSha256(string content)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string BuildRawKey(string deltaCursor, string plaidTransactionId, string payloadHash)
    {
        return string.Concat(deltaCursor, "|", plaidTransactionId, "|", payloadHash);
    }

    private async Task<RecurringMatchContext> BuildRecurringMatchContextAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var householdId = await dbContext.Accounts
            .AsNoTracking()
            .Where(x => x.Id == accountId)
            .Select(x => (Guid?)x.HouseholdId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!householdId.HasValue)
        {
            return RecurringMatchContext.Empty;
        }

        var recurringItems = await dbContext.RecurringItems
            .Where(x => x.HouseholdId == householdId.Value && x.IsActive)
            .ToListAsync(cancellationToken);

        if (recurringItems.Count == 0)
        {
            return RecurringMatchContext.Empty;
        }

        var recurringIds = recurringItems.Select(x => x.Id).ToList();
        var lastObservedByRecurringId = await dbContext.EnrichedTransactions
            .AsNoTracking()
            .Where(x => x.RecurringItemId.HasValue && recurringIds.Contains(x.RecurringItemId.Value))
            .GroupBy(x => x.RecurringItemId!.Value)
            .Select(group => new
            {
                RecurringItemId = group.Key,
                LastObservedDate = group.Max(x => x.TransactionDate),
            })
            .ToListAsync(cancellationToken);

        return new RecurringMatchContext(
            recurringItems,
            lastObservedByRecurringId.ToDictionary(x => x.RecurringItemId, x => x.LastObservedDate));
    }

    private static bool TryApplyRecurringMatch(
        EnrichedTransaction transaction,
        PlaidDeltaIngestionItemInput item,
        RecurringMatchContext recurringMatchContext,
        DateTime now)
    {
        if (transaction.RecurringItemId.HasValue
            || recurringMatchContext.RecurringItems.Count == 0
            || transaction.ReviewStatus == TransactionReviewStatus.NeedsReview
            || ShouldRouteToNeedsReview(item))
        {
            return false;
        }

        var candidate = SelectRecurringCandidate(item, recurringMatchContext);
        if (candidate is null)
        {
            return false;
        }

        transaction.RecurringItemId = candidate.RecurringItem.Id;
        transaction.LastModifiedAtUtc = now;
        candidate.RecurringItem.NextDueDate = AdvanceRecurringDueDate(candidate.RecurringItem.NextDueDate, candidate.RecurringItem.Frequency);
        recurringMatchContext.LastObservedByRecurringId[candidate.RecurringItem.Id] = item.TransactionDate;

        return true;
    }

    private static RecurringCandidate? SelectRecurringCandidate(
        PlaidDeltaIngestionItemInput item,
        RecurringMatchContext recurringMatchContext)
    {
        var candidates = new List<RecurringCandidate>();

        foreach (var recurringItem in recurringMatchContext.RecurringItems)
        {
            if (!SupportsDeterministicRecurringPolicy(recurringItem))
            {
                continue;
            }

            if (!IsMerchantMatch(item.Description, recurringItem.MerchantName))
            {
                continue;
            }

            var minDate = recurringItem.NextDueDate.AddDays(-recurringItem.DueWindowDaysBefore);
            var maxDate = recurringItem.NextDueDate.AddDays(recurringItem.DueWindowDaysAfter);
            if (item.TransactionDate < minDate || item.TransactionDate > maxDate)
            {
                continue;
            }

            var expectedAmount = decimal.Abs(recurringItem.ExpectedAmount);
            var transactionAmount = decimal.Abs(item.Amount);
            var amountDelta = decimal.Abs(transactionAmount - expectedAmount);
            var allowedAmountVariance = ResolveAllowedAmountVariance(recurringItem, expectedAmount);
            if (amountDelta > allowedAmountVariance)
            {
                continue;
            }

            var dueDateDistanceDays = Math.Abs(item.TransactionDate.DayNumber - recurringItem.NextDueDate.DayNumber);
            var dueDateScore = ResolveDueDateScore(recurringItem, dueDateDistanceDays);
            var amountScore = ResolveAmountScore(amountDelta, allowedAmountVariance);

            recurringMatchContext.LastObservedByRecurringId.TryGetValue(recurringItem.Id, out var lastObservedDate);
            var recencyScore = ResolveRecencyScore(recurringItem, item.TransactionDate, lastObservedDate);

            var weightedScore = decimal.Round(
                (dueDateScore * recurringItem.DueDateScoreWeight)
                + (amountScore * recurringItem.AmountScoreWeight)
                + (recencyScore * recurringItem.RecencyScoreWeight),
                4);

            if (weightedScore < recurringItem.DeterministicMatchThreshold)
            {
                continue;
            }

            candidates.Add(new RecurringCandidate(
                recurringItem,
                weightedScore,
                dueDateDistanceDays,
                amountDelta,
                lastObservedDate));
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates.Count == 1
            ? candidates[0]
            : null;
    }

    private static bool SupportsDeterministicRecurringPolicy(RecurringItem recurringItem)
    {
        var scoreVersion = recurringItem.DeterministicScoreVersion.Trim();
        var tieBreakPolicy = recurringItem.TieBreakPolicy.Trim();

        return scoreVersion.Equals(SupportedDeterministicScoreVersion, StringComparison.OrdinalIgnoreCase)
            && tieBreakPolicy.Equals(SupportedTieBreakPolicy, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMerchantMatch(string description, string merchantName)
    {
        var normalizedDescription = NormalizeMerchantToken(description);
        var normalizedMerchant = NormalizeMerchantToken(merchantName);
        if (normalizedDescription.Length == 0 || normalizedMerchant.Length < 3)
        {
            return false;
        }

        return normalizedDescription.Contains(normalizedMerchant, StringComparison.Ordinal)
            || normalizedMerchant.Contains(normalizedDescription, StringComparison.Ordinal);
    }

    private static string NormalizeMerchantToken(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    private static decimal ResolveAllowedAmountVariance(RecurringItem recurringItem, decimal expectedAmount)
    {
        var percentVariance = expectedAmount * (recurringItem.AmountVariancePercent / 100m);
        return decimal.Max(percentVariance, recurringItem.AmountVarianceAbsolute);
    }

    private static decimal ResolveDueDateScore(RecurringItem recurringItem, int dueDateDistanceDays)
    {
        var maxWindow = Math.Max(1, Math.Max(recurringItem.DueWindowDaysBefore, recurringItem.DueWindowDaysAfter));
        var normalizedDistance = decimal.Min(1m, dueDateDistanceDays / (decimal)maxWindow);
        return 1m - normalizedDistance;
    }

    private static decimal ResolveAmountScore(decimal amountDelta, decimal allowedAmountVariance)
    {
        if (allowedAmountVariance == 0)
        {
            return amountDelta == 0 ? 1m : 0m;
        }

        var normalizedDelta = decimal.Min(1m, amountDelta / allowedAmountVariance);
        return 1m - normalizedDelta;
    }

    private static decimal ResolveRecencyScore(
        RecurringItem recurringItem,
        DateOnly transactionDate,
        DateOnly? lastObservedDate)
    {
        if (!lastObservedDate.HasValue)
        {
            return 1m;
        }

        var expectedNextDate = AdvanceRecurringDueDate(lastObservedDate.Value, recurringItem.Frequency);
        var expectedDistance = Math.Max(1, Math.Abs(expectedNextDate.DayNumber - lastObservedDate.Value.DayNumber));
        var observedDistance = Math.Abs(transactionDate.DayNumber - expectedNextDate.DayNumber);

        var normalizedDistance = decimal.Min(1m, observedDistance / (decimal)expectedDistance);
        return 1m - normalizedDistance;
    }

    private static DateOnly AdvanceRecurringDueDate(DateOnly dueDate, RecurringFrequency frequency)
    {
        return frequency switch
        {
            RecurringFrequency.Weekly => dueDate.AddDays(7),
            RecurringFrequency.BiWeekly => dueDate.AddDays(14),
            RecurringFrequency.Monthly => dueDate.AddMonths(1),
            RecurringFrequency.Quarterly => dueDate.AddMonths(3),
            RecurringFrequency.Annually => dueDate.AddYears(1),
            _ => dueDate,
        };
    }

    private sealed record RecurringCandidate(
        RecurringItem RecurringItem,
        decimal Score,
        int DueDateDistanceDays,
        decimal AmountDelta,
        DateOnly? LastObservedDate);

    private sealed class RecurringMatchContext(
        IReadOnlyList<RecurringItem> recurringItems,
        Dictionary<Guid, DateOnly> lastObservedByRecurringId)
    {
        public static readonly RecurringMatchContext Empty = new(
            [],
            new Dictionary<Guid, DateOnly>());

        public IReadOnlyList<RecurringItem> RecurringItems { get; } = recurringItems;

        public Dictionary<Guid, DateOnly> LastObservedByRecurringId { get; } = lastObservedByRecurringId;
    }
}
