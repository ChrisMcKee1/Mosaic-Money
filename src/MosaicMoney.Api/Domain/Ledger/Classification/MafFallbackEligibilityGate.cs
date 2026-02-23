namespace MosaicMoney.Api.Domain.Ledger.Classification;

public static class MafFallbackEligibilityReasonCodes
{
    public const string EligibleAfterSemanticInsufficiency = "maf_eligible_after_semantic_insufficiency";
    public const string IneligibleFinalizedBeforeFallback = "maf_ineligible_finalized_before_fallback";
    public const string IneligibleSemanticNotAttempted = "maf_ineligible_semantic_not_attempted";
    public const string IneligibleExistingNeedsReview = "maf_ineligible_existing_needs_review";
}

public sealed record MafFallbackEligibilityDecision(
    bool IsEligible,
    string ReasonCode,
    string Reason);

public interface IMafFallbackEligibilityGate
{
    MafFallbackEligibilityDecision Evaluate(
        ClassificationAmbiguityDecision ambiguityDecision,
        ClassificationConfidenceFusionDecision fusionDecision,
        bool semanticStageAttempted);
}

public sealed class MafFallbackEligibilityGate : IMafFallbackEligibilityGate
{
    public MafFallbackEligibilityDecision Evaluate(
        ClassificationAmbiguityDecision ambiguityDecision,
        ClassificationConfidenceFusionDecision fusionDecision,
        bool semanticStageAttempted)
    {
        if (ambiguityDecision.DecisionReasonCode == ClassificationAmbiguityReasonCodes.ExistingNeedsReviewState)
        {
            return new MafFallbackEligibilityDecision(
                IsEligible: false,
                MafFallbackEligibilityReasonCodes.IneligibleExistingNeedsReview,
                "Transaction already required human review before this run; do not invoke MAF fallback.");
        }

        if (!semanticStageAttempted)
        {
            return new MafFallbackEligibilityDecision(
                IsEligible: false,
                MafFallbackEligibilityReasonCodes.IneligibleSemanticNotAttempted,
                "MAF fallback requires deterministic and semantic stages to execute first.");
        }

        if (fusionDecision.Decision != ClassificationDecision.NeedsReview || !fusionDecision.EscalatedToNextStage)
        {
            return new MafFallbackEligibilityDecision(
                IsEligible: false,
                MafFallbackEligibilityReasonCodes.IneligibleFinalizedBeforeFallback,
                "Classification was resolved before fallback; MAF must not run.");
        }

        return new MafFallbackEligibilityDecision(
            IsEligible: true,
            MafFallbackEligibilityReasonCodes.EligibleAfterSemanticInsufficiency,
            "Deterministic and semantic stages were insufficient; MAF fallback may run.");
    }
}