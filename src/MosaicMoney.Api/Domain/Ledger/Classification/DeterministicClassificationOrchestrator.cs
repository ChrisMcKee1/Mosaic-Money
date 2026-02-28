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
    IClassificationSpecialistRoutingPolicy specialistRoutingPolicy,
    IPostgresSemanticRetrievalService semanticRetrievalService,
    IMafFallbackEligibilityGate mafFallbackEligibilityGate,
    IMafFallbackGraphService mafFallbackGraphService) : IDeterministicClassificationOrchestrator
{
    private sealed record ClassificationWorkflowFinalDecision(
        ClassificationDecision Decision,
        TransactionReviewStatus ReviewStatus,
        Guid? ProposedSubcategoryId,
        decimal FinalConfidence,
        string DecisionReasonCode,
        string DecisionRationale,
        string? AgentNoteSummary);

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

        var subcategoryQuery = dbContext.Subcategories
            .AsNoTracking()
            .Where(x =>
                x.Category.OwnerType == CategoryOwnerType.Platform
                || (x.Category.OwnerType == CategoryOwnerType.HouseholdShared
                    && x.Category.HouseholdId == transaction.Account.HouseholdId));

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

        var subcategories = await subcategoryQuery
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
        var routingDecision = specialistRoutingPolicy.Evaluate(new ClassificationSpecialistRoutingInput(
            transaction.Description,
            transaction.Amount,
            transaction.ReviewStatus,
            stageResult,
            gateDecision));
        var now = DateTime.UtcNow;
        var shouldAttemptSemanticStage = gateDecision.Decision == ClassificationDecision.NeedsReview
            && routingDecision.AllowSemanticStage;
        var semanticResult = shouldAttemptSemanticStage
            ? await semanticRetrievalService.RetrieveCandidatesAsync(transaction.Id, cancellationToken: cancellationToken)
            : null;
        var rawFusionDecision = confidenceFusionPolicy.Evaluate(
            transaction.ReviewStatus,
            stageResult,
            gateDecision,
            semanticResult);
        var fusionDecision = ApplyRoutingPolicyDecision(transaction.ReviewStatus, rawFusionDecision, routingDecision);
        var mafEligibilityDecision = mafFallbackEligibilityGate.Evaluate(
            gateDecision,
            fusionDecision,
            shouldAttemptSemanticStage);

        if (!routingDecision.AllowMafFallbackStage)
        {
            mafEligibilityDecision = new MafFallbackEligibilityDecision(
                IsEligible: false,
                MafFallbackEligibilityReasonCodes.IneligibleSpecialistRoutingPolicy,
                "Specialist routing policy disabled MAF fallback for the selected lane.");
        }

        var mafResult = await ExecuteMafFallbackIfEligibleAsync(
            transaction,
            subcategories,
            stageResult,
            semanticResult,
            fusionDecision,
            mafEligibilityDecision,
            cancellationToken);

        var deterministicStageOutput = BuildDeterministicStageOutput(
            stageResult,
            gateDecision,
            routingDecision,
            shouldAttemptSemanticStage,
            now);

        var semanticStageOutput = shouldAttemptSemanticStage
            ? BuildSemanticStageOutput(
                semanticResult,
                fusionDecision,
                now,
                fusionDecision.EscalatedToNextStage)
            : null;

        var mafStageOutput = mafEligibilityDecision.IsEligible
            ? BuildMafFallbackStageOutput(mafResult, now)
            : null;

        var finalDecision = ResolveFinalDecision(
            fusionDecision,
            mafResult,
            mafStageOutput);

        var outcome = new TransactionClassificationOutcome
        {
            Id = Guid.NewGuid(),
            TransactionId = transaction.Id,
            ProposedSubcategoryId = finalDecision.ProposedSubcategoryId,
            FinalConfidence = RoundConfidence(finalDecision.FinalConfidence),
            Decision = finalDecision.Decision,
            ReviewStatus = finalDecision.ReviewStatus,
            DecisionReasonCode = Truncate(finalDecision.DecisionReasonCode.Trim(), 120),
            DecisionRationale = Truncate(finalDecision.DecisionRationale.Trim(), 500),
            AgentNoteSummary = AgentNoteSummaryPolicy.Sanitize(finalDecision.AgentNoteSummary),
            CreatedAtUtc = now,
        };

        foreach (var stageOutput in BuildStageOutputs(deterministicStageOutput, semanticStageOutput, mafStageOutput))
        {
            outcome.StageOutputs.Add(stageOutput);
        }

        ApplyDecisionToTransaction(transaction, finalDecision, now, needsReviewByUserId);

        dbContext.TransactionClassificationOutcomes.Add(outcome);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new DeterministicClassificationExecutionResult(
            outcome,
            transaction.ReviewStatus,
            transaction.ReviewReason,
            transaction.SubcategoryId);
    }

    private async Task<MafFallbackGraphResult?> ExecuteMafFallbackIfEligibleAsync(
        EnrichedTransaction transaction,
        IReadOnlyList<DeterministicClassificationSubcategory> subcategories,
        DeterministicClassificationStageResult stageResult,
        SemanticRetrievalResult? semanticResult,
        ClassificationConfidenceFusionDecision fusionDecision,
        MafFallbackEligibilityDecision mafEligibilityDecision,
        CancellationToken cancellationToken)
    {
        if (!mafEligibilityDecision.IsEligible)
        {
            return null;
        }

        var mafRequest = new MafFallbackGraphRequest(
            transaction.Id,
            transaction.Description,
            transaction.Amount,
            transaction.TransactionDate,
            subcategories,
            stageResult,
            semanticResult,
            fusionDecision);

        return await mafFallbackGraphService.ExecuteAsync(mafRequest, cancellationToken);
    }

    private static IReadOnlyList<ClassificationStageOutput> BuildStageOutputs(
        ClassificationStageOutput deterministicStageOutput,
        ClassificationStageOutput? semanticStageOutput,
        ClassificationStageOutput? mafStageOutput)
    {
        var outputs = new List<ClassificationStageOutput>(capacity: 3)
        {
            deterministicStageOutput,
        };

        if (semanticStageOutput is not null)
        {
            outputs.Add(semanticStageOutput);
        }

        if (mafStageOutput is not null)
        {
            outputs.Add(mafStageOutput);
        }

        return outputs;
    }

    private static void ApplyDecisionToTransaction(
        EnrichedTransaction transaction,
        ClassificationWorkflowFinalDecision finalDecision,
        DateTime now,
        Guid? needsReviewByUserId)
    {
        transaction.ReviewStatus = finalDecision.ReviewStatus;

        if (finalDecision.ReviewStatus == TransactionReviewStatus.NeedsReview)
        {
            transaction.ReviewReason = finalDecision.DecisionReasonCode;

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

        if (finalDecision.Decision == ClassificationDecision.Categorized && finalDecision.ProposedSubcategoryId.HasValue)
        {
            transaction.SubcategoryId = finalDecision.ProposedSubcategoryId.Value;
        }

        transaction.LastModifiedAtUtc = now;
    }

    private static decimal RoundConfidence(decimal value)
    {
        return decimal.Round(value, 4, MidpointRounding.AwayFromZero);
    }

    private static ClassificationStageOutput BuildSemanticStageOutput(
        SemanticRetrievalResult? semanticResult,
        ClassificationConfidenceFusionDecision fusionDecision,
        DateTime producedAtUtc,
        bool escalatedToNextStage)
    {
        if (semanticResult is null)
        {
            var semanticUnavailableRationale = BuildSemanticDecisionRationale(
                "Semantic retrieval did not execute.",
                fusionDecision);

            return new ClassificationStageOutput
            {
                Id = Guid.NewGuid(),
                Stage = ClassificationStage.Semantic,
                StageOrder = 2,
                ProposedSubcategoryId = null,
                Confidence = 0m,
                RationaleCode = SemanticRetrievalStatusCodes.QueryFailed,
                Rationale = semanticUnavailableRationale,
                EscalatedToNextStage = escalatedToNextStage,
                ProducedAtUtc = producedAtUtc,
            };
        }

        if (!semanticResult.Succeeded)
        {
            var semanticFailureRationale = BuildSemanticDecisionRationale(semanticResult.StatusMessage, fusionDecision);

            return new ClassificationStageOutput
            {
                Id = Guid.NewGuid(),
                Stage = ClassificationStage.Semantic,
                StageOrder = 2,
                ProposedSubcategoryId = null,
                Confidence = 0m,
                RationaleCode = Truncate(semanticResult.StatusCode, 120),
                Rationale = semanticFailureRationale,
                EscalatedToNextStage = escalatedToNextStage,
                ProducedAtUtc = producedAtUtc,
            };
        }

        var top = semanticResult.Candidates.FirstOrDefault();
        var rationale = top is null
            ? semanticResult.StatusMessage
            : $"Top semantic candidate score {top.NormalizedScore:F4} from source transaction {top.SourceTransactionId:D}.";

        var semanticDecisionRationale = BuildSemanticDecisionRationale(rationale, fusionDecision);

        return new ClassificationStageOutput
        {
            Id = Guid.NewGuid(),
            Stage = ClassificationStage.Semantic,
            StageOrder = 2,
            ProposedSubcategoryId = top?.ProposedSubcategoryId,
            Confidence = RoundConfidence(top?.NormalizedScore ?? 0m),
            RationaleCode = Truncate(semanticResult.StatusCode, 120),
            Rationale = semanticDecisionRationale,
            EscalatedToNextStage = escalatedToNextStage,
            ProducedAtUtc = producedAtUtc,
        };
    }

    private static ClassificationStageOutput BuildDeterministicStageOutput(
        DeterministicClassificationStageResult stageResult,
        ClassificationAmbiguityDecision gateDecision,
        ClassificationSpecialistRoutingDecision routingDecision,
        bool escalatedToNextStage,
        DateTime producedAtUtc)
    {
        var rationale = BuildDeterministicDecisionRationale(stageResult.Rationale, gateDecision, routingDecision);

        return new ClassificationStageOutput
        {
            Id = Guid.NewGuid(),
            Stage = ClassificationStage.Deterministic,
            StageOrder = 1,
            ProposedSubcategoryId = stageResult.ProposedSubcategoryId,
            Confidence = RoundConfidence(stageResult.Confidence),
            RationaleCode = Truncate(stageResult.RationaleCode.Trim(), 120),
            Rationale = rationale,
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
            var rationaleCode = mafResult.MessagingSendDenied
                ? MafFallbackGraphStatusCodes.ExternalMessagingSendDenied
                : Truncate(mafResult.StatusCode, 120);
            var rationale = mafResult.MessagingSendDenied
                ? BuildMessagingGuardrailRationale(mafResult.StatusMessage, mafResult.MessagingSendDeniedCount, mafResult.MessagingSendDeniedActions)
                : Truncate(mafResult.StatusMessage, 500);

            return new ClassificationStageOutput
            {
                Id = Guid.NewGuid(),
                Stage = ClassificationStage.MafFallback,
                StageOrder = 3,
                ProposedSubcategoryId = null,
                Confidence = 0m,
                RationaleCode = rationaleCode,
                Rationale = rationale,
                EscalatedToNextStage = false,
                ProducedAtUtc = producedAtUtc,
            };
        }

        var topProposal = mafResult.Proposals[0];
        var topProposalRationale = mafResult.MessagingSendDenied
            ? BuildMessagingGuardrailRationale(topProposal.Rationale, mafResult.MessagingSendDeniedCount, mafResult.MessagingSendDeniedActions)
            : topProposal.Rationale;
        var topProposalRationaleCode = mafResult.MessagingSendDenied
            ? MafFallbackGraphStatusCodes.ExternalMessagingSendDenied
            : Truncate(topProposal.RationaleCode, 120);

        return new ClassificationStageOutput
        {
            Id = Guid.NewGuid(),
            Stage = ClassificationStage.MafFallback,
            StageOrder = 3,
            ProposedSubcategoryId = topProposal.ProposedSubcategoryId,
            Confidence = RoundConfidence(topProposal.Confidence),
            RationaleCode = topProposalRationaleCode,
            Rationale = Truncate(topProposalRationale, 500),
            EscalatedToNextStage = false,
            ProducedAtUtc = producedAtUtc,
        };
    }

    private static string BuildMessagingGuardrailRationale(
        string baseRationale,
        int deniedCount,
        string? deniedActionsCsv)
    {
        var auditSuffix = deniedCount > 0
            ? $" Guardrail denied {deniedCount} external send action(s): {deniedActionsCsv ?? "unknown"}."
            : " Guardrail denied one or more external send actions.";
        return Truncate($"{baseRationale}{auditSuffix}", 500);
    }

    private static string BuildDeterministicDecisionRationale(
        string deterministicRationale,
        ClassificationAmbiguityDecision gateDecision,
        ClassificationSpecialistRoutingDecision routingDecision)
    {
        var stageRationale = $"{deterministicRationale} Gate decision {gateDecision.DecisionReasonCode}: {gateDecision.DecisionRationale} Routing decision {routingDecision.DecisionReasonCode}: {routingDecision.DecisionRationale}";
        return Truncate(stageRationale, 500);
    }

    private static string BuildSemanticDecisionRationale(
        string semanticRationale,
        ClassificationConfidenceFusionDecision fusionDecision)
    {
        var stageRationale = $"{semanticRationale} Fusion decision {fusionDecision.DecisionReasonCode}: {fusionDecision.DecisionRationale}";
        return Truncate(stageRationale, 500);
    }

    private static ClassificationWorkflowFinalDecision ResolveFinalDecision(
        ClassificationConfidenceFusionDecision fusionDecision,
        MafFallbackGraphResult? mafResult,
        ClassificationStageOutput? mafStageOutput)
    {
        var baseDecision = new ClassificationWorkflowFinalDecision(
            fusionDecision.Decision,
            fusionDecision.ReviewStatus,
            fusionDecision.ProposedSubcategoryId,
            RoundConfidence(fusionDecision.FinalConfidence),
            fusionDecision.DecisionReasonCode,
            fusionDecision.DecisionRationale,
            fusionDecision.AgentNoteSummary);

        if (mafStageOutput is null || baseDecision.Decision != ClassificationDecision.NeedsReview)
        {
            return baseDecision;
        }

        var topProposal = mafResult?.Proposals.FirstOrDefault();
        var resolvedSubcategoryId = baseDecision.ProposedSubcategoryId ?? mafStageOutput.ProposedSubcategoryId;
        var resolvedConfidence = RoundConfidence(decimal.Max(baseDecision.FinalConfidence, mafStageOutput.Confidence));
        var resolvedReasonCode = string.IsNullOrWhiteSpace(mafStageOutput.RationaleCode)
            ? baseDecision.DecisionReasonCode
            : mafStageOutput.RationaleCode;
        var resolvedRationale = string.IsNullOrWhiteSpace(mafStageOutput.Rationale)
            ? baseDecision.DecisionRationale
            : mafStageOutput.Rationale;
        var resolvedAgentNoteSummary = string.IsNullOrWhiteSpace(baseDecision.AgentNoteSummary)
            ? topProposal?.AgentNoteSummary
            : baseDecision.AgentNoteSummary;

        return new ClassificationWorkflowFinalDecision(
            ClassificationDecision.NeedsReview,
            baseDecision.ReviewStatus,
            resolvedSubcategoryId,
            resolvedConfidence,
            resolvedReasonCode,
            resolvedRationale,
            resolvedAgentNoteSummary);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }

    private static ClassificationConfidenceFusionDecision ApplyRoutingPolicyDecision(
        TransactionReviewStatus currentReviewStatus,
        ClassificationConfidenceFusionDecision fusionDecision,
        ClassificationSpecialistRoutingDecision routingDecision)
    {
        if (!routingDecision.OverrideFinalDecisionToNeedsReview)
        {
            return fusionDecision;
        }

        return new ClassificationConfidenceFusionDecision(
            ClassificationDecision.NeedsReview,
            ResolveNeedsReviewStatus(currentReviewStatus),
            ProposedSubcategoryId: null,
            RoundConfidence(fusionDecision.FinalConfidence),
            routingDecision.DecisionReasonCode,
            routingDecision.DecisionRationale,
            routingDecision.AgentNoteSummary,
            EscalatedToNextStage: false);
    }

    private static TransactionReviewStatus ResolveNeedsReviewStatus(TransactionReviewStatus currentReviewStatus)
    {
        if (TransactionReviewStateMachine.TryTransition(
                currentReviewStatus,
                TransactionReviewAction.RouteToNeedsReview,
                out var nextStatus))
        {
            return nextStatus;
        }

        return TransactionReviewStatus.NeedsReview;
    }
}
