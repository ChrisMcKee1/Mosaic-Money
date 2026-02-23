using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace MosaicMoney.Api.Domain.Ledger.Classification;

public static class MafFallbackGraphStatusCodes
{
    public const string Ok = "ok";
    public const string NoProposals = "no_proposals";
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
    string? AgentNoteSummary);

public sealed record MafFallbackGraphResult(
    bool Succeeded,
    string StatusCode,
    string StatusMessage,
    IReadOnlyList<MafFallbackProposal> Proposals);

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
    private const int MaxAgentSummaryLength = 600;

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
        string? AgentNoteSummary);

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
                []);
        }

        if (request.TransactionId == Guid.Empty || request.AllowedSubcategories.Count == 0)
        {
            return new MafFallbackGraphResult(
                Succeeded: false,
                MafFallbackGraphStatusCodes.InvalidRequest,
                "MAF fallback requires a transaction id and at least one allowed subcategory.",
                []);
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

            var parsedProposals = ParseAndValidateProposals(
                parsedResponse.Proposals,
                allowedSubcategoryIds,
                resolvedOptions.MinimumProposalConfidence,
                resolvedOptions.MaxProposals);

            if (parsedProposals.Count == 0)
            {
                return new MafFallbackGraphResult(
                    Succeeded: true,
                    MafFallbackGraphStatusCodes.NoProposals,
                    "MAF fallback did not return any schema-valid proposals above threshold.",
                    []);
            }

            return new MafFallbackGraphResult(
                Succeeded: true,
                MafFallbackGraphStatusCodes.Ok,
                "MAF fallback returned schema-valid proposals.",
                parsedProposals);
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
                []);
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
                []);
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
                []);
        }
    }

    private static IReadOnlyList<MafFallbackProposal> ParseAndValidateProposals(
        IReadOnlyList<MafFallbackProposalSchema>? proposals,
        IReadOnlySet<Guid> allowedSubcategoryIds,
        decimal minimumConfidence,
        int maxProposals)
    {
        if (proposals is null || proposals.Count == 0)
        {
            return [];
        }

        var boundedThreshold = decimal.Clamp(minimumConfidence, 0m, 1m);
        var boundedMaxProposals = Math.Clamp(maxProposals, 1, 10);

        var parsed = proposals
            .Select(ParseProposal)
            .Where(x => x is not null)
            .Select(x => x!)
            .Where(x => allowedSubcategoryIds.Contains(x.ProposedSubcategoryId))
            .Where(x => x.Confidence >= boundedThreshold)
            .GroupBy(x => x.ProposedSubcategoryId)
            .Select(group => group
                .OrderByDescending(x => x.Confidence)
                .ThenBy(x => x.RationaleCode, StringComparer.Ordinal)
                .First())
            .OrderByDescending(x => x.Confidence)
            .ThenBy(x => x.ProposedSubcategoryId)
            .Take(boundedMaxProposals)
            .ToList();

        return parsed;
    }

    private static MafFallbackProposal? ParseProposal(MafFallbackProposalSchema proposal)
    {
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
        var agentSummary = Truncate(proposal.AgentNoteSummary?.Trim(), MaxAgentSummaryLength);

        if (string.IsNullOrWhiteSpace(rationaleCode) || string.IsNullOrWhiteSpace(rationale))
        {
            return null;
        }

        return new MafFallbackProposal(
            subcategoryId,
            confidence,
            rationaleCode,
            rationale,
            string.IsNullOrWhiteSpace(agentSummary) ? null : agentSummary);
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
}