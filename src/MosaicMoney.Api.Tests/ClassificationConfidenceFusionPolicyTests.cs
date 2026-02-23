using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.Classification;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class ClassificationConfidenceFusionPolicyTests
{
    private readonly ClassificationConfidenceFusionPolicy _policy = new();

    [Fact]
    public void Evaluate_DeterministicCategorized_PreservesDeterministicPrecedence()
    {
        var deterministicSubcategoryId = Guid.NewGuid();
        var deterministicResult = BuildDeterministicStageResult(
            proposedSubcategoryId: deterministicSubcategoryId,
            confidence: 0.9400m,
            rationaleCode: DeterministicClassificationReasonCodes.KeywordMatch,
            hasConflict: false);
        var ambiguityDecision = BuildAmbiguityDecision(
            decision: ClassificationDecision.Categorized,
            reviewStatus: TransactionReviewStatus.None,
            finalConfidence: 0.9400m,
            reasonCode: ClassificationAmbiguityReasonCodes.DeterministicAccepted);
        var semanticResult = new SemanticRetrievalResult(
            Succeeded: true,
            StatusCode: SemanticRetrievalStatusCodes.Ok,
            StatusMessage: "Semantic candidates resolved successfully.",
            Candidates:
            [
                BuildSemanticCandidate(Guid.NewGuid(), 0.9900m),
            ]);

        var result = _policy.Evaluate(
            TransactionReviewStatus.None,
            deterministicResult,
            ambiguityDecision,
            semanticResult);

        Assert.Equal(ClassificationDecision.Categorized, result.Decision);
        Assert.Equal(deterministicSubcategoryId, result.ProposedSubcategoryId);
        Assert.Equal(ClassificationAmbiguityReasonCodes.DeterministicAccepted, result.DecisionReasonCode);
        Assert.False(result.EscalatedToNextStage);
    }

    [Fact]
    public void Evaluate_SemanticFallbackAtThresholdBoundary_Categorizes()
    {
        var semanticSubcategoryId = Guid.NewGuid();
        var deterministicResult = BuildDeterministicStageResult(
            proposedSubcategoryId: null,
            confidence: 0m,
            rationaleCode: DeterministicClassificationReasonCodes.NoRuleMatch,
            hasConflict: false);
        var ambiguityDecision = BuildAmbiguityDecision(
            decision: ClassificationDecision.NeedsReview,
            reviewStatus: TransactionReviewStatus.NeedsReview,
            finalConfidence: 0m,
            reasonCode: ClassificationAmbiguityReasonCodes.NoDeterministicMatch);
        var threshold = ClassificationConfidenceFusionPolicy.MinimumSemanticConfidenceForAutoCategorization;
        var gap = ClassificationConfidenceFusionPolicy.MinimumSemanticTopGapForAutoCategorization;
        var semanticResult = new SemanticRetrievalResult(
            Succeeded: true,
            StatusCode: SemanticRetrievalStatusCodes.Ok,
            StatusMessage: "Semantic candidates resolved successfully.",
            Candidates:
            [
                BuildSemanticCandidate(semanticSubcategoryId, threshold),
                BuildSemanticCandidate(Guid.NewGuid(), threshold - gap),
            ]);

        var result = _policy.Evaluate(
            TransactionReviewStatus.None,
            deterministicResult,
            ambiguityDecision,
            semanticResult);

        Assert.Equal(ClassificationDecision.Categorized, result.Decision);
        Assert.Equal(semanticSubcategoryId, result.ProposedSubcategoryId);
        Assert.Equal(ClassificationConfidenceFusionReasonCodes.SemanticFallbackAccepted, result.DecisionReasonCode);
        Assert.Equal(threshold, result.FinalConfidence);
        Assert.False(result.EscalatedToNextStage);
    }

    [Fact]
    public void Evaluate_SemanticFallbackBelowThreshold_RoutesToNeedsReview()
    {
        var deterministicResult = BuildDeterministicStageResult(
            proposedSubcategoryId: null,
            confidence: 0m,
            rationaleCode: DeterministicClassificationReasonCodes.NoRuleMatch,
            hasConflict: false);
        var ambiguityDecision = BuildAmbiguityDecision(
            decision: ClassificationDecision.NeedsReview,
            reviewStatus: TransactionReviewStatus.NeedsReview,
            finalConfidence: 0m,
            reasonCode: ClassificationAmbiguityReasonCodes.NoDeterministicMatch);
        var threshold = ClassificationConfidenceFusionPolicy.MinimumSemanticConfidenceForAutoCategorization;
        var semanticResult = new SemanticRetrievalResult(
            Succeeded: true,
            StatusCode: SemanticRetrievalStatusCodes.Ok,
            StatusMessage: "Semantic candidates resolved successfully.",
            Candidates:
            [
                BuildSemanticCandidate(Guid.NewGuid(), threshold - 0.0001m),
            ]);

        var result = _policy.Evaluate(
            TransactionReviewStatus.None,
            deterministicResult,
            ambiguityDecision,
            semanticResult);

        Assert.Equal(ClassificationDecision.NeedsReview, result.Decision);
        Assert.Equal(TransactionReviewStatus.NeedsReview, result.ReviewStatus);
        Assert.Equal(ClassificationConfidenceFusionReasonCodes.SemanticBelowThreshold, result.DecisionReasonCode);
        Assert.True(result.EscalatedToNextStage);
    }

    [Fact]
    public void Evaluate_SemanticTopGapBelowThreshold_RoutesToNeedsReviewConflict()
    {
        var deterministicResult = BuildDeterministicStageResult(
            proposedSubcategoryId: null,
            confidence: 0m,
            rationaleCode: DeterministicClassificationReasonCodes.NoRuleMatch,
            hasConflict: false);
        var ambiguityDecision = BuildAmbiguityDecision(
            decision: ClassificationDecision.NeedsReview,
            reviewStatus: TransactionReviewStatus.NeedsReview,
            finalConfidence: 0m,
            reasonCode: ClassificationAmbiguityReasonCodes.NoDeterministicMatch);
        var threshold = ClassificationConfidenceFusionPolicy.MinimumSemanticConfidenceForAutoCategorization;
        var gap = ClassificationConfidenceFusionPolicy.MinimumSemanticTopGapForAutoCategorization;
        var semanticResult = new SemanticRetrievalResult(
            Succeeded: true,
            StatusCode: SemanticRetrievalStatusCodes.Ok,
            StatusMessage: "Semantic candidates resolved successfully.",
            Candidates:
            [
                BuildSemanticCandidate(Guid.NewGuid(), threshold + 0.0300m),
                BuildSemanticCandidate(Guid.NewGuid(), threshold + 0.0300m - (gap - 0.0100m)),
            ]);

        var result = _policy.Evaluate(
            TransactionReviewStatus.None,
            deterministicResult,
            ambiguityDecision,
            semanticResult);

        Assert.Equal(ClassificationDecision.NeedsReview, result.Decision);
        Assert.Equal(ClassificationConfidenceFusionReasonCodes.SemanticCandidateConflict, result.DecisionReasonCode);
        Assert.True(result.EscalatedToNextStage);
    }

    [Fact]
    public void Evaluate_DeterministicSemanticMismatch_RoutesToNeedsReviewConflict()
    {
        var deterministicResult = BuildDeterministicStageResult(
            proposedSubcategoryId: Guid.NewGuid(),
            confidence: 0.6200m,
            rationaleCode: DeterministicClassificationReasonCodes.KeywordMatch,
            hasConflict: false);
        var ambiguityDecision = BuildAmbiguityDecision(
            decision: ClassificationDecision.NeedsReview,
            reviewStatus: TransactionReviewStatus.NeedsReview,
            finalConfidence: 0.6200m,
            reasonCode: ClassificationAmbiguityReasonCodes.LowConfidence);
        var semanticResult = new SemanticRetrievalResult(
            Succeeded: true,
            StatusCode: SemanticRetrievalStatusCodes.Ok,
            StatusMessage: "Semantic candidates resolved successfully.",
            Candidates:
            [
                BuildSemanticCandidate(Guid.NewGuid(), 0.9600m),
            ]);

        var result = _policy.Evaluate(
            TransactionReviewStatus.None,
            deterministicResult,
            ambiguityDecision,
            semanticResult);

        Assert.Equal(ClassificationDecision.NeedsReview, result.Decision);
        Assert.Equal(ClassificationConfidenceFusionReasonCodes.DeterministicSemanticConflict, result.DecisionReasonCode);
        Assert.True(result.EscalatedToNextStage);
    }

    private static DeterministicClassificationStageResult BuildDeterministicStageResult(
        Guid? proposedSubcategoryId,
        decimal confidence,
        string rationaleCode,
        bool hasConflict)
    {
        return new DeterministicClassificationStageResult(
            proposedSubcategoryId,
            confidence,
            rationaleCode,
            "deterministic stage test rationale",
            hasConflict,
            []);
    }

    private static ClassificationAmbiguityDecision BuildAmbiguityDecision(
        ClassificationDecision decision,
        TransactionReviewStatus reviewStatus,
        decimal finalConfidence,
        string reasonCode)
    {
        return new ClassificationAmbiguityDecision(
            decision,
            reviewStatus,
            finalConfidence,
            reasonCode,
            "ambiguity policy test rationale",
            "ambiguity policy test summary");
    }

    private static SemanticRetrievalCandidate BuildSemanticCandidate(Guid subcategoryId, decimal normalizedScore)
    {
        return new SemanticRetrievalCandidate(
            subcategoryId,
            normalizedScore,
            Guid.NewGuid(),
            1,
            "postgresql.pgvector.cosine_distance",
            "seed",
            "{}");
    }
}