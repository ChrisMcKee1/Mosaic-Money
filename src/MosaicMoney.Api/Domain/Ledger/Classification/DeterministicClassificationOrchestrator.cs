using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Data;

namespace MosaicMoney.Api.Domain.Ledger.Classification;

public sealed record DeterministicClassificationExecutionResult(
    TransactionClassificationOutcome Outcome,
    TransactionReviewStatus TransactionReviewStatus,
    string? TransactionReviewReason,
    Guid? TransactionSubcategoryId);

public interface IDeterministicClassificationOrchestrator
{
    Task<DeterministicClassificationExecutionResult?> ClassifyAndPersistAsync(
        Guid transactionId,
    Guid? needsReviewByUserId = null,
        CancellationToken cancellationToken = default);
}

public sealed class DeterministicClassificationOrchestrator(
    MosaicMoneyDbContext dbContext,
    IDeterministicClassificationEngine deterministicClassificationEngine,
    IClassificationAmbiguityPolicyGate ambiguityPolicyGate) : IDeterministicClassificationOrchestrator
{
    public async Task<DeterministicClassificationExecutionResult?> ClassifyAndPersistAsync(
        Guid transactionId,
        Guid? needsReviewByUserId = null,
        CancellationToken cancellationToken = default)
    {
        var transaction = await dbContext.EnrichedTransactions
            .FirstOrDefaultAsync(x => x.Id == transactionId, cancellationToken);

        if (transaction is null)
        {
            return null;
        }

        var subcategories = await dbContext.Subcategories
            .AsNoTracking()
            .Select(x => new DeterministicClassificationSubcategory(x.Id, x.Name))
            .ToListAsync(cancellationToken);

        var deterministicRequest = new DeterministicClassificationRequest(
            transaction.Id,
            transaction.Description,
            transaction.Amount,
            transaction.TransactionDate,
            subcategories);

        var stageResult = deterministicClassificationEngine.Execute(deterministicRequest);
        var gateDecision = ambiguityPolicyGate.Evaluate(transaction.ReviewStatus, stageResult);
        var now = DateTime.UtcNow;

        var outcome = new TransactionClassificationOutcome
        {
            Id = Guid.NewGuid(),
            TransactionId = transaction.Id,
            ProposedSubcategoryId = stageResult.ProposedSubcategoryId,
            FinalConfidence = RoundConfidence(gateDecision.FinalConfidence),
            Decision = gateDecision.Decision,
            ReviewStatus = gateDecision.ReviewStatus,
            DecisionReasonCode = Truncate(gateDecision.DecisionReasonCode.Trim(), 120),
            DecisionRationale = Truncate(gateDecision.DecisionRationale.Trim(), 500),
            AgentNoteSummary = string.IsNullOrWhiteSpace(gateDecision.AgentNoteSummary)
                ? null
                : Truncate(gateDecision.AgentNoteSummary.Trim(), 600),
            CreatedAtUtc = now,
        };

        outcome.StageOutputs.Add(new ClassificationStageOutput
        {
            Id = Guid.NewGuid(),
            Stage = ClassificationStage.Deterministic,
            StageOrder = 1,
            ProposedSubcategoryId = stageResult.ProposedSubcategoryId,
            Confidence = RoundConfidence(stageResult.Confidence),
            RationaleCode = Truncate(stageResult.RationaleCode.Trim(), 120),
            Rationale = Truncate(stageResult.Rationale.Trim(), 500),
            EscalatedToNextStage = false,
            ProducedAtUtc = now,
        });

        ApplyDecisionToTransaction(transaction, stageResult, gateDecision, now, needsReviewByUserId);

        dbContext.TransactionClassificationOutcomes.Add(outcome);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new DeterministicClassificationExecutionResult(
            outcome,
            transaction.ReviewStatus,
            transaction.ReviewReason,
            transaction.SubcategoryId);
    }

    private static void ApplyDecisionToTransaction(
        EnrichedTransaction transaction,
        DeterministicClassificationStageResult stageResult,
        ClassificationAmbiguityDecision gateDecision,
        DateTime now,
        Guid? needsReviewByUserId)
    {
        transaction.ReviewStatus = gateDecision.ReviewStatus;

        if (gateDecision.ReviewStatus == TransactionReviewStatus.NeedsReview)
        {
            transaction.ReviewReason = gateDecision.DecisionReasonCode;

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

        if (gateDecision.Decision == ClassificationDecision.Categorized && stageResult.ProposedSubcategoryId.HasValue)
        {
            transaction.SubcategoryId = stageResult.ProposedSubcategoryId.Value;
        }

        transaction.LastModifiedAtUtc = now;
    }

    private static decimal RoundConfidence(decimal value)
    {
        return decimal.Round(value, 4, MidpointRounding.AwayFromZero);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }
}
