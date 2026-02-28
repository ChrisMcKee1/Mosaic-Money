namespace MosaicMoney.Api.Domain.Assistant;

public sealed class FoundryAgentOptions
{
    public const string SectionName = "AiWorkflow:Agent:Foundry";

    public const string LegacySectionName = "AiWorkflow:Assistant:Foundry";

    public bool Enabled { get; init; }

    public string Endpoint { get; init; } = string.Empty;

    public string ApiKey { get; init; } = string.Empty;

    public string Deployment { get; init; } = "gpt-5.3-codex";

    public string AgentName { get; init; } = "Mosaic";

    public string SystemPrompt { get; init; } =
        "You are Mosaic, the Mosaic Money Foundry agent. Keep responses concise, preserve single-entry ledger semantics, and route ambiguous or high-impact outcomes to human review.";

    public string? McpDatabaseToolName { get; init; }

    public string? McpDatabaseToolEndpoint { get; init; }

    public string? KnowledgeSourceUrl { get; init; }

    public string ApiVersion { get; init; } = "2025-05-01";

    public bool IsConfigured()
    {
        return Enabled
            && !string.IsNullOrWhiteSpace(Endpoint)
            && !string.IsNullOrWhiteSpace(ApiKey)
            && !string.IsNullOrWhiteSpace(Deployment)
            && !string.IsNullOrWhiteSpace(AgentName);
    }

    public string GetProjectEndpoint()
    {
        return Endpoint.Trim().TrimEnd('/');
    }
}

public sealed record FoundryAgentInvocationRequest(
    Guid HouseholdId,
    Guid ConversationId,
    Guid HouseholdUserId,
    string CommandType,
    string Message,
    string? UserNote,
    string PolicyDisposition,
    Guid? ApprovalId,
    string? ApprovalDecision,
    string? ApprovalRationale);

public sealed record FoundryAgentBootstrapResult(
    bool Succeeded,
    string AgentName,
    string AgentSource,
    string OutcomeCode,
    string Summary,
    string? AgentId,
    bool Created,
    bool UsedFallbackPayload);

public sealed record FoundryAgentInvocationResult(
    bool Succeeded,
    bool NeedsReview,
    string AgentName,
    string AgentSource,
    string OutcomeCode,
    string Summary,
    string AssignmentHint,
    string? ResponseSummary,
    string? AgentId);
