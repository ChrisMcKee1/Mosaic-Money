using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.Classification;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class MafFallbackEligibilityGateTests
{
    private readonly MafFallbackEligibilityGate _gate = new();

    [Fact]
    public void Evaluate_SemanticNotAttempted_ReturnsIneligible()
    {
        var decision = _gate.Evaluate(
            BuildAmbiguityDecision(ClassificationAmbiguityReasonCodes.NoDeterministicMatch),
            BuildFusionDecision(ClassificationDecision.NeedsReview, escalatedToNextStage: true),
            semanticStageAttempted: false);

        Assert.False(decision.IsEligible);
        Assert.Equal(MafFallbackEligibilityReasonCodes.IneligibleSemanticNotAttempted, decision.ReasonCode);
    }

    [Fact]
    public void Evaluate_ExistingNeedsReviewState_ReturnsIneligible()
    {
        var decision = _gate.Evaluate(
            BuildAmbiguityDecision(ClassificationAmbiguityReasonCodes.ExistingNeedsReviewState),
            BuildFusionDecision(ClassificationDecision.NeedsReview, escalatedToNextStage: true),
            semanticStageAttempted: true);

        Assert.False(decision.IsEligible);
        Assert.Equal(MafFallbackEligibilityReasonCodes.IneligibleExistingNeedsReview, decision.ReasonCode);
    }

    [Fact]
    public void Evaluate_FinalizedBeforeFallback_ReturnsIneligible()
    {
        var decision = _gate.Evaluate(
            BuildAmbiguityDecision(ClassificationAmbiguityReasonCodes.NoDeterministicMatch),
            BuildFusionDecision(ClassificationDecision.Categorized, escalatedToNextStage: false),
            semanticStageAttempted: true);

        Assert.False(decision.IsEligible);
        Assert.Equal(MafFallbackEligibilityReasonCodes.IneligibleFinalizedBeforeFallback, decision.ReasonCode);
    }

    [Fact]
    public void Evaluate_DeterministicAndSemanticInsufficient_ReturnsEligible()
    {
        var decision = _gate.Evaluate(
            BuildAmbiguityDecision(ClassificationAmbiguityReasonCodes.NoDeterministicMatch),
            BuildFusionDecision(ClassificationDecision.NeedsReview, escalatedToNextStage: true),
            semanticStageAttempted: true);

        Assert.True(decision.IsEligible);
        Assert.Equal(MafFallbackEligibilityReasonCodes.EligibleAfterSemanticInsufficiency, decision.ReasonCode);
    }

    private static ClassificationAmbiguityDecision BuildAmbiguityDecision(string reasonCode)
    {
        return new ClassificationAmbiguityDecision(
            Decision: ClassificationDecision.NeedsReview,
            ReviewStatus: TransactionReviewStatus.NeedsReview,
            FinalConfidence: 0m,
            DecisionReasonCode: reasonCode,
            DecisionRationale: "test rationale",
            AgentNoteSummary: "test summary");
    }

    private static ClassificationConfidenceFusionDecision BuildFusionDecision(
        ClassificationDecision decision,
        bool escalatedToNextStage)
    {
        return new ClassificationConfidenceFusionDecision(
            Decision: decision,
            ReviewStatus: decision == ClassificationDecision.NeedsReview ? TransactionReviewStatus.NeedsReview : TransactionReviewStatus.None,
            ProposedSubcategoryId: null,
            FinalConfidence: 0m,
            DecisionReasonCode: "test_reason",
            DecisionRationale: "test rationale",
            AgentNoteSummary: null,
            EscalatedToNextStage: escalatedToNextStage);
    }
}
