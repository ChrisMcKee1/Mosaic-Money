namespace MosaicMoney.Api.Domain.Ledger.Classification;

public static class ClassificationConfidenceFusionReasonCodes
{
    public const string SemanticFallbackAccepted = "fusion_semantic_fallback_accepted";
    public const string SemanticBelowThreshold = "fusion_semantic_below_threshold";
    public const string SemanticCandidateConflict = "fusion_semantic_candidate_conflict";
    public const string DeterministicSemanticConflict = "fusion_deterministic_semantic_conflict";
}

public sealed record ClassificationConfidenceFusionDecision(
    ClassificationDecision Decision,
    TransactionReviewStatus ReviewStatus,
    Guid? ProposedSubcategoryId,
    decimal FinalConfidence,
    string DecisionReasonCode,
    string DecisionRationale,
    string? AgentNoteSummary,
    bool EscalatedToNextStage);

public interface IClassificationConfidenceFusionPolicy
{
    ClassificationConfidenceFusionDecision Evaluate(
        TransactionReviewStatus currentReviewStatus,
        DeterministicClassificationStageResult deterministicResult,
        ClassificationAmbiguityDecision ambiguityDecision,
        SemanticRetrievalResult? semanticResult);
}

public sealed class ClassificationConfidenceFusionPolicy : IClassificationConfidenceFusionPolicy
{
    public const decimal MinimumSemanticConfidenceForAutoCategorization = 0.9200m;
    public const decimal MinimumSemanticTopGapForAutoCategorization = 0.0500m;

    public ClassificationConfidenceFusionDecision Evaluate(
        TransactionReviewStatus currentReviewStatus,
        DeterministicClassificationStageResult deterministicResult,
        ClassificationAmbiguityDecision ambiguityDecision,
        SemanticRetrievalResult? semanticResult)
    {
        if (ambiguityDecision.Decision == ClassificationDecision.Categorized)
        {
            return BuildPassThroughDecision(ambiguityDecision, deterministicResult, escalatedToNextStage: false);
        }

        if (ambiguityDecision.Decision != ClassificationDecision.NeedsReview)
        {
            return BuildPassThroughDecision(ambiguityDecision, deterministicResult, escalatedToNextStage: true);
        }

        if (semanticResult is null || !semanticResult.Succeeded || semanticResult.Candidates.Count == 0)
        {
            return BuildPassThroughDecision(ambiguityDecision, deterministicResult, escalatedToNextStage: true);
        }

        var topCandidate = semanticResult.Candidates[0];

        if (deterministicResult.ProposedSubcategoryId.HasValue)
        {
            if (deterministicResult.ProposedSubcategoryId.Value != topCandidate.ProposedSubcategoryId)
            {
                return BuildNeedsReviewDecision(
                    currentReviewStatus,
                    decimal.Max(deterministicResult.Confidence, topCandidate.NormalizedScore),
                    ClassificationConfidenceFusionReasonCodes.DeterministicSemanticConflict,
                    "Deterministic and semantic stages proposed different subcategories. Review is required.",
                    "Classification remained in NeedsReview due to deterministic/semantic disagreement.");
            }

            // Deterministic low-confidence routes remain human-gated even with semantic agreement.
            return BuildPassThroughDecision(ambiguityDecision, deterministicResult, escalatedToNextStage: true);
        }

        if (topCandidate.NormalizedScore < MinimumSemanticConfidenceForAutoCategorization)
        {
            return BuildNeedsReviewDecision(
                currentReviewStatus,
                topCandidate.NormalizedScore,
                ClassificationConfidenceFusionReasonCodes.SemanticBelowThreshold,
                $"Semantic fallback score {topCandidate.NormalizedScore:F4} is below required threshold {MinimumSemanticConfidenceForAutoCategorization:F4}.",
                $"Semantic fallback remained in NeedsReview (score {topCandidate.NormalizedScore:F4} below threshold).");
        }

        var secondCandidate = semanticResult.Candidates.Count >= 2
            ? semanticResult.Candidates[1]
            : null;

        if (secondCandidate is not null)
        {
            var topGap = topCandidate.NormalizedScore - secondCandidate.NormalizedScore;
            if (topGap < MinimumSemanticTopGapForAutoCategorization)
            {
                return BuildNeedsReviewDecision(
                    currentReviewStatus,
                    topCandidate.NormalizedScore,
                    ClassificationConfidenceFusionReasonCodes.SemanticCandidateConflict,
                    $"Semantic fallback produced competing candidates with top-gap {topGap:F4}, below required {MinimumSemanticTopGapForAutoCategorization:F4}.",
                    $"Semantic fallback remained in NeedsReview due to competing top candidates (gap {topGap:F4}).");
            }
        }

        if (!IsSemanticFallbackEligible(deterministicResult, ambiguityDecision))
        {
            return BuildPassThroughDecision(ambiguityDecision, deterministicResult, escalatedToNextStage: true);
        }

        var categorizedReviewStatus = ResolveCategorizedReviewStatus(currentReviewStatus);
        return new ClassificationConfidenceFusionDecision(
            ClassificationDecision.Categorized,
            categorizedReviewStatus,
            topCandidate.ProposedSubcategoryId,
            RoundConfidence(topCandidate.NormalizedScore),
            ClassificationConfidenceFusionReasonCodes.SemanticFallbackAccepted,
            $"Deterministic no-match fallback accepted semantic candidate at {topCandidate.NormalizedScore:F4}.",
            $"Semantic fallback proposed subcategory with confidence {topCandidate.NormalizedScore:F4} after deterministic no-match.",
            false);
    }

    private static ClassificationConfidenceFusionDecision BuildPassThroughDecision(
        ClassificationAmbiguityDecision ambiguityDecision,
        DeterministicClassificationStageResult deterministicResult,
        bool escalatedToNextStage)
    {
        var proposedSubcategoryId = ambiguityDecision.Decision == ClassificationDecision.Categorized
            ? deterministicResult.ProposedSubcategoryId
            : null;

        return new ClassificationConfidenceFusionDecision(
            ambiguityDecision.Decision,
            ambiguityDecision.ReviewStatus,
            proposedSubcategoryId,
            RoundConfidence(ambiguityDecision.FinalConfidence),
            ambiguityDecision.DecisionReasonCode,
            ambiguityDecision.DecisionRationale,
            ambiguityDecision.AgentNoteSummary,
            escalatedToNextStage);
    }

    private static ClassificationConfidenceFusionDecision BuildNeedsReviewDecision(
        TransactionReviewStatus currentReviewStatus,
        decimal confidence,
        string reasonCode,
        string rationale,
        string agentNoteSummary)
    {
        return new ClassificationConfidenceFusionDecision(
            ClassificationDecision.NeedsReview,
            ResolveNeedsReviewStatus(currentReviewStatus),
            null,
            RoundConfidence(confidence),
            reasonCode,
            rationale,
            agentNoteSummary,
            true);
    }

    private static bool IsSemanticFallbackEligible(
        DeterministicClassificationStageResult deterministicResult,
        ClassificationAmbiguityDecision ambiguityDecision)
    {
        return !deterministicResult.ProposedSubcategoryId.HasValue
            && !deterministicResult.HasConflict
            && deterministicResult.RationaleCode == DeterministicClassificationReasonCodes.NoRuleMatch
            && ambiguityDecision.DecisionReasonCode == ClassificationAmbiguityReasonCodes.NoDeterministicMatch;
    }

    private static TransactionReviewStatus ResolveCategorizedReviewStatus(TransactionReviewStatus currentReviewStatus)
    {
        return currentReviewStatus == TransactionReviewStatus.Reviewed
            ? TransactionReviewStatus.Reviewed
            : TransactionReviewStatus.None;
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

    private static decimal RoundConfidence(decimal value)
    {
        return decimal.Round(value, 4, MidpointRounding.AwayFromZero);
    }
}