using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.Classification;

namespace MosaicMoney.Api.Tests;

internal sealed record AgenticEvalReleaseGateCriterion(
    string Name,
    decimal Score,
    decimal Threshold,
    int PassedChecks,
    int TotalChecks,
    bool Passed,
    string Evidence);

internal sealed record AgenticEvalReleaseGateReport(
    string GateId,
    IReadOnlyList<AgenticEvalReleaseGateCriterion> Criteria,
    bool IsReleaseReady,
    AgenticEvalOfficialEvaluatorStackSnapshot OfficialEvaluatorStack)
{
    public AgenticEvalReleaseGateCriterion GetCriterion(string criterionName)
    {
        var criterion = Criteria.FirstOrDefault(x =>
            string.Equals(x.Name, criterionName, StringComparison.Ordinal));

        if (criterion is null)
        {
            throw new InvalidOperationException($"Criterion '{criterionName}' was not found.");
        }

        return criterion;
    }

    public string ToFailureMessage()
    {
        var lines = new List<string>
        {
            $"{GateId} release gate failed.",
        };

        foreach (var criterion in Criteria)
        {
            lines.Add(
                $"- {criterion.Name}: score={criterion.Score:F4}, threshold={criterion.Threshold:F4}, checks={criterion.PassedChecks}/{criterion.TotalChecks}, evidence={criterion.Evidence}");
        }

        lines.Add(
            $"- official_evaluator_stack: dotnetLoaded={OfficialEvaluatorStack.ExecutionReadiness.DotNetEvaluatorTypesLoaded}, foundryConfigured={OfficialEvaluatorStack.ExecutionReadiness.FoundryCloudEvaluatorsConfigured}, failClosed={OfficialEvaluatorStack.ExecutionReadiness.FailClosedIfCloudUnavailable}");

        return string.Join(Environment.NewLine, lines);
    }
}

internal static class AgenticEvalReleaseGate
{
    public const string GateId = "MM-AI-11";

    public const string RoutingCorrectnessCriterionName = "routing_correctness";
    public const string AmbiguityFailClosedCriterionName = "ambiguity_fail_closed_to_needs_review";
    public const string ExternalMessagingHardStopCriterionName = "external_messaging_hard_stop";
    public const string AgentNoteExplainabilityCriterionName = "agentnote_explainability_summary_policy";

    private const decimal MinimumRoutingCorrectnessThreshold = 0.9500m;
    private const decimal RequiredAmbiguityFailClosedThreshold = 1.0000m;
    private const decimal RequiredExternalMessagingHardStopThreshold = 1.0000m;
    private const decimal MinimumAgentNoteExplainabilityThreshold = 0.9500m;

    public static async Task<AgenticEvalReleaseGateReport> EvaluateAsync(CancellationToken cancellationToken = default)
    {
        var routing = EvaluateRoutingCorrectness();
        var ambiguity = EvaluateAmbiguityFailClosedBehavior();
        var messaging = await EvaluateExternalMessagingHardStopBehaviorAsync(cancellationToken);
        var explainability = EvaluateAgentNoteExplainabilityBehavior();
        var officialEvaluatorStack = AgenticEvalOfficialEvaluatorStack.BuildSnapshot();

        var criteria = new[]
        {
            routing,
            ambiguity,
            messaging,
            explainability,
        };

        return new AgenticEvalReleaseGateReport(
            GateId,
            criteria,
            criteria.All(x => x.Passed),
            officialEvaluatorStack);
    }

    private static AgenticEvalReleaseGateCriterion EvaluateRoutingCorrectness()
    {
        var ambiguityGate = new ClassificationAmbiguityPolicyGate();
        var fusionPolicy = new ClassificationConfidenceFusionPolicy();

        var strongDeterministicSubcategoryId = Guid.NewGuid();
        var semanticSubcategoryId = Guid.NewGuid();
        var mismatchDeterministicSubcategoryId = Guid.NewGuid();
        var mismatchSemanticSubcategoryId = Guid.NewGuid();

        var cases = new[]
        {
            new RoutingCase(
                Name: "deterministic_high_confidence_accept",
                CurrentReviewStatus: TransactionReviewStatus.None,
                DeterministicResult: BuildDeterministicStageResult(
                    proposedSubcategoryId: strongDeterministicSubcategoryId,
                    confidence: 0.9400m,
                    rationaleCode: DeterministicClassificationReasonCodes.KeywordMatch,
                    hasConflict: false),
                SemanticResult: null,
                ExpectedDecision: ClassificationDecision.Categorized,
                ExpectedReviewStatus: TransactionReviewStatus.None,
                ExpectedReasonCode: ClassificationAmbiguityReasonCodes.DeterministicAccepted,
                ExpectedProposedSubcategoryId: strongDeterministicSubcategoryId),
            new RoutingCase(
                Name: "deterministic_low_confidence_needs_review",
                CurrentReviewStatus: TransactionReviewStatus.None,
                DeterministicResult: BuildDeterministicStageResult(
                    proposedSubcategoryId: Guid.NewGuid(),
                    confidence: 0.6200m,
                    rationaleCode: DeterministicClassificationReasonCodes.KeywordMatch,
                    hasConflict: false),
                SemanticResult: null,
                ExpectedDecision: ClassificationDecision.NeedsReview,
                ExpectedReviewStatus: TransactionReviewStatus.NeedsReview,
                ExpectedReasonCode: ClassificationAmbiguityReasonCodes.LowConfidence,
                ExpectedProposedSubcategoryId: null),
            new RoutingCase(
                Name: "semantic_fallback_threshold_pass",
                CurrentReviewStatus: TransactionReviewStatus.None,
                DeterministicResult: BuildDeterministicStageResult(
                    proposedSubcategoryId: null,
                    confidence: 0m,
                    rationaleCode: DeterministicClassificationReasonCodes.NoRuleMatch,
                    hasConflict: false),
                SemanticResult: new SemanticRetrievalResult(
                    Succeeded: true,
                    StatusCode: SemanticRetrievalStatusCodes.Ok,
                    StatusMessage: "Semantic retrieval succeeded.",
                    Candidates:
                    [
                        BuildSemanticCandidate(semanticSubcategoryId, 0.9500m),
                        BuildSemanticCandidate(Guid.NewGuid(), 0.8900m),
                    ]),
                ExpectedDecision: ClassificationDecision.Categorized,
                ExpectedReviewStatus: TransactionReviewStatus.None,
                ExpectedReasonCode: ClassificationConfidenceFusionReasonCodes.SemanticFallbackAccepted,
                ExpectedProposedSubcategoryId: semanticSubcategoryId),
            new RoutingCase(
                Name: "semantic_fallback_below_threshold",
                CurrentReviewStatus: TransactionReviewStatus.None,
                DeterministicResult: BuildDeterministicStageResult(
                    proposedSubcategoryId: null,
                    confidence: 0m,
                    rationaleCode: DeterministicClassificationReasonCodes.NoRuleMatch,
                    hasConflict: false),
                SemanticResult: new SemanticRetrievalResult(
                    Succeeded: true,
                    StatusCode: SemanticRetrievalStatusCodes.Ok,
                    StatusMessage: "Semantic retrieval succeeded.",
                    Candidates:
                    [
                        BuildSemanticCandidate(Guid.NewGuid(), 0.9100m),
                    ]),
                ExpectedDecision: ClassificationDecision.NeedsReview,
                ExpectedReviewStatus: TransactionReviewStatus.NeedsReview,
                ExpectedReasonCode: ClassificationConfidenceFusionReasonCodes.SemanticBelowThreshold,
                ExpectedProposedSubcategoryId: null),
            new RoutingCase(
                Name: "deterministic_semantic_conflict_needs_review",
                CurrentReviewStatus: TransactionReviewStatus.None,
                DeterministicResult: BuildDeterministicStageResult(
                    proposedSubcategoryId: mismatchDeterministicSubcategoryId,
                    confidence: 0.6300m,
                    rationaleCode: DeterministicClassificationReasonCodes.KeywordMatch,
                    hasConflict: false),
                SemanticResult: new SemanticRetrievalResult(
                    Succeeded: true,
                    StatusCode: SemanticRetrievalStatusCodes.Ok,
                    StatusMessage: "Semantic retrieval succeeded.",
                    Candidates:
                    [
                        BuildSemanticCandidate(mismatchSemanticSubcategoryId, 0.9700m),
                    ]),
                ExpectedDecision: ClassificationDecision.NeedsReview,
                ExpectedReviewStatus: TransactionReviewStatus.NeedsReview,
                ExpectedReasonCode: ClassificationConfidenceFusionReasonCodes.DeterministicSemanticConflict,
                ExpectedProposedSubcategoryId: null),
        };

        var passedChecks = 0;
        foreach (var scenario in cases)
        {
            var ambiguityDecision = ambiguityGate.Evaluate(scenario.CurrentReviewStatus, scenario.DeterministicResult);
            var fusionDecision = fusionPolicy.Evaluate(
                scenario.CurrentReviewStatus,
                scenario.DeterministicResult,
                ambiguityDecision,
                scenario.SemanticResult);

            if (fusionDecision.Decision == scenario.ExpectedDecision
                && fusionDecision.ReviewStatus == scenario.ExpectedReviewStatus
                && string.Equals(fusionDecision.DecisionReasonCode, scenario.ExpectedReasonCode, StringComparison.Ordinal)
                && fusionDecision.ProposedSubcategoryId == scenario.ExpectedProposedSubcategoryId)
            {
                passedChecks++;
            }
        }

        var score = ComputeScore(passedChecks, cases.Length);
        return BuildCriterion(
            RoutingCorrectnessCriterionName,
            score,
            MinimumRoutingCorrectnessThreshold,
            passedChecks,
            cases.Length,
            evidence: "Stage policy routing matched labeled expected outcomes.");
    }

    private static AgenticEvalReleaseGateCriterion EvaluateAmbiguityFailClosedBehavior()
    {
        var gate = new ClassificationAmbiguityPolicyGate();
        var deterministicSubcategoryId = Guid.NewGuid();

        var cases = new[]
        {
            new AmbiguityCase(
                CurrentReviewStatus: TransactionReviewStatus.NeedsReview,
                DeterministicResult: BuildDeterministicStageResult(
                    proposedSubcategoryId: deterministicSubcategoryId,
                    confidence: 0.9700m,
                    rationaleCode: DeterministicClassificationReasonCodes.KeywordMatch,
                    hasConflict: false),
                ExpectedReasonCode: ClassificationAmbiguityReasonCodes.ExistingNeedsReviewState),
            new AmbiguityCase(
                CurrentReviewStatus: TransactionReviewStatus.None,
                DeterministicResult: BuildDeterministicStageResult(
                    proposedSubcategoryId: null,
                    confidence: 0.9100m,
                    rationaleCode: DeterministicClassificationReasonCodes.ConflictingRules,
                    hasConflict: true),
                ExpectedReasonCode: ClassificationAmbiguityReasonCodes.ConflictingDeterministicRules),
            new AmbiguityCase(
                CurrentReviewStatus: TransactionReviewStatus.None,
                DeterministicResult: BuildDeterministicStageResult(
                    proposedSubcategoryId: null,
                    confidence: 0m,
                    rationaleCode: DeterministicClassificationReasonCodes.NoRuleMatch,
                    hasConflict: false),
                ExpectedReasonCode: ClassificationAmbiguityReasonCodes.NoDeterministicMatch),
            new AmbiguityCase(
                CurrentReviewStatus: TransactionReviewStatus.None,
                DeterministicResult: BuildDeterministicStageResult(
                    proposedSubcategoryId: deterministicSubcategoryId,
                    confidence: 0.8400m,
                    rationaleCode: DeterministicClassificationReasonCodes.KeywordMatch,
                    hasConflict: false),
                ExpectedReasonCode: ClassificationAmbiguityReasonCodes.LowConfidence),
        };

        var passedChecks = 0;
        foreach (var scenario in cases)
        {
            var decision = gate.Evaluate(scenario.CurrentReviewStatus, scenario.DeterministicResult);
            if (decision.Decision == ClassificationDecision.NeedsReview
                && decision.ReviewStatus == TransactionReviewStatus.NeedsReview
                && string.Equals(decision.DecisionReasonCode, scenario.ExpectedReasonCode, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(decision.DecisionRationale)
                && !string.IsNullOrWhiteSpace(decision.AgentNoteSummary))
            {
                passedChecks++;
            }
        }

        var score = ComputeScore(passedChecks, cases.Length);
        return BuildCriterion(
            AmbiguityFailClosedCriterionName,
            score,
            RequiredAmbiguityFailClosedThreshold,
            passedChecks,
            cases.Length,
            evidence: "Ambiguity policy routed all low-confidence/conflict cases to NeedsReview.");
    }

    private static async Task<AgenticEvalReleaseGateCriterion> EvaluateExternalMessagingHardStopBehaviorAsync(CancellationToken cancellationToken)
    {
        var sendActions = new[]
        {
            "send_message",
            "send_email_receipt",
            "send_whatsapp",
            "notify_external_system",
        };

        var deniedChecks = 0;
        foreach (var action in sendActions)
        {
            var result = await ExecuteMafGraphActionAsync(action, cancellationToken);
            if (result.Succeeded
                && string.Equals(result.StatusCode, MafFallbackGraphStatusCodes.ExternalMessagingSendDenied, StringComparison.Ordinal)
                && result.MessagingSendDenied
                && result.MessagingSendDeniedCount >= 1
                && result.Proposals.Count == 0)
            {
                deniedChecks++;
            }
        }

        var draftOnlyResult = await ExecuteMafGraphActionAsync("draft_message", cancellationToken);
        var draftOnlyRemainsAllowed = draftOnlyResult.Succeeded
            && string.Equals(draftOnlyResult.StatusCode, MafFallbackGraphStatusCodes.Ok, StringComparison.Ordinal)
            && !draftOnlyResult.MessagingSendDenied
            && draftOnlyResult.Proposals.Count == 1;

        var score = ComputeScore(deniedChecks, sendActions.Length);
        var criterion = BuildCriterion(
            ExternalMessagingHardStopCriterionName,
            score,
            RequiredExternalMessagingHardStopThreshold,
            deniedChecks,
            sendActions.Length,
            evidence: $"Denied outbound send actions and preserved draft-only path (draftAllowed={draftOnlyRemainsAllowed}).");

        return draftOnlyRemainsAllowed
            ? criterion
            : criterion with { Passed = false };
    }

    private static AgenticEvalReleaseGateCriterion EvaluateAgentNoteExplainabilityBehavior()
    {
        var cases = new[]
        {
            new AgentNoteCase(
                RawSummary: " Candidate routed to NeedsReview   due to confidence gap. ",
                IsCompliant: value => string.Equals(
                    value,
                    "Candidate routed to NeedsReview due to confidence gap.",
                    StringComparison.Ordinal)),
            new AgentNoteCase(
                RawSummary: "User: classify this transaction. Assistant: reviewing evidence. Tool output: {\"tool\":\"semantic\",\"result\":\"...\"}",
                IsCompliant: value => string.Equals(value, AgentNoteSummaryPolicy.SuppressedSummary, StringComparison.Ordinal)),
            new AgentNoteCase(
                RawSummary: "```json\n{\"tool\":\"search\",\"result\":\"...\"}\n```",
                IsCompliant: value => string.Equals(value, AgentNoteSummaryPolicy.SuppressedSummary, StringComparison.Ordinal)),
            new AgentNoteCase(
                RawSummary: new string('a', AgentNoteSummaryPolicy.MaxPersistedSummaryLength + 37),
                IsCompliant: value => value is not null && value.Length == AgentNoteSummaryPolicy.MaxPersistedSummaryLength),
            new AgentNoteCase(
                RawSummary: "Semantic fallback proposed a review candidate with confidence 0.9321 and explicit rationale code.",
                IsCompliant: value =>
                    !string.IsNullOrWhiteSpace(value)
                    && value.Length <= AgentNoteSummaryPolicy.MaxPersistedSummaryLength
                    && !string.Equals(value, AgentNoteSummaryPolicy.SuppressedSummary, StringComparison.Ordinal)),
        };

        var passedChecks = 0;
        foreach (var scenario in cases)
        {
            var sanitized = AgentNoteSummaryPolicy.Sanitize(scenario.RawSummary);
            if (scenario.IsCompliant(sanitized))
            {
                passedChecks++;
            }
        }

        var score = ComputeScore(passedChecks, cases.Length);
        return BuildCriterion(
            AgentNoteExplainabilityCriterionName,
            score,
            MinimumAgentNoteExplainabilityThreshold,
            passedChecks,
            cases.Length,
            evidence: "AgentNote summaries remained concise, bounded, and transcript-safe.");
    }

    private static async Task<MafFallbackGraphResult> ExecuteMafGraphActionAsync(
        string proposedAction,
        CancellationToken cancellationToken)
    {
        var allowedSubcategoryId = Guid.NewGuid();
        var payload = $$"""
        {
          "proposals": [
            {
              "proposedSubcategoryId": "{{allowedSubcategoryId}}",
              "confidence": 0.9400,
              "rationaleCode": "mm_ai_11_case",
              "rationale": "Release gate scenario proposal.",
              "agentNoteSummary": "Concise release gate summary.",
              "proposedAction": "{{proposedAction}}",
              "proposedExternalMessageDraft": "Draft content for review only."
            }
          ]
        }
        """;

        var executor = new StubMafFallbackGraphExecutor(payload);
        var service = new MafFallbackGraphService(
            executor,
            Options.Create(new MafFallbackGraphOptions
            {
                Enabled = true,
                TimeoutSeconds = 8,
                MaxProposals = 3,
                MinimumProposalConfidence = 0.7000m,
            }),
            NullLogger<MafFallbackGraphService>.Instance);

        return await service.ExecuteAsync(BuildMafRequest(allowedSubcategoryId), cancellationToken);
    }

    private static MafFallbackGraphRequest BuildMafRequest(Guid allowedSubcategoryId)
    {
        return new MafFallbackGraphRequest(
            TransactionId: Guid.NewGuid(),
            Description: "Unknown merchant",
            Amount: -24.20m,
            TransactionDate: new DateOnly(2026, 2, 24),
            AllowedSubcategories:
            [
                new DeterministicClassificationSubcategory(allowedSubcategoryId, "utilities")
            ],
            DeterministicResult: BuildDeterministicStageResult(
                proposedSubcategoryId: null,
                confidence: 0m,
                rationaleCode: DeterministicClassificationReasonCodes.NoRuleMatch,
                hasConflict: false),
            SemanticResult: new SemanticRetrievalResult(
                Succeeded: true,
                StatusCode: SemanticRetrievalStatusCodes.NoCandidates,
                StatusMessage: "No semantic candidates met threshold.",
                Candidates: []),
            FusionDecision: new ClassificationConfidenceFusionDecision(
                Decision: ClassificationDecision.NeedsReview,
                ReviewStatus: TransactionReviewStatus.NeedsReview,
                ProposedSubcategoryId: null,
                FinalConfidence: 0m,
                DecisionReasonCode: ClassificationAmbiguityReasonCodes.NoDeterministicMatch,
                DecisionRationale: "Deterministic and semantic stages were insufficient.",
                AgentNoteSummary: null,
                EscalatedToNextStage: true));
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
            "Release gate deterministic scenario rationale.",
            hasConflict,
            []);
    }

    private static SemanticRetrievalCandidate BuildSemanticCandidate(Guid subcategoryId, decimal score)
    {
        return new SemanticRetrievalCandidate(
            ProposedSubcategoryId: subcategoryId,
            NormalizedScore: score,
            SourceTransactionId: Guid.NewGuid(),
            SupportingMatchCount: 1,
            ProvenanceSource: "postgresql.pgvector.cosine_distance",
            ProvenanceReference: "mm-ai-11",
            ProvenancePayloadJson: "{}");
    }

    private static decimal ComputeScore(int passedChecks, int totalChecks)
    {
        if (totalChecks <= 0)
        {
            return 0m;
        }

        return decimal.Round((decimal)passedChecks / totalChecks, 4, MidpointRounding.AwayFromZero);
    }

    private static AgenticEvalReleaseGateCriterion BuildCriterion(
        string name,
        decimal score,
        decimal threshold,
        int passedChecks,
        int totalChecks,
        string evidence)
    {
        return new AgenticEvalReleaseGateCriterion(
            name,
            score,
            threshold,
            passedChecks,
            totalChecks,
            Passed: score >= threshold,
            evidence);
    }

    private sealed record RoutingCase(
        string Name,
        TransactionReviewStatus CurrentReviewStatus,
        DeterministicClassificationStageResult DeterministicResult,
        SemanticRetrievalResult? SemanticResult,
        ClassificationDecision ExpectedDecision,
        TransactionReviewStatus ExpectedReviewStatus,
        string ExpectedReasonCode,
        Guid? ExpectedProposedSubcategoryId);

    private sealed record AmbiguityCase(
        TransactionReviewStatus CurrentReviewStatus,
        DeterministicClassificationStageResult DeterministicResult,
        string ExpectedReasonCode);

    private sealed record AgentNoteCase(
        string RawSummary,
        Func<string?, bool> IsCompliant);

    private sealed class StubMafFallbackGraphExecutor(string payload) : IMafFallbackGraphExecutor
    {
        public Task<string> ExecuteAsync(MafFallbackGraphRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(payload);
        }
    }
}
