using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace MosaicMoney.Api.Domain.Ledger.Classification;

public static class MafFallbackGraphStatusCodes
{
    public const string Ok = "ok";
    public const string NoProposals = "no_proposals";
    public const string ExternalMessagingSendDenied = "external_messaging_send_denied";
    public const string Disabled = "disabled";
    public const string InvalidRequest = "invalid_request";
    public const string Timeout = "timeout";
    public const string SchemaValidationFailed = "schema_validation_failed";
    public const string ExecutionFailed = "execution_failed";
}

public sealed class MafFallbackGraphOptions
{
    public const string SectionName = "AiWorkflow:MafFallback";

    public bool Enabled { get; init; }

    public int TimeoutSeconds { get; init; } = 8;

    public int MaxProposals { get; init; } = 3;

    public decimal MinimumProposalConfidence { get; init; } = 0.7000m;
}

public sealed record MafFallbackGraphRequest(
    Guid TransactionId,
    string Description,
    decimal Amount,
    DateOnly TransactionDate,
    IReadOnlyList<DeterministicClassificationSubcategory> AllowedSubcategories,
    DeterministicClassificationStageResult DeterministicResult,
    SemanticRetrievalResult? SemanticResult,
    ClassificationConfidenceFusionDecision FusionDecision);

public sealed record MafFallbackProposal(
    Guid ProposedSubcategoryId,
    decimal Confidence,
    string RationaleCode,
    string Rationale,
    string? AgentNoteSummary,
    string? ProposedAction,
    string? ProposedExternalMessageDraft);

public sealed record MafFallbackGraphResult(
    bool Succeeded,
    string StatusCode,
    string StatusMessage,
    IReadOnlyList<MafFallbackProposal> Proposals,
    bool MessagingSendDenied,
    int MessagingSendDeniedCount,
    string? MessagingSendDeniedActions);

public interface IMafFallbackGraphExecutor
{
    Task<string> ExecuteAsync(MafFallbackGraphRequest request, CancellationToken cancellationToken = default);
}

public interface IMafFallbackGraphService
{
    Task<MafFallbackGraphResult> ExecuteAsync(
        MafFallbackGraphRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class NoOpMafFallbackGraphExecutor : IMafFallbackGraphExecutor
{
    public Task<string> ExecuteAsync(MafFallbackGraphRequest request, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("No MAF fallback graph executor is configured.");
    }
}

public sealed class MafFallbackGraphService(
    IMafFallbackGraphExecutor executor,
    IOptions<MafFallbackGraphOptions> options,
    ILogger<MafFallbackGraphService> logger) : IMafFallbackGraphService
{
    private const int MaxReasonCodeLength = 120;
    private const int MaxRationaleLength = 500;
    private const int MaxProposedActionLength = 80;
    private const int MaxExternalMessageDraftLength = 1000;
    private static readonly HashSet<string> ExplicitExternalSendActions =
    [
        "notify_external_system",
    ];

    private sealed record ParsedProposalCollection(
        IReadOnlyList<MafFallbackProposal> Proposals,
        IReadOnlyList<string> DeniedSendActions);

    private sealed record ParsedProposalOutcome(
        MafFallbackProposal? Proposal,
        string? DeniedSendAction);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed record MafFallbackResponseSchema(
        [property: JsonPropertyName("proposals")]
        IReadOnlyList<MafFallbackProposalSchema>? Proposals);

    private sealed record MafFallbackProposalSchema(
        [property: JsonPropertyName("proposedSubcategoryId")]
        string? ProposedSubcategoryId,
        [property: JsonPropertyName("confidence")]
        decimal? Confidence,
        [property: JsonPropertyName("rationaleCode")]
        string? RationaleCode,
        [property: JsonPropertyName("rationale")]
        string? Rationale,
        [property: JsonPropertyName("agentNoteSummary")]
        string? AgentNoteSummary,
        [property: JsonPropertyName("proposedAction")]
        string? ProposedAction,
        [property: JsonPropertyName("proposedExternalMessageDraft")]
        string? ProposedExternalMessageDraft);

    public async Task<MafFallbackGraphResult> ExecuteAsync(
        MafFallbackGraphRequest request,
        CancellationToken cancellationToken = default)
    {
        var resolvedOptions = options.Value;
        if (!resolvedOptions.Enabled)
        {
            return new MafFallbackGraphResult(
                Succeeded: false,
                MafFallbackGraphStatusCodes.Disabled,
                "MAF fallback graph is disabled by configuration.",
                [],
                false,
                0,
                null);
        }

        if (request.TransactionId == Guid.Empty || request.AllowedSubcategories.Count == 0)
        {
            return new MafFallbackGraphResult(
                Succeeded: false,
                MafFallbackGraphStatusCodes.InvalidRequest,
                "MAF fallback requires a transaction id and at least one allowed subcategory.",
                [],
                false,
                0,
                null);
        }

        var allowedSubcategoryIds = request.AllowedSubcategories
            .Select(x => x.Id)
            .ToHashSet();

        var timeoutSeconds = Math.Clamp(resolvedOptions.TimeoutSeconds, 1, 60);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            var rawResponse = await executor.ExecuteAsync(request, timeoutCts.Token);
            var parsedResponse = JsonSerializer.Deserialize<MafFallbackResponseSchema>(rawResponse, JsonOptions)
                ?? throw new JsonException("Response body was null.");

            var parsedResult = ParseAndValidateProposals(
                parsedResponse.Proposals,
                allowedSubcategoryIds,
                resolvedOptions.MinimumProposalConfidence,
                resolvedOptions.MaxProposals);

            var deniedCount = parsedResult.DeniedSendActions.Count;
            var deniedActionsCsv = deniedCount == 0
                ? null
                : string.Join(",", parsedResult.DeniedSendActions.Distinct(StringComparer.Ordinal));

            if (deniedCount > 0)
            {
                logger.LogWarning(
                    "MAF fallback denied {DeniedCount} external messaging send action(s) for transaction {TransactionId}. Actions: {DeniedActions}",
                    deniedCount,
                    request.TransactionId,
                    deniedActionsCsv);
            }

            if (parsedResult.Proposals.Count == 0)
            {
                var noProposalStatusCode = deniedCount > 0
                    ? MafFallbackGraphStatusCodes.ExternalMessagingSendDenied
                    : MafFallbackGraphStatusCodes.NoProposals;
                var noProposalStatusMessage = deniedCount > 0
                    ? $"MAF fallback denied {deniedCount} external messaging send action(s); draft-only output remains allowed."
                    : "MAF fallback did not return any schema-valid proposals above threshold.";

                return new MafFallbackGraphResult(
                    Succeeded: true,
                    noProposalStatusCode,
                    noProposalStatusMessage,
                    [],
                    deniedCount > 0,
                    deniedCount,
                    deniedActionsCsv);
            }

            var successStatusMessage = deniedCount > 0
                ? $"MAF fallback returned schema-valid proposals and denied {deniedCount} external messaging send action(s)."
                : "MAF fallback returned schema-valid proposals.";

            return new MafFallbackGraphResult(
                Succeeded: true,
                MafFallbackGraphStatusCodes.Ok,
                successStatusMessage,
                parsedResult.Proposals,
                deniedCount > 0,
                deniedCount,
                deniedActionsCsv);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            logger.LogWarning(
                "MAF fallback timed out for transaction {TransactionId} after {TimeoutSeconds}s.",
                request.TransactionId,
                timeoutSeconds);

            return new MafFallbackGraphResult(
                Succeeded: false,
                MafFallbackGraphStatusCodes.Timeout,
                $"MAF fallback timed out after {timeoutSeconds}s.",
                [],
                false,
                0,
                null);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(
                ex,
                "MAF fallback produced an invalid schema payload for transaction {TransactionId}.",
                request.TransactionId);

            return new MafFallbackGraphResult(
                Succeeded: false,
                MafFallbackGraphStatusCodes.SchemaValidationFailed,
                "MAF fallback returned an invalid proposal schema.",
                [],
                false,
                0,
                null);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(
                ex,
                "MAF fallback execution failed for transaction {TransactionId}.",
                request.TransactionId);

            return new MafFallbackGraphResult(
                Succeeded: false,
                MafFallbackGraphStatusCodes.ExecutionFailed,
                "MAF fallback execution failed.",
                [],
                false,
                0,
                null);
        }
    }

    private static ParsedProposalCollection ParseAndValidateProposals(
        IReadOnlyList<MafFallbackProposalSchema>? proposals,
        IReadOnlySet<Guid> allowedSubcategoryIds,
        decimal minimumConfidence,
        int maxProposals)
    {
        if (proposals is null || proposals.Count == 0)
        {
            return new ParsedProposalCollection([], []);
        }

        var boundedThreshold = decimal.Clamp(minimumConfidence, 0m, 1m);
        var boundedMaxProposals = Math.Clamp(maxProposals, 1, 10);
        var deniedSendActions = new List<string>();

        var parsedProposals = new List<MafFallbackProposal>(proposals.Count);
        foreach (var proposal in proposals)
        {
            var parsedOutcome = ParseProposal(proposal);
            if (parsedOutcome is null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(parsedOutcome.DeniedSendAction))
            {
                deniedSendActions.Add(parsedOutcome.DeniedSendAction!);
            }

            if (parsedOutcome.Proposal is null)
            {
                continue;
            }

            if (!allowedSubcategoryIds.Contains(parsedOutcome.Proposal.ProposedSubcategoryId))
            {
                continue;
            }

            if (parsedOutcome.Proposal.Confidence < boundedThreshold)
            {
                continue;
            }

            parsedProposals.Add(parsedOutcome.Proposal);
        }

        var parsed = parsedProposals
            .GroupBy(x => x.ProposedSubcategoryId)
            .Select(group => group
                .OrderByDescending(x => x.Confidence)
                .ThenBy(x => x.RationaleCode, StringComparer.Ordinal)
                .First())
            .OrderByDescending(x => x.Confidence)
            .ThenBy(x => x.ProposedSubcategoryId)
            .Take(boundedMaxProposals)
            .ToList();

        return new ParsedProposalCollection(parsed, deniedSendActions);
    }

    private static ParsedProposalOutcome? ParseProposal(MafFallbackProposalSchema proposal)
    {
        var proposedAction = NormalizeOptionalAction(proposal.ProposedAction);
        if (IsExternalMessagingSendAction(proposedAction))
        {
            return new ParsedProposalOutcome(
                Proposal: null,
                DeniedSendAction: proposedAction);
        }

        if (!Guid.TryParse(proposal.ProposedSubcategoryId, out var subcategoryId))
        {
            return null;
        }

        if (!proposal.Confidence.HasValue)
        {
            return null;
        }

        var confidence = decimal.Round(decimal.Clamp(proposal.Confidence.Value, 0m, 1m), 4, MidpointRounding.AwayFromZero);
        var rationaleCode = Truncate(proposal.RationaleCode?.Trim(), MaxReasonCodeLength);
        var rationale = Truncate(proposal.Rationale?.Trim(), MaxRationaleLength);
        var agentSummary = AgentNoteSummaryPolicy.Sanitize(proposal.AgentNoteSummary);
        var externalMessageDraft = NormalizeOptionalValue(proposal.ProposedExternalMessageDraft, MaxExternalMessageDraftLength);

        if (string.IsNullOrWhiteSpace(rationaleCode) || string.IsNullOrWhiteSpace(rationale))
        {
            return null;
        }

        return new ParsedProposalOutcome(
            new MafFallbackProposal(
                subcategoryId,
                confidence,
                rationaleCode,
                rationale,
                agentSummary,
                proposedAction,
                externalMessageDraft),
            DeniedSendAction: null);
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }

    private static string? NormalizeOptionalValue(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : trimmed[..maxLength];
    }

    private static bool IsExternalMessagingSendAction(string? proposedAction)
    {
        if (string.IsNullOrWhiteSpace(proposedAction))
        {
            return false;
        }

        var normalized = proposedAction.Trim().ToLowerInvariant();
        return normalized.StartsWith("send_", StringComparison.Ordinal)
            || ExplicitExternalSendActions.Contains(normalized);
    }

    private static string? NormalizeOptionalAction(string? action)
    {
        var normalized = NormalizeOptionalValue(action, MaxProposedActionLength);
        return normalized?.ToLowerInvariant();
    }
}