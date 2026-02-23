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
    IClassificationAmbiguityPolicyGate ambiguityPolicyGate,
    IClassificationConfidenceFusionPolicy confidenceFusionPolicy,
    IPostgresSemanticRetrievalService semanticRetrievalService,
    IMafFallbackEligibilityGate mafFallbackEligibilityGate,
    IMafFallbackGraphService mafFallbackGraphService) : IDeterministicClassificationOrchestrator
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
        var shouldAttemptSemanticStage = gateDecision.Decision == ClassificationDecision.NeedsReview;
        var semanticResult = shouldAttemptSemanticStage
            ? await semanticRetrievalService.RetrieveCandidatesAsync(transaction.Id, cancellationToken: cancellationToken)
            : null;
        var fusionDecision = confidenceFusionPolicy.Evaluate(
            transaction.ReviewStatus,
            stageResult,
            gateDecision,
            semanticResult);
        var mafEligibilityDecision = mafFallbackEligibilityGate.Evaluate(
            gateDecision,
            fusionDecision,
            shouldAttemptSemanticStage);

        MafFallbackGraphResult? mafResult = null;
        if (mafEligibilityDecision.IsEligible)
        {
            var mafRequest = new MafFallbackGraphRequest(
                transaction.Id,
                transaction.Description,
                transaction.Amount,
                transaction.TransactionDate,
                subcategories,
                stageResult,
                semanticResult,
                fusionDecision);

            mafResult = await mafFallbackGraphService.ExecuteAsync(mafRequest, cancellationToken);
        }

        var outcome = new TransactionClassificationOutcome
        {
            Id = Guid.NewGuid(),
            TransactionId = transaction.Id,
            ProposedSubcategoryId = fusionDecision.ProposedSubcategoryId,
            FinalConfidence = RoundConfidence(fusionDecision.FinalConfidence),
            Decision = fusionDecision.Decision,
            ReviewStatus = fusionDecision.ReviewStatus,
            DecisionReasonCode = Truncate(fusionDecision.DecisionReasonCode.Trim(), 120),
            DecisionRationale = Truncate(fusionDecision.DecisionRationale.Trim(), 500),
            AgentNoteSummary = string.IsNullOrWhiteSpace(fusionDecision.AgentNoteSummary)
                ? null
                : Truncate(fusionDecision.AgentNoteSummary.Trim(), 600),
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
            EscalatedToNextStage = shouldAttemptSemanticStage,
            ProducedAtUtc = now,
        });

        if (shouldAttemptSemanticStage)
        {
            outcome.StageOutputs.Add(BuildSemanticStageOutput(
                semanticResult,
                now,
                fusionDecision.EscalatedToNextStage));
        }

        if (mafEligibilityDecision.IsEligible)
        {
            outcome.StageOutputs.Add(BuildMafFallbackStageOutput(mafResult, now));

            var topProposal = mafResult?.Proposals.FirstOrDefault();
            if (topProposal is not null
                && outcome.Decision == ClassificationDecision.NeedsReview
                && !outcome.ProposedSubcategoryId.HasValue)
            {
                // Keep review routing fail-closed while persisting a bounded fallback proposal for human review.
                outcome.ProposedSubcategoryId = topProposal.ProposedSubcategoryId;
            }

            if (topProposal is not null
                && string.IsNullOrWhiteSpace(outcome.AgentNoteSummary)
                && !string.IsNullOrWhiteSpace(topProposal.AgentNoteSummary))
            {
                outcome.AgentNoteSummary = Truncate(topProposal.AgentNoteSummary.Trim(), 600);
            }
        }

        ApplyDecisionToTransaction(transaction, fusionDecision, now, needsReviewByUserId);

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
        ClassificationConfidenceFusionDecision fusionDecision,
        DateTime now,
        Guid? needsReviewByUserId)
    {
        transaction.ReviewStatus = fusionDecision.ReviewStatus;

        if (fusionDecision.ReviewStatus == TransactionReviewStatus.NeedsReview)
        {
            transaction.ReviewReason = fusionDecision.DecisionReasonCode;

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

        if (fusionDecision.Decision == ClassificationDecision.Categorized && fusionDecision.ProposedSubcategoryId.HasValue)
        {
            transaction.SubcategoryId = fusionDecision.ProposedSubcategoryId.Value;
        }

        transaction.LastModifiedAtUtc = now;
    }

    private static decimal RoundConfidence(decimal value)
    {
        return decimal.Round(value, 4, MidpointRounding.AwayFromZero);
    }

    private static ClassificationStageOutput BuildSemanticStageOutput(
        SemanticRetrievalResult? semanticResult,
        DateTime producedAtUtc,
        bool escalatedToNextStage)
    {
        if (semanticResult is null)
        {
            return new ClassificationStageOutput
            {
                Id = Guid.NewGuid(),
                Stage = ClassificationStage.Semantic,
                StageOrder = 2,
                ProposedSubcategoryId = null,
                Confidence = 0m,
                RationaleCode = SemanticRetrievalStatusCodes.QueryFailed,
                Rationale = "Semantic retrieval did not execute.",
                EscalatedToNextStage = escalatedToNextStage,
                ProducedAtUtc = producedAtUtc,
            };
        }

        if (!semanticResult.Succeeded)
        {
            return new ClassificationStageOutput
            {
                Id = Guid.NewGuid(),
                Stage = ClassificationStage.Semantic,
                StageOrder = 2,
                ProposedSubcategoryId = null,
                Confidence = 0m,
                RationaleCode = Truncate(semanticResult.StatusCode, 120),
                Rationale = Truncate(semanticResult.StatusMessage, 500),
                EscalatedToNextStage = escalatedToNextStage,
                ProducedAtUtc = producedAtUtc,
            };
        }

        var top = semanticResult.Candidates.FirstOrDefault();
        var rationale = top is null
            ? semanticResult.StatusMessage
            : $"Top semantic candidate score {top.NormalizedScore:F4} from source transaction {top.SourceTransactionId:D}.";

        return new ClassificationStageOutput
        {
            Id = Guid.NewGuid(),
            Stage = ClassificationStage.Semantic,
            StageOrder = 2,
            ProposedSubcategoryId = top?.ProposedSubcategoryId,
            Confidence = RoundConfidence(top?.NormalizedScore ?? 0m),
            RationaleCode = Truncate(semanticResult.StatusCode, 120),
            Rationale = Truncate(rationale, 500),
            EscalatedToNextStage = escalatedToNextStage,
            ProducedAtUtc = producedAtUtc,
        };
    }

    private static ClassificationStageOutput BuildMafFallbackStageOutput(
        MafFallbackGraphResult? mafResult,
        DateTime producedAtUtc)
    {
        if (mafResult is null)
        {
            return new ClassificationStageOutput
            {
                Id = Guid.NewGuid(),
                Stage = ClassificationStage.MafFallback,
                StageOrder = 3,
                ProposedSubcategoryId = null,
                Confidence = 0m,
                RationaleCode = MafFallbackGraphStatusCodes.ExecutionFailed,
                Rationale = "MAF fallback did not execute.",
                EscalatedToNextStage = false,
                ProducedAtUtc = producedAtUtc,
            };
        }

        if (!mafResult.Succeeded || mafResult.Proposals.Count == 0)
        {
            return new ClassificationStageOutput
            {
                Id = Guid.NewGuid(),
                Stage = ClassificationStage.MafFallback,
                StageOrder = 3,
                ProposedSubcategoryId = null,
                Confidence = 0m,
                RationaleCode = Truncate(mafResult.StatusCode, 120),
                Rationale = Truncate(mafResult.StatusMessage, 500),
                EscalatedToNextStage = false,
                ProducedAtUtc = producedAtUtc,
            };
        }

        var topProposal = mafResult.Proposals[0];
        return new ClassificationStageOutput
        {
            Id = Guid.NewGuid(),
            Stage = ClassificationStage.MafFallback,
            StageOrder = 3,
            ProposedSubcategoryId = topProposal.ProposedSubcategoryId,
            Confidence = RoundConfidence(topProposal.Confidence),
            RationaleCode = Truncate(topProposal.RationaleCode, 120),
            Rationale = Truncate(topProposal.Rationale, 500),
            EscalatedToNextStage = false,
            ProducedAtUtc = producedAtUtc,
        };
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }
}
