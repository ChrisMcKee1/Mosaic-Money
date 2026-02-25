using System.Text.Json;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class AgenticEvalOfficialEvaluatorArtifactsTests
{
    [Fact]
    public void CriteriaDatasetMappingJson_MatchesOfficialStackSnapshot()
    {
        var snapshot = BuildDeterministicSnapshot();
        var artifactPath = ResolveRepoPath("artifacts", "release-gates", "mm-ai-12", "criteria-dataset-mapping.json");

        using var document = JsonDocument.Parse(File.ReadAllText(artifactPath));
        var root = document.RootElement;

        Assert.Equal(snapshot.ProfileId, GetRequiredString(root, "profileId"));

        var artifactSourceLinks = ReadRequiredStringArray(root, "sourceLinks");
        Assert.Equal(snapshot.SourceLinks, artifactSourceLinks);

        var artifactDatasetSchema = ReadDatasetSchema(
            root.GetProperty("datasetSchema"),
            nameProperty: "name",
            typeProperty: "type",
            requiredProperty: "required",
            purposeProperty: "purpose");

        AssertDatasetSchemaEquals(snapshot.DatasetSchema, artifactDatasetSchema);

        var artifactCriteria = ReadCriteriaMappings(
            root.GetProperty("criteriaMappings"),
            criterionNameProperty: "criterion",
            dotnetHintsProperty: "dotnetEvaluatorTypeHints",
            foundryEvaluatorsProperty: "foundryEvaluators",
            foundryGradersProperty: "foundryGraders",
            datasetMappingsProperty: "datasetFieldMappings",
            rationaleProperty: "rationale");

        AssertCriteriaMappingsEqual(snapshot.CriteriaMappings, artifactCriteria);
    }

    [Fact]
    public void LatestReplayJson_MatchesOfficialStackSnapshotShape()
    {
        var snapshot = BuildDeterministicSnapshot();
        var artifactPath = ResolveRepoPath("artifacts", "release-gates", "mm-ai-12", "latest.json");

        using var document = JsonDocument.Parse(File.ReadAllText(artifactPath));
        var root = document.RootElement;

        Assert.Equal(AgenticEvalReleaseGate.GateId, GetRequiredString(root, "GateId"));
        Assert.Equal(snapshot.ProfileId, GetRequiredString(root, "ProfileId"));

        var artifactSourceLinks = ReadRequiredStringArray(root, "SourceLinks");
        Assert.Equal(snapshot.SourceLinks, artifactSourceLinks);

        var artifactPackageReferences = ReadRequiredStringArray(root, "DotNetPackageReferences");
        Assert.Equal(snapshot.DotNetPackageReferences, artifactPackageReferences);

        var artifactDatasetSchema = ReadDatasetSchema(
            root.GetProperty("DatasetSchema"),
            nameProperty: "Name",
            typeProperty: "Type",
            requiredProperty: "Required",
            purposeProperty: "Purpose");

        AssertDatasetSchemaEquals(snapshot.DatasetSchema, artifactDatasetSchema);

        var artifactCriteria = ReadCriteriaMappings(
            root.GetProperty("CriteriaMappings"),
            criterionNameProperty: "CriterionName",
            dotnetHintsProperty: "DotNetEvaluatorTypeHints",
            foundryEvaluatorsProperty: "FoundryEvaluatorNames",
            foundryGradersProperty: "FoundryGraderNames",
            datasetMappingsProperty: "DatasetFieldMappings",
            rationaleProperty: "Rationale");

        AssertCriteriaMappingsEqual(snapshot.CriteriaMappings, artifactCriteria);

        var executionReadiness = root.GetProperty("ExecutionReadiness");
        Assert.True(executionReadiness.GetProperty("FailClosedIfCloudUnavailable").GetBoolean());
    }

    [Fact]
    public void ReplayPack_IncludesSourceLinksAndOfflineCloudBoundaryNotes()
    {
        var snapshot = BuildDeterministicSnapshot();
        var replayPackPath = ResolveRepoPath("artifacts", "release-gates", "mm-ai-12", "replay-pack.md");
        var replayPack = File.ReadAllText(replayPackPath);

        foreach (var sourceLink in snapshot.SourceLinks)
        {
            Assert.Contains(sourceLink, replayPack, StringComparison.Ordinal);
        }

        Assert.Contains("## Offline Complete Today", replayPack, StringComparison.Ordinal);
        Assert.Contains("## Cloud Configuration Still Required", replayPack, StringComparison.Ordinal);
        Assert.Contains("AZURE_AI_PROJECT_ENDPOINT", replayPack, StringComparison.Ordinal);
        Assert.Contains("AZURE_AI_MODEL_DEPLOYMENT_NAME", replayPack, StringComparison.Ordinal);
        Assert.Contains("AZURE_SUBSCRIPTION_ID", replayPack, StringComparison.Ordinal);
        Assert.Contains("AZURE_RESOURCE_GROUP", replayPack, StringComparison.Ordinal);
        Assert.Contains("AZURE_AI_PROJECT", replayPack, StringComparison.Ordinal);
    }

    private static AgenticEvalOfficialEvaluatorStackSnapshot BuildDeterministicSnapshot()
    {
        return AgenticEvalOfficialEvaluatorStack.BuildSnapshot(
            new AgenticEvalOfficialStackEnvironment(
                ProjectEndpoint: null,
                ModelDeploymentName: null,
                SubscriptionId: null,
                ResourceGroupName: null,
                ProjectName: null),
            evaluatedAtUtc: DateTimeOffset.Parse("2026-02-25T00:00:00+00:00"));
    }

    private static string ResolveRepoPath(params string[] relativeParts)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var solutionPath = Path.Combine(current.FullName, "Mosaic-Money.sln");
            if (File.Exists(solutionPath))
            {
                var parts = new List<string> { current.FullName };
                parts.AddRange(relativeParts);
                return Path.Combine(parts.ToArray());
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root from test base directory.");
    }

    private static string GetRequiredString(JsonElement root, string propertyName)
    {
        return root.GetProperty(propertyName).GetString()
            ?? throw new InvalidOperationException($"Property '{propertyName}' was null.");
    }

    private static IReadOnlyList<string> ReadRequiredStringArray(JsonElement root, string propertyName)
    {
        return root.GetProperty(propertyName)
            .EnumerateArray()
            .Select(static element => element.GetString() ?? string.Empty)
            .ToArray();
    }

    private static IReadOnlyList<AgenticEvalOfficialDatasetField> ReadDatasetSchema(
        JsonElement datasetSchema,
        string nameProperty,
        string typeProperty,
        string requiredProperty,
        string purposeProperty)
    {
        return datasetSchema
            .EnumerateArray()
            .Select(element => new AgenticEvalOfficialDatasetField(
                Name: element.GetProperty(nameProperty).GetString() ?? string.Empty,
                Type: element.GetProperty(typeProperty).GetString() ?? string.Empty,
                Required: element.GetProperty(requiredProperty).GetBoolean(),
                Purpose: element.GetProperty(purposeProperty).GetString() ?? string.Empty))
            .ToArray();
    }

    private static IReadOnlyList<AgenticEvalOfficialCriterionMapping> ReadCriteriaMappings(
        JsonElement criteriaMappings,
        string criterionNameProperty,
        string dotnetHintsProperty,
        string foundryEvaluatorsProperty,
        string foundryGradersProperty,
        string datasetMappingsProperty,
        string rationaleProperty)
    {
        return criteriaMappings
            .EnumerateArray()
            .Select(element => new AgenticEvalOfficialCriterionMapping(
                CriterionName: element.GetProperty(criterionNameProperty).GetString() ?? string.Empty,
                DotNetEvaluatorTypeHints: element.GetProperty(dotnetHintsProperty).EnumerateArray().Select(static x => x.GetString() ?? string.Empty).ToArray(),
                FoundryEvaluatorNames: element.GetProperty(foundryEvaluatorsProperty).EnumerateArray().Select(static x => x.GetString() ?? string.Empty).ToArray(),
                FoundryGraderNames: element.GetProperty(foundryGradersProperty).EnumerateArray().Select(static x => x.GetString() ?? string.Empty).ToArray(),
                DatasetFieldMappings: element.GetProperty(datasetMappingsProperty)
                    .EnumerateObject()
                    .ToDictionary(
                        static property => property.Name,
                        static property => property.Value.GetString() ?? string.Empty,
                        StringComparer.Ordinal),
                Rationale: element.GetProperty(rationaleProperty).GetString() ?? string.Empty))
            .ToArray();
    }

    private static void AssertDatasetSchemaEquals(
        IReadOnlyList<AgenticEvalOfficialDatasetField> expected,
        IReadOnlyList<AgenticEvalOfficialDatasetField> actual)
    {
        Assert.Equal(expected.Count, actual.Count);

        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Name, actual[i].Name);
            Assert.Equal(expected[i].Type, actual[i].Type);
            Assert.Equal(expected[i].Required, actual[i].Required);
            Assert.Equal(expected[i].Purpose, actual[i].Purpose);
        }
    }

    private static void AssertCriteriaMappingsEqual(
        IReadOnlyList<AgenticEvalOfficialCriterionMapping> expected,
        IReadOnlyList<AgenticEvalOfficialCriterionMapping> actual)
    {
        Assert.Equal(expected.Count, actual.Count);

        var actualByCriterion = actual.ToDictionary(
            static criterion => criterion.CriterionName,
            StringComparer.Ordinal);

        foreach (var expectedCriterion in expected)
        {
            Assert.True(
                actualByCriterion.TryGetValue(expectedCriterion.CriterionName, out var actualCriterion),
                $"Criterion '{expectedCriterion.CriterionName}' was missing from artifact.");

            Assert.Equal(expectedCriterion.DotNetEvaluatorTypeHints, actualCriterion!.DotNetEvaluatorTypeHints);
            Assert.Equal(expectedCriterion.FoundryEvaluatorNames, actualCriterion.FoundryEvaluatorNames);
            Assert.Equal(expectedCriterion.FoundryGraderNames, actualCriterion.FoundryGraderNames);
            Assert.Equal(expectedCriterion.Rationale, actualCriterion.Rationale);

            Assert.Equal(expectedCriterion.DatasetFieldMappings.Count, actualCriterion.DatasetFieldMappings.Count);
            foreach (var mapping in expectedCriterion.DatasetFieldMappings)
            {
                Assert.True(
                    actualCriterion.DatasetFieldMappings.TryGetValue(mapping.Key, out var actualMapping),
                    $"Dataset mapping '{mapping.Key}' missing for criterion '{expectedCriterion.CriterionName}'.");
                Assert.Equal(mapping.Value, actualMapping);
            }
        }
    }
}
