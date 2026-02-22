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

    public async Task<PlaidDeltaIngestionResult> IngestAsync(
        PlaidDeltaIngestionRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedCursor = NormalizeCursor(request.DeltaCursor);
        var now = DateTime.UtcNow;

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
}
