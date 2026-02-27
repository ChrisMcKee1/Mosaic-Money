using System.Text.Json;
using MosaicMoney.Api.Domain.Ledger.Classification;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class AgenticEvalSpecialistEvaluatorArtifactsTests
{
    [Fact]
    public void LatestReplayJson_ContainsAllSpecialistLanes()
    {
        var snapshot = BuildDeterministicSnapshot();
        var artifactPath = ResolveRepoPath("artifacts", "release-gates", "mm-ai-15", "latest.json");

        using var document = JsonDocument.Parse(File.ReadAllText(artifactPath));
        var root = document.RootElement;

        Assert.Equal(AgenticEvalReleaseGate.GateId, GetRequiredString(root, "gateId"));
        Assert.Equal(snapshot.ProfileId, GetRequiredString(root, "profileId"));
        Assert.Equal(snapshot.LanePacks.Count, root.GetProperty("laneCount").GetInt32());

        var lanes = root.GetProperty("lanes")
            .EnumerateArray()
            .Select(static lane => lane.GetProperty("specialistKey").GetString() ?? string.Empty)
            .ToArray();

        Assert.Equal(ClassificationSpecialistKeys.All, lanes);
    }

    [Fact]
    public void SpecialistLaneDatasetsJson_MatchesSnapshotShape()
    {
        var snapshot = BuildDeterministicSnapshot();
        var artifactPath = ResolveRepoPath("artifacts", "release-gates", "mm-ai-15", "specialist-lane-datasets.json");

        using var document = JsonDocument.Parse(File.ReadAllText(artifactPath));
        var root = document.RootElement;

        Assert.Equal(snapshot.ProfileId, GetRequiredString(root, "profileId"));

        var lanePacks = root.GetProperty("lanePacks").EnumerateArray().ToArray();
        Assert.Equal(snapshot.LanePacks.Count, lanePacks.Length);

        var laneByKey = lanePacks.ToDictionary(
            static lane => lane.GetProperty("specialistKey").GetString() ?? string.Empty,
            StringComparer.Ordinal);

        foreach (var expectedLane in snapshot.LanePacks)
        {
            Assert.True(laneByKey.TryGetValue(expectedLane.SpecialistKey, out var actualLane));
            Assert.Equal(expectedLane.SpecialistId, GetRequiredString(actualLane, "specialistId"));
            Assert.Equal(expectedLane.AllowSemanticStage, actualLane.GetProperty("allowSemanticStage").GetBoolean());
            Assert.Equal(expectedLane.AllowMafFallbackStage, actualLane.GetProperty("allowMafFallbackStage").GetBoolean());
            Assert.Equal(expectedLane.Thresholds.Count, actualLane.GetProperty("thresholds").GetArrayLength());
            Assert.Equal(expectedLane.Samples.Count, actualLane.GetProperty("samples").GetArrayLength());
        }
    }

    [Fact]
    public void ReplayPack_IncludesAllSpecialistLaneSections()
    {
        var replayPackPath = ResolveRepoPath("artifacts", "release-gates", "mm-ai-15", "replay-pack.md");
        var replayPack = File.ReadAllText(replayPackPath);

        Assert.Contains("## Specialist Lanes", replayPack, StringComparison.Ordinal);
        Assert.Contains("## Source Links", replayPack, StringComparison.Ordinal);

        foreach (var laneKey in ClassificationSpecialistKeys.All)
        {
            Assert.Contains($"### {laneKey}", replayPack, StringComparison.Ordinal);
        }
    }

    private static AgenticEvalSpecialistEvaluatorPackSnapshot BuildDeterministicSnapshot()
    {
        return AgenticEvalSpecialistEvaluatorPack.BuildSnapshot(
            evaluatedAtUtc: DateTimeOffset.Parse("2026-02-27T00:00:00+00:00"));
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
}
