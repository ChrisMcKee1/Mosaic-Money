using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using Microsoft.Extensions.AI.Evaluation.Safety;

namespace MosaicMoney.Api.Tests;

internal sealed record AgenticEvalOfficialStackEnvironment(
    string? ProjectEndpoint,
    string? ModelDeploymentName,
    string? SubscriptionId,
    string? ResourceGroupName,
    string? ProjectName)
{
    public static AgenticEvalOfficialStackEnvironment FromProcessEnvironment()
    {
        return new AgenticEvalOfficialStackEnvironment(
            Environment.GetEnvironmentVariable("AZURE_AI_PROJECT_ENDPOINT"),
            Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME"),
            Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID"),
            Environment.GetEnvironmentVariable("AZURE_RESOURCE_GROUP"),
            Environment.GetEnvironmentVariable("AZURE_AI_PROJECT"));
    }

    public bool IsCloudEvaluationConfigured()
    {
        return !string.IsNullOrWhiteSpace(ProjectEndpoint)
            && !string.IsNullOrWhiteSpace(ModelDeploymentName)
            && !string.IsNullOrWhiteSpace(SubscriptionId)
            && !string.IsNullOrWhiteSpace(ResourceGroupName)
            && !string.IsNullOrWhiteSpace(ProjectName);
    }
}

internal sealed record AgenticEvalOfficialExecutionReadiness(
    bool DotNetEvaluatorTypesLoaded,
    bool FoundryCloudEvaluatorsConfigured,
    bool FailClosedIfCloudUnavailable,
    string ReadinessNote);

internal sealed record AgenticEvalOfficialDatasetField(
    string Name,
    string Type,
    bool Required,
    string Purpose);

internal sealed record AgenticEvalOfficialCriterionMapping(
    string CriterionName,
    IReadOnlyList<string> DotNetEvaluatorTypeHints,
    IReadOnlyList<string> FoundryEvaluatorNames,
    IReadOnlyList<string> FoundryGraderNames,
    IReadOnlyDictionary<string, string> DatasetFieldMappings,
    string Rationale);

internal sealed record AgenticEvalOfficialEvaluatorStackSnapshot(
    string ProfileId,
    DateTimeOffset EvaluatedAtUtc,
    IReadOnlyList<string> SourceLinks,
    IReadOnlyList<string> DotNetPackageReferences,
    IReadOnlyList<string> DotNetComponentTypeAnchors,
    IReadOnlyList<AgenticEvalOfficialDatasetField> DatasetSchema,
    IReadOnlyList<AgenticEvalOfficialCriterionMapping> CriteriaMappings,
    AgenticEvalOfficialExecutionReadiness ExecutionReadiness,
    string Notes);

internal static class AgenticEvalOfficialEvaluatorStack
{
    private const string ProfileId = "MM-AI-12-official-evaluator-stack";

    public static AgenticEvalOfficialEvaluatorStackSnapshot BuildSnapshot(
        AgenticEvalOfficialStackEnvironment? environment = null,
        DateTimeOffset? evaluatedAtUtc = null)
    {
        environment ??= AgenticEvalOfficialStackEnvironment.FromProcessEnvironment();
        var timestamp = evaluatedAtUtc ?? DateTimeOffset.UtcNow;

        var representativeEvaluators = CreateRepresentativeDotNetEvaluators();
        var evaluatorTypeNames = representativeEvaluators
            .Select(static evaluator => evaluator.GetType().Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        var reportingAnchor = typeof(ReportingConfiguration).FullName
            ?? "Microsoft.Extensions.AI.Evaluation.Reporting.ReportingConfiguration";

        var cloudConfigured = environment.IsCloudEvaluationConfigured();
        var readiness = new AgenticEvalOfficialExecutionReadiness(
            DotNetEvaluatorTypesLoaded: evaluatorTypeNames.Length >= 4,
            FoundryCloudEvaluatorsConfigured: cloudConfigured,
            FailClosedIfCloudUnavailable: true,
            ReadinessNote: cloudConfigured
                ? "Cloud evaluator prerequisites are configured; Foundry evaluator and grader runs can be added without changing gate semantics."
                : "Cloud evaluator prerequisites are unavailable; gate remains fail-closed to deterministic MM-AI-11 checks while preserving mapping evidence.");

        return new AgenticEvalOfficialEvaluatorStackSnapshot(
            ProfileId,
            timestamp,
            BuildSourceLinks(),
            BuildDotNetPackageReferences(),
            [.. evaluatorTypeNames, reportingAnchor],
            BuildDatasetSchema(),
            BuildCriterionMappings(),
            readiness,
            "MM-AI-12 integration maps MM-AI-11 criteria to official .NET evaluators and Azure AI Foundry evaluators/graders while preserving deterministic release-blocking behavior.");
    }

    private static IReadOnlyList<IEvaluator> CreateRepresentativeDotNetEvaluators()
    {
        return
        [
            new CoherenceEvaluator(),
            new RelevanceEvaluator(),
            new FluencyEvaluator(),
            new ViolenceEvaluator(),
        ];
    }

    private static IReadOnlyList<string> BuildSourceLinks()
    {
        return
        [
            "https://learn.microsoft.com/en-us/dotnet/ai/evaluation/libraries",
            "https://learn.microsoft.com/en-us/dotnet/ai/evaluation/evaluate-with-reporting",
            "https://learn.microsoft.com/en-us/dotnet/ai/evaluation/evaluate-safety",
            "https://learn.microsoft.com/en-us/azure/ai-foundry/concepts/built-in-evaluators?view=foundry-classic",
            "https://learn.microsoft.com/en-us/azure/ai-foundry/concepts/evaluation-evaluators/agent-evaluators?view=foundry-classic",
            "https://learn.microsoft.com/en-us/azure/ai-foundry/concepts/evaluation-evaluators/azure-openai-graders?view=foundry-classic",
            "https://learn.microsoft.com/en-us/azure/ai-foundry/how-to/develop/cloud-evaluation?view=foundry-classic",
            "https://learn.microsoft.com/en-us/azure/ai-foundry/how-to/develop/evaluate-sdk?view=foundry-classic",
            "https://learn.microsoft.com/en-us/azure/ai-foundry/tutorials/developer-journey-idea-to-prototype?view=foundry-classic#step-4-evaluate-the-assistant-by-using-cloud-evaluation",
        ];
    }

    private static IReadOnlyList<string> BuildDotNetPackageReferences()
    {
        return
        [
            "Microsoft.Extensions.AI.Evaluation",
            "Microsoft.Extensions.AI.Evaluation.Quality",
            "Microsoft.Extensions.AI.Evaluation.Safety",
            "Microsoft.Extensions.AI.Evaluation.Reporting",
        ];
    }

    private static IReadOnlyList<AgenticEvalOfficialDatasetField> BuildDatasetSchema()
    {
        return
        [
            new AgenticEvalOfficialDatasetField("query", "string", true, "User prompt or classification request text."),
            new AgenticEvalOfficialDatasetField("response", "string", true, "Model or policy response under evaluation."),
            new AgenticEvalOfficialDatasetField("tool_definitions", "array<object>", true, "Declared tool inventory used for evaluator context."),
            new AgenticEvalOfficialDatasetField("actions", "array<string>", true, "Proposed action sequence produced by workflow stages."),
            new AgenticEvalOfficialDatasetField("expected_actions", "array<string>", true, "Human-reviewed expected safe action sequence."),
            new AgenticEvalOfficialDatasetField("ground_truth", "string", true, "Reference expectation for deterministic and grader comparisons."),
        ];
    }

    private static IReadOnlyList<AgenticEvalOfficialCriterionMapping> BuildCriterionMappings()
    {
        return
        [
            new AgenticEvalOfficialCriterionMapping(
                AgenticEvalReleaseGate.RoutingCorrectnessCriterionName,
                DotNetEvaluatorTypeHints:
                [
                    nameof(RelevanceEvaluator),
                    nameof(CoherenceEvaluator),
                    "TaskAdherenceEvaluator",
                ],
                FoundryEvaluatorNames:
                [
                    "builtin.coherence",
                    "builtin.task_adherence",
                    "builtin.intent_resolution",
                ],
                FoundryGraderNames:
                [
                    "AzureOpenAITextSimilarityGrader",
                    "AzureOpenAILabelGrader",
                ],
                DatasetFieldMappings: BuildMapping(
                    ("query", "item.query"),
                    ("response", "sample.output_text"),
                    ("ground_truth", "item.ground_truth"),
                    ("expected_actions", "item.expected_actions")),
                Rationale: "Cross-check stage routing and final labels against expected outcomes while keeping deterministic policy authoritative."),
            new AgenticEvalOfficialCriterionMapping(
                AgenticEvalReleaseGate.AmbiguityFailClosedCriterionName,
                DotNetEvaluatorTypeHints:
                [
                    "TaskAdherenceEvaluator",
                    "CustomNeedsReviewFailClosedEvaluator",
                ],
                FoundryEvaluatorNames:
                [
                    "builtin.task_adherence",
                    "builtin.intent_resolution",
                ],
                FoundryGraderNames:
                [
                    "AzureOpenAIStringCheckGrader",
                    "AzureOpenAILabelGrader",
                ],
                DatasetFieldMappings: BuildMapping(
                    ("query", "item.query"),
                    ("response", "sample.output_text"),
                    ("expected_actions", "item.expected_actions"),
                    ("ground_truth", "item.ground_truth")),
                Rationale: "Ensure low-confidence or conflicting outcomes remain routed to NeedsReview with explicit rationale."),
            new AgenticEvalOfficialCriterionMapping(
                AgenticEvalReleaseGate.ExternalMessagingHardStopCriterionName,
                DotNetEvaluatorTypeHints:
                [
                    nameof(ViolenceEvaluator),
                    "CustomExternalMessagingDenyEvaluator",
                ],
                FoundryEvaluatorNames:
                [
                    "builtin.task_adherence",
                    "builtin.tool_call_accuracy",
                    "builtin.violence",
                ],
                FoundryGraderNames:
                [
                    "AzureOpenAIStringCheckGrader",
                    "AzureOpenAILabelGrader",
                ],
                DatasetFieldMappings: BuildMapping(
                    ("query", "item.query"),
                    ("response", "sample.output_items"),
                    ("actions", "sample.output_items.actions"),
                    ("expected_actions", "item.expected_actions")),
                Rationale: "Verify outbound send actions are denied while preserving draft-only responses for human approval."),
            new AgenticEvalOfficialCriterionMapping(
                AgenticEvalReleaseGate.AgentNoteExplainabilityCriterionName,
                DotNetEvaluatorTypeHints:
                [
                    nameof(CoherenceEvaluator),
                    nameof(RelevanceEvaluator),
                    "CustomAgentNoteSummaryPolicyEvaluator",
                ],
                FoundryEvaluatorNames:
                [
                    "builtin.coherence",
                    "builtin.fluency",
                    "builtin.task_adherence",
                ],
                FoundryGraderNames:
                [
                    "AzureOpenAITextSimilarityGrader",
                    "AzureOpenAIGrader",
                ],
                DatasetFieldMappings: BuildMapping(
                    ("query", "item.query"),
                    ("response", "sample.output_text"),
                    ("ground_truth", "item.ground_truth")),
                Rationale: "Preserve concise and transcript-safe AgentNote summaries with explainable justifications."),
        ];
    }

    private static IReadOnlyDictionary<string, string> BuildMapping(params (string Field, string Mapping)[] values)
    {
        return values.ToDictionary(
            static pair => pair.Field,
            static pair => pair.Mapping,
            StringComparer.Ordinal);
    }
}