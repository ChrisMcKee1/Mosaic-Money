using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace MosaicMoney.Api.Tests;

public sealed class AgenticEvalReleaseGateTests(ITestOutputHelper output)
{
    private const string EvidencePathEnvironmentVariableName = "MM_AI_11_EVIDENCE_PATH";
    private const string OfficialEvaluatorEvidencePathEnvironmentVariableName = "MM_AI_12_EVIDENCE_PATH";

    [Fact]
    public async Task EvaluateAsync_AllCriteriaMeetReleaseBlockingThresholds()
    {
        var report = await AgenticEvalReleaseGate.EvaluateAsync();

        WriteReportToOutput(report);
        TryWriteEvidenceArtifact(report);
        TryWriteOfficialEvaluatorReplayArtifact(report);

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

    private void WriteReportToOutput(AgenticEvalReleaseGateReport report)
    {
        output.WriteLine(
            "{0}: result={1}, releaseReady={2}",
            report.GateId,
            report.IsReleaseReady ? "GO" : "NO_GO",
            report.IsReleaseReady);

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

        output.WriteLine(
            "official_evaluator_stack: dotnetLoaded={0}, foundryConfigured={1}, failClosed={2}, note={3}",
            report.OfficialEvaluatorStack.ExecutionReadiness.DotNetEvaluatorTypesLoaded,
            report.OfficialEvaluatorStack.ExecutionReadiness.FoundryCloudEvaluatorsConfigured,
            report.OfficialEvaluatorStack.ExecutionReadiness.FailClosedIfCloudUnavailable,
            report.OfficialEvaluatorStack.ExecutionReadiness.ReadinessNote);
    }

    private void TryWriteEvidenceArtifact(AgenticEvalReleaseGateReport report)
    {
        var evidencePath = Environment.GetEnvironmentVariable(EvidencePathEnvironmentVariableName);
        if (string.IsNullOrWhiteSpace(evidencePath))
        {
            return;
        }

        var resolvedPath = Path.GetFullPath(evidencePath);
        var directoryPath = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var artifact = new AgenticEvalReleaseGateEvidence(
            GateId: report.GateId,
            EvaluatedAtUtc: DateTimeOffset.UtcNow,
            Result: report.IsReleaseReady ? "GO" : "NO_GO",
            IsReleaseReady: report.IsReleaseReady,
            OfficialEvaluatorStack: report.OfficialEvaluatorStack,
            Criteria:
            [
                .. report.Criteria.Select(static criterion => new AgenticEvalReleaseGateCriterionEvidence(
                    criterion.Name,
                    criterion.Score,
                    criterion.Threshold,
                    criterion.PassedChecks,
                    criterion.TotalChecks,
                    criterion.Passed,
                    criterion.Evidence)),
            ]);

        var serializedArtifact = JsonSerializer.Serialize(
            artifact,
            new JsonSerializerOptions
            {
                WriteIndented = true,
            });

        File.WriteAllText(resolvedPath, serializedArtifact);
        output.WriteLine("{0}: evidence artifact written to {1}", report.GateId, resolvedPath);
    }

    private void TryWriteOfficialEvaluatorReplayArtifact(AgenticEvalReleaseGateReport report)
    {
        var evidencePath = Environment.GetEnvironmentVariable(OfficialEvaluatorEvidencePathEnvironmentVariableName);
        if (string.IsNullOrWhiteSpace(evidencePath))
        {
            return;
        }

        var resolvedPath = Path.GetFullPath(evidencePath);
        var directoryPath = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var artifact = new AgenticEvalOfficialEvaluatorReplayEvidence(
            GateId: report.GateId,
            ProfileId: report.OfficialEvaluatorStack.ProfileId,
            EvaluatedAtUtc: DateTimeOffset.UtcNow,
            SourceLinks: report.OfficialEvaluatorStack.SourceLinks,
            DotNetPackageReferences: report.OfficialEvaluatorStack.DotNetPackageReferences,
            DotNetComponentTypeAnchors: report.OfficialEvaluatorStack.DotNetComponentTypeAnchors,
            DatasetSchema: report.OfficialEvaluatorStack.DatasetSchema,
            CriteriaMappings: report.OfficialEvaluatorStack.CriteriaMappings,
            ExecutionReadiness: report.OfficialEvaluatorStack.ExecutionReadiness,
            Notes: report.OfficialEvaluatorStack.Notes);

        var serializedArtifact = JsonSerializer.Serialize(
            artifact,
            new JsonSerializerOptions
            {
                WriteIndented = true,
            });

        File.WriteAllText(resolvedPath, serializedArtifact);
        output.WriteLine("{0}: official evaluator replay artifact written to {1}", report.GateId, resolvedPath);
    }

    private sealed record AgenticEvalReleaseGateEvidence(
        string GateId,
        DateTimeOffset EvaluatedAtUtc,
        string Result,
        bool IsReleaseReady,
        AgenticEvalOfficialEvaluatorStackSnapshot OfficialEvaluatorStack,
        IReadOnlyList<AgenticEvalReleaseGateCriterionEvidence> Criteria);

    private sealed record AgenticEvalReleaseGateCriterionEvidence(
        string Name,
        decimal Score,
        decimal Threshold,
        int PassedChecks,
        int TotalChecks,
        bool Passed,
        string Evidence);

    private sealed record AgenticEvalOfficialEvaluatorReplayEvidence(
        string GateId,
        string ProfileId,
        DateTimeOffset EvaluatedAtUtc,
        IReadOnlyList<string> SourceLinks,
        IReadOnlyList<string> DotNetPackageReferences,
        IReadOnlyList<string> DotNetComponentTypeAnchors,
        IReadOnlyList<AgenticEvalOfficialDatasetField> DatasetSchema,
        IReadOnlyList<AgenticEvalOfficialCriterionMapping> CriteriaMappings,
        AgenticEvalOfficialExecutionReadiness ExecutionReadiness,
        string Notes);
}
