using System.Text.Json;

namespace MosaicMoney.Api.Tests;

internal static class AgenticEvalOfficialEvaluatorReplayPack
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static void WriteCompanionArtifacts(
        AgenticEvalOfficialEvaluatorStackSnapshot snapshot,
        string officialEvaluatorArtifactPath)
    {
        var outputDirectory = Path.GetDirectoryName(officialEvaluatorArtifactPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return;
        }

        Directory.CreateDirectory(outputDirectory);

        var mappingArtifactPath = Path.Combine(outputDirectory, "criteria-dataset-mapping.json");
        var replayPackPath = Path.Combine(outputDirectory, "replay-pack.md");

        File.WriteAllText(mappingArtifactPath, BuildCriteriaDatasetMappingJson(snapshot));
        File.WriteAllText(replayPackPath, BuildReplayPackMarkdown(snapshot));
    }

    private static string BuildCriteriaDatasetMappingJson(AgenticEvalOfficialEvaluatorStackSnapshot snapshot)
    {
        var payload = new
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
            criteriaMappings = snapshot.CriteriaMappings.Select(static mapping => new
            {
                criterion = mapping.CriterionName,
                dotnetEvaluatorTypeHints = mapping.DotNetEvaluatorTypeHints,
                foundryEvaluators = mapping.FoundryEvaluatorNames,
                foundryGraders = mapping.FoundryGraderNames,
                datasetFieldMappings = mapping.DatasetFieldMappings,
                rationale = mapping.Rationale,
            }),
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static string BuildReplayPackMarkdown(AgenticEvalOfficialEvaluatorStackSnapshot snapshot)
    {
        var lines = new List<string>
        {
            "# MM-AI-12 Official Evaluator Replay Pack",
            string.Empty,
            "## Offline Complete Today",
            "- Deterministic MM-AI-11 release-gate criteria remain authoritative and fail-closed.",
            "- Official evaluator stack references and mappings are captured in JSON artifacts for reproducible audit runs.",
            "- Replay command: `pwsh .github/scripts/run-mm-ai-11-release-gate.ps1`.",
            string.Empty,
            "## Cloud Configuration Still Required",
            "Cloud evaluator and grader execution evidence is optional for local reruns but requires all values below:",
            "- `AZURE_AI_PROJECT_ENDPOINT`",
            "- `AZURE_AI_MODEL_DEPLOYMENT_NAME`",
            "- `AZURE_SUBSCRIPTION_ID`",
            "- `AZURE_RESOURCE_GROUP`",
            "- `AZURE_AI_PROJECT`",
            string.Empty,
            "## Source Links",
        };

        foreach (var sourceLink in snapshot.SourceLinks)
        {
            lines.Add($"- {sourceLink}");
        }

        lines.Add(string.Empty);
        lines.Add("## Dataset Schema");
        foreach (var field in snapshot.DatasetSchema)
        {
            lines.Add($"- `{field.Name}` ({field.Type}, required={field.Required.ToString().ToLowerInvariant()}): {field.Purpose}");
        }

        lines.Add(string.Empty);
        lines.Add("## Criteria Coverage");
        foreach (var criterion in snapshot.CriteriaMappings)
        {
            lines.Add($"- `{criterion.CriterionName}`: {criterion.Rationale}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
