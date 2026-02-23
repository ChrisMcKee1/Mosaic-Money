using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.Classification;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class ClassificationAmbiguityPolicyGateTests
{
    private readonly ClassificationAmbiguityPolicyGate _gate = new();

    [Fact]
    public void Evaluate_ExistingNeedsReview_RemainsFailClosedToNeedsReview()
    {
        var deterministic = BuildStageResult(
            proposedSubcategoryId: Guid.NewGuid(),
            confidence: 0.9700m,
            hasConflict: false);

        var decision = _gate.Evaluate(TransactionReviewStatus.NeedsReview, deterministic);

        Assert.Equal(ClassificationDecision.NeedsReview, decision.Decision);
        Assert.Equal(TransactionReviewStatus.NeedsReview, decision.ReviewStatus);
        Assert.Equal(ClassificationAmbiguityReasonCodes.ExistingNeedsReviewState, decision.DecisionReasonCode);
    }

    [Fact]
    public void Evaluate_LowConfidence_RoutesToNeedsReview()
    {
        var deterministic = BuildStageResult(
            proposedSubcategoryId: Guid.NewGuid(),
            confidence: 0.6200m,
            hasConflict: false);

        var decision = _gate.Evaluate(TransactionReviewStatus.None, deterministic);

        Assert.Equal(ClassificationDecision.NeedsReview, decision.Decision);
        Assert.Equal(TransactionReviewStatus.NeedsReview, decision.ReviewStatus);
        Assert.Equal(ClassificationAmbiguityReasonCodes.LowConfidence, decision.DecisionReasonCode);
    }

    [Fact]
    public void Evaluate_Conflict_RoutesToNeedsReview()
    {
        var deterministic = BuildStageResult(
            proposedSubcategoryId: null,
            confidence: 0.9100m,
            hasConflict: true);

        var decision = _gate.Evaluate(TransactionReviewStatus.None, deterministic);

        Assert.Equal(ClassificationDecision.NeedsReview, decision.Decision);
        Assert.Equal(TransactionReviewStatus.NeedsReview, decision.ReviewStatus);
        Assert.Equal(ClassificationAmbiguityReasonCodes.ConflictingDeterministicRules, decision.DecisionReasonCode);
    }

    [Theory]
    [InlineData(TransactionReviewStatus.None, TransactionReviewStatus.None)]
    [InlineData(TransactionReviewStatus.Reviewed, TransactionReviewStatus.Reviewed)]
    public void Evaluate_HighConfidenceSingleCandidate_CategorizesWithoutNeedsReview(
        TransactionReviewStatus currentReviewStatus,
        TransactionReviewStatus expectedReviewStatus)
    {
        var deterministic = BuildStageResult(
            proposedSubcategoryId: Guid.NewGuid(),
            confidence: 0.9200m,
            hasConflict: false);

        var decision = _gate.Evaluate(currentReviewStatus, deterministic);

        Assert.Equal(ClassificationDecision.Categorized, decision.Decision);
        Assert.Equal(expectedReviewStatus, decision.ReviewStatus);
        Assert.Equal(ClassificationAmbiguityReasonCodes.DeterministicAccepted, decision.DecisionReasonCode);
    }

    private static DeterministicClassificationStageResult BuildStageResult(
        Guid? proposedSubcategoryId,
        decimal confidence,
        bool hasConflict)
    {
        return new DeterministicClassificationStageResult(
            proposedSubcategoryId,
            confidence,
            DeterministicClassificationReasonCodes.KeywordMatch,
            "deterministic stage test rationale",
            hasConflict,
            []);
    }
}
