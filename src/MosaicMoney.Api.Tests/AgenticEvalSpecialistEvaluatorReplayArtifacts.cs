using System.Text.Json;

namespace MosaicMoney.Api.Tests;

internal static class AgenticEvalSpecialistEvaluatorReplayArtifacts
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static void WriteArtifacts(
        AgenticEvalSpecialistEvaluatorPackSnapshot snapshot,
        string latestArtifactPath)
    {
        var outputDirectory = Path.GetDirectoryName(latestArtifactPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return;
        }

        Directory.CreateDirectory(outputDirectory);

        var summaryPayload = new
        {
            gateId = AgenticEvalReleaseGate.GateId,
            profileId = snapshot.ProfileId,
            evaluatedAtUtc = snapshot.EvaluatedAtUtc,
            laneCount = snapshot.LanePacks.Count,
            lanes = snapshot.LanePacks.Select(static lane => new
            {
                specialistKey = lane.SpecialistKey,
                specialistId = lane.SpecialistId,
                allowSemanticStage = lane.AllowSemanticStage,
                allowMafFallbackStage = lane.AllowMafFallbackStage,
                thresholdCount = lane.Thresholds.Count,
                sampleCount = lane.Samples.Count,
            }),
            notes = snapshot.Notes,
        };

        File.WriteAllText(latestArtifactPath, JsonSerializer.Serialize(summaryPayload, JsonOptions));

        var datasetsPath = Path.Combine(outputDirectory, "specialist-lane-datasets.json");
        var datasetPayload = new
        {
            profileId = snapshot.ProfileId,
            sourceLinks = snapshot.SourceLinks,
            datasetSchema = snapshot.DatasetSchema.Select(static field => new
            {
                name = field.Name,
                type = field.Type,
                required = field.Required,
                purpose = field.Purpose,
            }),
            lanePacks = snapshot.LanePacks.Select(static lane => new
            {
                specialistKey = lane.SpecialistKey,
                specialistId = lane.SpecialistId,
                allowSemanticStage = lane.AllowSemanticStage,
                allowMafFallbackStage = lane.AllowMafFallbackStage,
                objective = lane.Objective,
                thresholds = lane.Thresholds.Select(static threshold => new
                {
                    criterion = threshold.Criterion,
                    minimumScore = threshold.MinimumScore,
                    requiresPerfectCompliance = threshold.RequiresPerfectCompliance,
                }),
                samples = lane.Samples.Select(static sample => new
                {
                    sampleId = sample.SampleId,
                    query = sample.Query,
                    groundTruth = sample.GroundTruth,
                    expectedActions = sample.ExpectedActions,
                    rationale = sample.Rationale,
                }),
            }),
        };

        File.WriteAllText(datasetsPath, JsonSerializer.Serialize(datasetPayload, JsonOptions));

        var replayPackPath = Path.Combine(outputDirectory, "replay-pack.md");
        File.WriteAllText(replayPackPath, BuildReplayPackMarkdown(snapshot));
    }

    private static string BuildReplayPackMarkdown(AgenticEvalSpecialistEvaluatorPackSnapshot snapshot)
    {
        var lines = new List<string>
        {
            "# MM-AI-15 Specialist Evaluator Replay Pack",
            string.Empty,
            "## Replay Command",
            "- `pwsh .github/scripts/run-mm-ai-11-release-gate.ps1`",
            string.Empty,
            "## Specialist Lanes",
        };

        foreach (var lane in snapshot.LanePacks)
        {
            lines.Add($"### {lane.SpecialistKey}");
            lines.Add($"- SpecialistId: `{lane.SpecialistId}`");
            lines.Add($"- AllowSemanticStage: `{lane.AllowSemanticStage}`");
            lines.Add($"- AllowMafFallbackStage: `{lane.AllowMafFallbackStage}`");
            lines.Add($"- Objective: {lane.Objective}");
            lines.Add("- Thresholds:");
            foreach (var threshold in lane.Thresholds)
            {
                lines.Add(
                    $"  - `{threshold.Criterion}` >= {threshold.MinimumScore:F4} (perfect={threshold.RequiresPerfectCompliance.ToString().ToLowerInvariant()})");
            }

            lines.Add("- Samples:");
            foreach (var sample in lane.Samples)
            {
                lines.Add($"  - `{sample.SampleId}`: {sample.Query}");
            }

            lines.Add(string.Empty);
        }

        lines.Add("## Source Links");
        foreach (var sourceLink in snapshot.SourceLinks)
        {
            lines.Add($"- {sourceLink}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
