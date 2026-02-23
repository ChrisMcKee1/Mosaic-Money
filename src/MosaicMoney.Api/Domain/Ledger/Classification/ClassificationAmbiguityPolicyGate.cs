namespace MosaicMoney.Api.Domain.Ledger.Classification;

public static class ClassificationAmbiguityReasonCodes
{
    public const string ExistingNeedsReviewState = "ambiguity_existing_needs_review";
    public const string ConflictingDeterministicRules = "ambiguity_conflicting_deterministic_rules";
    public const string NoDeterministicMatch = "ambiguity_no_deterministic_match";
    public const string LowConfidence = "ambiguity_low_confidence";
    public const string DeterministicAccepted = "deterministic_confident_match";
}

public sealed record ClassificationAmbiguityDecision(
    ClassificationDecision Decision,
    TransactionReviewStatus ReviewStatus,
    decimal FinalConfidence,
    string DecisionReasonCode,
    string DecisionRationale,
    string? AgentNoteSummary);

public interface IClassificationAmbiguityPolicyGate
{
    ClassificationAmbiguityDecision Evaluate(
        TransactionReviewStatus currentReviewStatus,
        DeterministicClassificationStageResult deterministicResult);
}

public sealed class ClassificationAmbiguityPolicyGate : IClassificationAmbiguityPolicyGate
{
    public const decimal MinimumConfidenceForAutoCategorization = 0.8500m;

    public ClassificationAmbiguityDecision Evaluate(
        TransactionReviewStatus currentReviewStatus,
        DeterministicClassificationStageResult deterministicResult)
    {
        if (currentReviewStatus == TransactionReviewStatus.NeedsReview)
        {
            return BuildNeedsReviewDecision(
                currentReviewStatus,
                deterministicResult,
                ClassificationAmbiguityReasonCodes.ExistingNeedsReviewState,
                "Transaction is already in NeedsReview and must remain human-gated.");
        }

        if (deterministicResult.HasConflict)
        {
            return BuildNeedsReviewDecision(
                currentReviewStatus,
                deterministicResult,
                ClassificationAmbiguityReasonCodes.ConflictingDeterministicRules,
                "Deterministic stage produced conflicting rule candidates with similar confidence.");
        }

        if (!deterministicResult.ProposedSubcategoryId.HasValue)
        {
            return BuildNeedsReviewDecision(
                currentReviewStatus,
                deterministicResult,
                ClassificationAmbiguityReasonCodes.NoDeterministicMatch,
                "Deterministic stage did not produce a single actionable subcategory match.");
        }

        if (deterministicResult.Confidence < MinimumConfidenceForAutoCategorization)
        {
            return BuildNeedsReviewDecision(
                currentReviewStatus,
                deterministicResult,
                ClassificationAmbiguityReasonCodes.LowConfidence,
                $"Deterministic confidence {deterministicResult.Confidence:F4} is below required threshold {MinimumConfidenceForAutoCategorization:F4}.");
        }

        var categorizedReviewStatus = currentReviewStatus == TransactionReviewStatus.Reviewed
            ? TransactionReviewStatus.Reviewed
            : TransactionReviewStatus.None;

        return new ClassificationAmbiguityDecision(
            ClassificationDecision.Categorized,
            categorizedReviewStatus,
            RoundConfidence(deterministicResult.Confidence),
            ClassificationAmbiguityReasonCodes.DeterministicAccepted,
            "Deterministic stage produced a single high-confidence classification candidate.",
            $"Deterministic stage accepted candidate with confidence {deterministicResult.Confidence:F4}.");
    }

    private static ClassificationAmbiguityDecision BuildNeedsReviewDecision(
        TransactionReviewStatus currentReviewStatus,
        DeterministicClassificationStageResult deterministicResult,
        string reasonCode,
        string rationale)
    {
        var reviewStatus = ResolveNeedsReviewStatus(currentReviewStatus);

        return new ClassificationAmbiguityDecision(
            ClassificationDecision.NeedsReview,
            reviewStatus,
            RoundConfidence(deterministicResult.Confidence),
            reasonCode,
            rationale,
            $"Deterministic stage routed to NeedsReview ({reasonCode}, confidence {deterministicResult.Confidence:F4}).");
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

        // Fail closed in case state-machine rules change unexpectedly.
        return TransactionReviewStatus.NeedsReview;
    }

    private static decimal RoundConfidence(decimal value)
    {
        return decimal.Round(value, 4, MidpointRounding.AwayFromZero);
    }
}
