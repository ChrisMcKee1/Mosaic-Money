using Xunit;
using Xunit.Abstractions;

namespace MosaicMoney.Api.Tests;

public sealed class AgenticEvalReleaseGateTests(ITestOutputHelper output)
{
    [Fact]
    public async Task EvaluateAsync_AllCriteriaMeetReleaseBlockingThresholds()
    {
        var report = await AgenticEvalReleaseGate.EvaluateAsync();

        foreach (var criterion in report.Criteria)
        {
            output.WriteLine(
                "{0}: score={1:F4}, threshold={2:F4}, checks={3}/{4}, passed={5}, evidence={6}",
                criterion.Name,
                criterion.Score,
                criterion.Threshold,
                criterion.PassedChecks,
                criterion.TotalChecks,
                criterion.Passed,
                criterion.Evidence);
        }

        Assert.True(report.IsReleaseReady, report.ToFailureMessage());
    }

    [Theory]
    [InlineData(AgenticEvalReleaseGate.RoutingCorrectnessCriterionName)]
    [InlineData(AgenticEvalReleaseGate.AmbiguityFailClosedCriterionName)]
    [InlineData(AgenticEvalReleaseGate.ExternalMessagingHardStopCriterionName)]
    [InlineData(AgenticEvalReleaseGate.AgentNoteExplainabilityCriterionName)]
    public async Task EvaluateAsync_IndividualCriterionMeetsThreshold(string criterionName)
    {
        var report = await AgenticEvalReleaseGate.EvaluateAsync();
        var criterion = report.GetCriterion(criterionName);

        Assert.True(
            criterion.Passed,
            $"{AgenticEvalReleaseGate.GateId} criterion '{criterion.Name}' failed with score {criterion.Score:F4} below threshold {criterion.Threshold:F4}. Evidence: {criterion.Evidence}");
    }
}
