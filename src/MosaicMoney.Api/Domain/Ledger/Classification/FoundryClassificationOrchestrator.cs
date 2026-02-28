using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger.Taxonomy;

namespace MosaicMoney.Api.Domain.Ledger.Classification;

public interface IFoundryClassificationOrchestrator
{
    Task<DeterministicClassificationExecutionResult?> ClassifyAndPersistAsync(
        Guid transactionId,
        Guid? needsReviewByUserId = null,
        CancellationToken cancellationToken = default);
}

public sealed class FoundryClassificationOrchestrator(
    MosaicMoneyDbContext dbContext,
    IFoundryClassificationService foundryClassificationService,
    ITaxonomyReadinessGate taxonomyReadinessGate,
    IClassificationInsightWriter insightWriter,
    ILogger<FoundryClassificationOrchestrator> logger) : IFoundryClassificationOrchestrator
{
    public async Task<DeterministicClassificationExecutionResult?> ClassifyAndPersistAsync(
        Guid transactionId,
        Guid? needsReviewByUserId = null,
        CancellationToken cancellationToken = default)
    {
        var transaction = await dbContext.EnrichedTransactions
            .Include(x => x.Account)
            .FirstOrDefaultAsync(x => x.Id == transactionId, cancellationToken);

        if (transaction is null)
        {
            return null;
        }

        var readinessEvaluation = await taxonomyReadinessGate.EvaluateAsync(
            transaction.Account.HouseholdId,
            TaxonomyReadinessLane.Classification,
            needsReviewByUserId,
            cancellationToken);

        var now = DateTime.UtcNow;
        var subcategories = await QueryAvailableSubcategoriesAsync(transaction, needsReviewByUserId, cancellationToken);

        FoundryClassificationDecision? foundryDecision = null;
        if (readinessEvaluation.IsReady && subcategories.Count > 0)
        {
            var scopeSummary = needsReviewByUserId.HasValue
                ? "Platform + HouseholdShared + caller User scope"
                : "Platform + HouseholdShared scope";

            var plaidContext = await QueryPlaidContextAsync(transaction.Id, cancellationToken);

            foundryDecision = await foundryClassificationService.ClassifyAsync(
                new FoundryClassificationInput(
                    transaction.Id,
                    transaction.Description,
                    transaction.Amount,
                    transaction.TransactionDate,
                    subcategories,
                    scopeSummary,
                    plaidContext),
                cancellationToken);
        }

        var fallbackDecision = BuildFallbackDecision(readinessEvaluation, foundryDecision);

        var outcome = new TransactionClassificationOutcome
        {
            Id = Guid.NewGuid(),
            TransactionId = transaction.Id,
            ProposedSubcategoryId = fallbackDecision.ProposedSubcategoryId,
            FinalConfidence = decimal.Round(fallbackDecision.Confidence, 4, MidpointRounding.AwayFromZero),
            Decision = fallbackDecision.Decision,
            ReviewStatus = fallbackDecision.ReviewStatus,
            DecisionReasonCode = Truncate(fallbackDecision.ReasonCode, 120),
            DecisionRationale = Truncate(fallbackDecision.Rationale, 500),
            AgentNoteSummary = AgentNoteSummaryPolicy.Sanitize(fallbackDecision.AgentNoteSummary),
            IsAiAssigned = true,
            AssignmentSource = Truncate(fallbackDecision.AssignmentSource, 40),
            AssignedByAgent = string.IsNullOrWhiteSpace(fallbackDecision.AssignmentAgent)
                ? null
                : Truncate(fallbackDecision.AssignmentAgent, 120),
            CreatedAtUtc = now,
        };

        outcome.StageOutputs.Add(new ClassificationStageOutput
        {
            Id = Guid.NewGuid(),
            Stage = ClassificationStage.Deterministic,
            StageOrder = 1,
            ProposedSubcategoryId = fallbackDecision.ProposedSubcategoryId,
            Confidence = decimal.Round(fallbackDecision.Confidence, 4, MidpointRounding.AwayFromZero),
            RationaleCode = Truncate(fallbackDecision.ReasonCode, 120),
            Rationale = Truncate(fallbackDecision.Rationale, 500),
            EscalatedToNextStage = false,
            ProducedAtUtc = now,
        });

        ApplyDecisionToTransaction(transaction, fallbackDecision, needsReviewByUserId, now);
        insightWriter.RecordOutcomeInsight(transaction, outcome, fallbackDecision.Rationale);

        dbContext.TransactionClassificationOutcomes.Add(outcome);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Foundry classification persisted for transaction {TransactionId}. Decision={Decision}, ReviewStatus={ReviewStatus}, ProposedSubcategory={SubcategoryId}",
            transaction.Id,
            outcome.Decision,
            outcome.ReviewStatus,
            outcome.ProposedSubcategoryId);

        return new DeterministicClassificationExecutionResult(
            outcome,
            transaction.ReviewStatus,
            transaction.ReviewReason,
            transaction.SubcategoryId);
    }

    private async Task<IReadOnlyList<DeterministicClassificationSubcategory>> QueryAvailableSubcategoriesAsync(
        EnrichedTransaction transaction,
        Guid? needsReviewByUserId,
        CancellationToken cancellationToken)
    {
        var subcategoryQuery = dbContext.Subcategories
            .AsNoTracking()
            .Where(x =>
                !x.IsArchived
                && !x.Category.IsArchived
                && (
                    x.Category.OwnerType == CategoryOwnerType.Platform
                    || (x.Category.OwnerType == CategoryOwnerType.HouseholdShared
                        && x.Category.HouseholdId == transaction.Account.HouseholdId)));

        if (needsReviewByUserId.HasValue)
        {
            var ownerUserId = needsReviewByUserId.Value;
            subcategoryQuery = subcategoryQuery.Where(x =>
                x.Category.OwnerType != CategoryOwnerType.User
                || (x.Category.HouseholdId == transaction.Account.HouseholdId
                    && x.Category.OwnerUserId == ownerUserId));
        }
        else
        {
            subcategoryQuery = subcategoryQuery.Where(x => x.Category.OwnerType != CategoryOwnerType.User);
        }

        return await subcategoryQuery
            .OrderBy(x => x.Category.DisplayOrder)
            .ThenBy(x => x.DisplayOrder)
            .Select(x => new DeterministicClassificationSubcategory(x.Id, x.Name))
            .ToListAsync(cancellationToken);
    }

    private async Task<FoundryPlaidContext?> QueryPlaidContextAsync(
        Guid transactionId,
        CancellationToken cancellationToken)
    {
        var payloadJson = await dbContext.RawTransactionIngestionRecords
            .AsNoTracking()
            .Where(x => x.EnrichedTransactionId == transactionId)
            .OrderByDescending(x => x.LastProcessedAtUtc)
            .ThenByDescending(x => x.LastSeenAtUtc)
            .ThenByDescending(x => x.Id)
            .Select(x => x.PayloadJson)
            .FirstOrDefaultAsync(cancellationToken);

        return TryBuildPlaidContext(payloadJson);
    }

    internal static FoundryPlaidContext? TryBuildPlaidContext(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(payloadJson);
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
            var root = document.RootElement;

            var merchantName = SanitizeContextValue(TryReadString(root, "merchant_name", "merchantName"), 160);
            var paymentChannel = SanitizeContextValue(TryReadString(root, "payment_channel", "paymentChannel"), 64);

            var categoryPrimary = (string?)null;
            var categoryDetailed = (string?)null;
            if (root.TryGetProperty("personal_finance_category", out var personalFinanceCategory)
                && personalFinanceCategory.ValueKind == JsonValueKind.Object)
            {
                categoryPrimary = SanitizeContextValue(TryReadString(personalFinanceCategory, "primary"), 120);
                categoryDetailed = SanitizeContextValue(TryReadString(personalFinanceCategory, "detailed"), 120);
            }

            categoryPrimary ??= SanitizeContextValue(TryReadArrayStringValue(root, "category", 0), 120);
            categoryDetailed ??= SanitizeContextValue(TryReadArrayStringValue(root, "category", 1), 120);

            var counterpartyName = (string?)null;
            var counterpartyType = (string?)null;
            if (root.TryGetProperty("counterparties", out var counterparties)
                && counterparties.ValueKind == JsonValueKind.Array)
            {
                foreach (var counterparty in counterparties.EnumerateArray())
                {
                    if (counterparty.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    counterpartyName = SanitizeContextValue(TryReadString(counterparty, "name"), 160);
                    counterpartyType = SanitizeContextValue(TryReadString(counterparty, "type"), 80);

                    if (!string.IsNullOrWhiteSpace(counterpartyName)
                        || !string.IsNullOrWhiteSpace(counterpartyType))
                    {
                        break;
                    }
                }
            }

            var hasContext = !string.IsNullOrWhiteSpace(merchantName)
                || !string.IsNullOrWhiteSpace(paymentChannel)
                || !string.IsNullOrWhiteSpace(categoryPrimary)
                || !string.IsNullOrWhiteSpace(categoryDetailed)
                || !string.IsNullOrWhiteSpace(counterpartyName)
                || !string.IsNullOrWhiteSpace(counterpartyType);

            return hasContext
                ? new FoundryPlaidContext(
                    merchantName,
                    paymentChannel,
                    categoryPrimary,
                    categoryDetailed,
                    counterpartyName,
                    counterpartyType)
                : null;
        }
    }

    private static FoundryClassificationDecision BuildFallbackDecision(
        TaxonomyReadinessEvaluation readinessEvaluation,
        FoundryClassificationDecision? foundryDecision)
    {
        if (!readinessEvaluation.IsReady)
        {
            var rationale = $"Taxonomy readiness gate blocked Foundry classification lane: {readinessEvaluation.Rationale}";
            return new FoundryClassificationDecision(
                ClassificationDecision.NeedsReview,
                TransactionReviewStatus.NeedsReview,
                ProposedSubcategoryId: null,
                Confidence: 0m,
                ReasonCode: readinessEvaluation.ReasonCode,
                Rationale: rationale,
                AgentNoteSummary: $"Taxonomy readiness gate routed to NeedsReview ({readinessEvaluation.ReasonCode}).",
                AssignmentSource: "taxonomy_gate",
                AssignmentAgent: null,
                RawOutputText: rationale);
        }

        if (foundryDecision is not null)
        {
            return foundryDecision;
        }

        return new FoundryClassificationDecision(
            ClassificationDecision.NeedsReview,
            TransactionReviewStatus.NeedsReview,
            ProposedSubcategoryId: null,
            Confidence: 0m,
            ReasonCode: "foundry_classification_unavailable",
            Rationale: "Foundry Responses API classification was unavailable; routed to NeedsReview.",
            AgentNoteSummary: "Foundry classification unavailable; human review required.",
            AssignmentSource: "foundry_unavailable",
            AssignmentAgent: null,
            RawOutputText: string.Empty);
    }

    private static void ApplyDecisionToTransaction(
        EnrichedTransaction transaction,
        FoundryClassificationDecision decision,
        Guid? needsReviewByUserId,
        DateTime now)
    {
        transaction.ReviewStatus = decision.ReviewStatus;

        if (decision.ReviewStatus == TransactionReviewStatus.NeedsReview)
        {
            transaction.ReviewReason = decision.ReasonCode;

            if (needsReviewByUserId.HasValue)
            {
                transaction.NeedsReviewByUserId = needsReviewByUserId.Value;
            }
        }
        else
        {
            transaction.ReviewReason = null;
            transaction.NeedsReviewByUserId = null;
        }

        if (decision.Decision == ClassificationDecision.Categorized && decision.ProposedSubcategoryId.HasValue)
        {
            transaction.SubcategoryId = decision.ProposedSubcategoryId;
        }

        transaction.LastModifiedAtUtc = now;
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }

    private static string? TryReadString(JsonElement root, params string[] propertyNames)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var propertyName in propertyNames)
        {
            if (!root.TryGetProperty(propertyName, out var property)
                || property.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            return property.GetString();
        }

        return null;
    }

    private static string? TryReadArrayStringValue(JsonElement root, string propertyName, int index)
    {
        if (!root.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Array
            || index < 0
            || property.GetArrayLength() <= index)
        {
            return null;
        }

        var value = property[index];
        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string? SanitizeContextValue(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();

        if (normalized.Length == 0)
        {
            return null;
        }

        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }
}
