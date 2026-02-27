using MosaicMoney.Api.Domain.Ledger.Classification;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class AgenticEvalSpecialistEvaluatorPackTests
{
    [Fact]
    public void BuildSnapshot_IncludesAllSpecialistLanes()
    {
        var snapshot = AgenticEvalSpecialistEvaluatorPack.BuildSnapshot(
            evaluatedAtUtc: DateTimeOffset.Parse("2026-02-27T00:00:00+00:00"));

        var laneKeys = snapshot.LanePacks
            .Select(static lane => lane.SpecialistKey)
            .ToArray();

        Assert.Equal(ClassificationSpecialistKeys.All, laneKeys);
    }

    [Fact]
    public void BuildSnapshot_IncludesThresholdsForReleaseGateCriteria()
    {
        var snapshot = AgenticEvalSpecialistEvaluatorPack.BuildSnapshot();

        foreach (var lane in snapshot.LanePacks)
        {
            var thresholdsByCriterion = lane.Thresholds.ToDictionary(
                static threshold => threshold.Criterion,
                StringComparer.Ordinal);

            Assert.True(thresholdsByCriterion.ContainsKey(AgenticEvalReleaseGate.RoutingCorrectnessCriterionName));
            Assert.True(thresholdsByCriterion.ContainsKey(AgenticEvalReleaseGate.AmbiguityFailClosedCriterionName));
            Assert.True(thresholdsByCriterion.ContainsKey(AgenticEvalReleaseGate.ExternalMessagingHardStopCriterionName));
            Assert.True(thresholdsByCriterion.ContainsKey(AgenticEvalReleaseGate.AgentNoteExplainabilityCriterionName));
        }
    }

    [Fact]
    public void BuildSnapshot_UsesDefaultStageGatingForSpecialistLanes()
    {
        var snapshot = AgenticEvalSpecialistEvaluatorPack.BuildSnapshot();
        var lanes = snapshot.LanePacks.ToDictionary(
            static lane => lane.SpecialistKey,
            StringComparer.Ordinal);

        Assert.True(lanes[ClassificationSpecialistKeys.Categorization].AllowSemanticStage);
        Assert.True(lanes[ClassificationSpecialistKeys.Categorization].AllowMafFallbackStage);

        Assert.False(lanes[ClassificationSpecialistKeys.Transfer].AllowSemanticStage);
        Assert.False(lanes[ClassificationSpecialistKeys.Transfer].AllowMafFallbackStage);
        Assert.False(lanes[ClassificationSpecialistKeys.Income].AllowSemanticStage);
        Assert.False(lanes[ClassificationSpecialistKeys.Income].AllowMafFallbackStage);
        Assert.False(lanes[ClassificationSpecialistKeys.DebtQuality].AllowSemanticStage);
        Assert.False(lanes[ClassificationSpecialistKeys.DebtQuality].AllowMafFallbackStage);
        Assert.False(lanes[ClassificationSpecialistKeys.Investment].AllowSemanticStage);
        Assert.False(lanes[ClassificationSpecialistKeys.Investment].AllowMafFallbackStage);
        Assert.False(lanes[ClassificationSpecialistKeys.Anomaly].AllowSemanticStage);
        Assert.False(lanes[ClassificationSpecialistKeys.Anomaly].AllowMafFallbackStage);
    }
}
