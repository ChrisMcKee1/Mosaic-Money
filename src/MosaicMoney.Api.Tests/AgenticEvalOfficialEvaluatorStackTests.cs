using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class AgenticEvalOfficialEvaluatorStackTests
{
    [Fact]
    public void BuildSnapshot_ContainsMappingsForAllReleaseGateCriteria()
    {
        var snapshot = AgenticEvalOfficialEvaluatorStack.BuildSnapshot(
            new AgenticEvalOfficialStackEnvironment(
                ProjectEndpoint: null,
                ModelDeploymentName: null,
                SubscriptionId: null,
                ResourceGroupName: null,
                ProjectName: null),
            evaluatedAtUtc: DateTimeOffset.Parse("2026-02-25T00:00:00+00:00"));

        var criteria = snapshot.CriteriaMappings.Select(static mapping => mapping.CriterionName).ToHashSet(StringComparer.Ordinal);

        Assert.Contains(AgenticEvalReleaseGate.RoutingCorrectnessCriterionName, criteria);
        Assert.Contains(AgenticEvalReleaseGate.AmbiguityFailClosedCriterionName, criteria);
        Assert.Contains(AgenticEvalReleaseGate.ExternalMessagingHardStopCriterionName, criteria);
        Assert.Contains(AgenticEvalReleaseGate.AgentNoteExplainabilityCriterionName, criteria);
    }

    [Fact]
    public void BuildSnapshot_ContainsRequiredDatasetSchemaFields()
    {
        var snapshot = AgenticEvalOfficialEvaluatorStack.BuildSnapshot(
            new AgenticEvalOfficialStackEnvironment(null, null, null, null, null));

        var requiredFields = snapshot.DatasetSchema
            .Where(static field => field.Required)
            .Select(static field => field.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("query", requiredFields);
        Assert.Contains("response", requiredFields);
        Assert.Contains("tool_definitions", requiredFields);
        Assert.Contains("actions", requiredFields);
        Assert.Contains("expected_actions", requiredFields);
        Assert.Contains("ground_truth", requiredFields);
    }

    [Fact]
    public void BuildSnapshot_LoadsOfficialDotNetEvaluatorAnchors()
    {
        var snapshot = AgenticEvalOfficialEvaluatorStack.BuildSnapshot(
            new AgenticEvalOfficialStackEnvironment(null, null, null, null, null));

        Assert.True(snapshot.ExecutionReadiness.DotNetEvaluatorTypesLoaded);
        Assert.Contains("CoherenceEvaluator", snapshot.DotNetComponentTypeAnchors);
        Assert.Contains("RelevanceEvaluator", snapshot.DotNetComponentTypeAnchors);
        Assert.Contains("FluencyEvaluator", snapshot.DotNetComponentTypeAnchors);
        Assert.Contains("ViolenceEvaluator", snapshot.DotNetComponentTypeAnchors);
        Assert.Contains(
            "Microsoft.Extensions.AI.Evaluation.Reporting.ReportingConfiguration",
            snapshot.DotNetComponentTypeAnchors);
    }

    [Fact]
    public void BuildSnapshot_FailsClosedWhenCloudEvaluatorInputsAreMissing()
    {
        var snapshot = AgenticEvalOfficialEvaluatorStack.BuildSnapshot(
            new AgenticEvalOfficialStackEnvironment(null, null, null, null, null));

        Assert.False(snapshot.ExecutionReadiness.FoundryCloudEvaluatorsConfigured);
        Assert.True(snapshot.ExecutionReadiness.FailClosedIfCloudUnavailable);
        Assert.Contains("fail-closed", snapshot.ExecutionReadiness.ReadinessNote, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSnapshot_ReportsCloudReadyWhenRequiredConfigurationIsProvided()
    {
        var snapshot = AgenticEvalOfficialEvaluatorStack.BuildSnapshot(
            new AgenticEvalOfficialStackEnvironment(
                ProjectEndpoint: "https://example.services.ai.azure.com/api/projects/mosaic",
                ModelDeploymentName: "gpt-4o-mini",
                SubscriptionId: "00000000-0000-0000-0000-000000000000",
                ResourceGroupName: "rg-mosaic-money",
                ProjectName: "mosaic-money-foundry"));

        Assert.True(snapshot.ExecutionReadiness.FoundryCloudEvaluatorsConfigured);
        Assert.True(snapshot.ExecutionReadiness.FailClosedIfCloudUnavailable);
    }
}