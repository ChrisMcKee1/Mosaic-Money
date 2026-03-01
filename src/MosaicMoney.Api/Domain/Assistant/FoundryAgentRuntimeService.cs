using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Options;

namespace MosaicMoney.Api.Domain.Agent;

public sealed class FoundryAgentOptions
{
    public const string SectionName = "AiWorkflow:Agent:Foundry";

    public const string LegacySectionName = "AiWorkflow:Assistant:Foundry";

    public bool Enabled { get; init; }

    public string Endpoint { get; init; } = string.Empty;

    public bool UseDefaultAzureCredential { get; init; } = true;

    public string AzureAiScope { get; init; } = "https://ai.azure.com/.default";

    public string ApiKey { get; init; } = string.Empty;

    public bool CreateAgentIfMissing { get; init; }

    public bool PreferLatestVersion { get; init; } = true;

    public string Deployment { get; init; } = "gpt-5.3-codex";

    public string AgentName { get; init; } = "Mosaic";

    public string SystemPrompt { get; init; } =
        "You are Mosaic, the Mosaic Money Foundry agent. Keep responses concise, preserve single-entry ledger semantics, and route ambiguous or high-impact outcomes to human review.";

    public string? McpDatabaseToolName { get; init; }

    public string? McpDatabaseToolEndpoint { get; init; }

    public string? McpDatabaseToolProjectConnectionId { get; init; }

    public string? McpDatabaseAllowedToolsCsv { get; init; }

    public string McpDatabaseRequireApproval { get; init; } = "never";

    public string? McpApiToolName { get; init; }

    public string? McpApiToolEndpoint { get; init; }

    public string? McpApiToolProjectConnectionId { get; init; }

    public string? McpApiAllowedToolsCsv { get; init; }

    public string McpApiRequireApproval { get; init; } = "always";

    public string? KnowledgeBaseMcpServerLabel { get; init; }

    public string? KnowledgeBaseMcpEndpoint { get; init; }

    public string? KnowledgeBaseProjectConnectionId { get; init; }

    public string KnowledgeBaseAllowedToolsCsv { get; init; } = "knowledge_base_retrieve";

    public string KnowledgeBaseRequireApproval { get; init; } = "never";

    public string? KnowledgeSourceUrl { get; init; }

    public string ApiVersion { get; init; } = "2025-11-01-preview";

    public bool IsConfigured()
    {
        var hasAuth = UseDefaultAzureCredential || !string.IsNullOrWhiteSpace(ApiKey);
        return Enabled
            && !string.IsNullOrWhiteSpace(Endpoint)
            && !string.IsNullOrWhiteSpace(Deployment)
            && !string.IsNullOrWhiteSpace(AgentName)
            && hasAuth;
    }

    public string GetProjectEndpoint()
    {
        return Endpoint.Trim().TrimEnd('/');
    }

    public IReadOnlyList<FoundryMcpToolDescriptor> GetConfiguredMcpTools()
    {
        var tools = new List<FoundryMcpToolDescriptor>();

        if (!string.IsNullOrWhiteSpace(McpDatabaseToolName)
            && !string.IsNullOrWhiteSpace(McpDatabaseToolEndpoint))
        {
            tools.Add(new FoundryMcpToolDescriptor(
                ServerLabel: McpDatabaseToolName.Trim(),
                ServerUrl: McpDatabaseToolEndpoint.Trim(),
                RequireApproval: NormalizeRequireApproval(McpDatabaseRequireApproval, "never"),
                AllowedTools: ParseCsv(McpDatabaseAllowedToolsCsv),
                ProjectConnectionId: NormalizeNullable(McpDatabaseToolProjectConnectionId)));
        }

        if (!string.IsNullOrWhiteSpace(McpApiToolName)
            && !string.IsNullOrWhiteSpace(McpApiToolEndpoint))
        {
            tools.Add(new FoundryMcpToolDescriptor(
                ServerLabel: McpApiToolName.Trim(),
                ServerUrl: McpApiToolEndpoint.Trim(),
                RequireApproval: NormalizeRequireApproval(McpApiRequireApproval, "always"),
                AllowedTools: ParseCsv(McpApiAllowedToolsCsv),
                ProjectConnectionId: NormalizeNullable(McpApiToolProjectConnectionId)));
        }

        if (!string.IsNullOrWhiteSpace(KnowledgeBaseMcpServerLabel)
            && !string.IsNullOrWhiteSpace(KnowledgeBaseMcpEndpoint))
        {
            tools.Add(new FoundryMcpToolDescriptor(
                ServerLabel: KnowledgeBaseMcpServerLabel.Trim(),
                ServerUrl: KnowledgeBaseMcpEndpoint.Trim(),
                RequireApproval: NormalizeRequireApproval(KnowledgeBaseRequireApproval, "never"),
                AllowedTools: ParseCsv(KnowledgeBaseAllowedToolsCsv),
                ProjectConnectionId: NormalizeNullable(KnowledgeBaseProjectConnectionId)));
        }

        return tools;
    }

    private static string NormalizeRequireApproval(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "always" => "always",
            "never" => "never",
            _ => fallback,
        };
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static IReadOnlyList<string> ParseCsv(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static candidate => !string.IsNullOrWhiteSpace(candidate))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

public sealed record FoundryMcpToolDescriptor(
    string ServerLabel,
    string ServerUrl,
    string RequireApproval,
    IReadOnlyList<string> AllowedTools,
    string? ProjectConnectionId);

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

public interface IFoundryAgentRuntimeService
{
    Task<FoundryAgentBootstrapResult> EnsureAgentAsync(CancellationToken cancellationToken = default);

    Task<FoundryAgentInvocationResult> InvokeAsync(
        FoundryAgentInvocationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class FoundryAgentBootstrapHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<FoundryAgentOptions> options,
    ILogger<FoundryAgentBootstrapHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var agentOptions = options.Value;
        if (!agentOptions.Enabled)
        {
            logger.LogInformation("Foundry agent bootstrap skipped because the runtime is disabled.");
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var runtime = scope.ServiceProvider.GetRequiredService<IFoundryAgentRuntimeService>();

        var bootstrapResult = await runtime.EnsureAgentAsync(stoppingToken);
        if (bootstrapResult.Succeeded)
        {
            logger.LogInformation(
                "Foundry agent bootstrap succeeded. AgentName={AgentName}, AgentSource={AgentSource}, Created={Created}, UsedFallbackPayload={UsedFallbackPayload}",
                bootstrapResult.AgentName,
                bootstrapResult.AgentSource,
                bootstrapResult.Created,
                bootstrapResult.UsedFallbackPayload);
            return;
        }

        logger.LogWarning(
            "Foundry agent bootstrap did not complete. AgentName={AgentName}, OutcomeCode={OutcomeCode}, Summary={Summary}",
            bootstrapResult.AgentName,
            bootstrapResult.OutcomeCode,
            bootstrapResult.Summary);
    }
}

public sealed class FoundryAgentRuntimeService(
    IHttpClientFactory httpClientFactory,
    IOptions<FoundryAgentOptions> options,
    ILogger<FoundryAgentRuntimeService> logger) : IFoundryAgentRuntimeService
{
    private sealed record FoundryAgentReference(
        string AgentId,
        string AgentName,
        int Version,
        DateTimeOffset UpdatedAtUtc,
        DateTimeOffset CreatedAtUtc);

    private const string AgentSource = "foundry";
    private readonly TokenCredential tokenCredential = new DefaultAzureCredential();
    private readonly SemaphoreSlim bearerTokenGate = new(1, 1);
    private AccessToken? cachedAccessToken;

    public async Task<FoundryAgentBootstrapResult> EnsureAgentAsync(CancellationToken cancellationToken = default)
    {
        var agentOptions = options.Value;
        if (!agentOptions.IsConfigured())
        {
            return new FoundryAgentBootstrapResult(
                Succeeded: false,
                AgentName: agentOptions.AgentName,
                AgentSource,
                OutcomeCode: "agent_foundry_not_configured",
                Summary: "Foundry agent configuration is not complete; routing to human review.",
                AgentId: null,
                Created: false,
                UsedFallbackPayload: false);
        }

        var existingAgent = await FindPreferredAgentAsync(agentOptions.AgentName, cancellationToken);
        if (existingAgent is not null)
        {
            var summary = string.Equals(existingAgent.AgentName, agentOptions.AgentName, StringComparison.OrdinalIgnoreCase)
                ? "Foundry agent already exists in the Foundry project."
                : $"Using latest Foundry agent version '{existingAgent.AgentName}' for '{agentOptions.AgentName}'.";

            return new FoundryAgentBootstrapResult(
                Succeeded: true,
                AgentName: existingAgent.AgentName,
                AgentSource,
                OutcomeCode: "agent_ready",
                Summary: summary,
                AgentId: existingAgent.AgentId,
                Created: false,
                UsedFallbackPayload: false);
        }

        if (!agentOptions.CreateAgentIfMissing)
        {
            return new FoundryAgentBootstrapResult(
                Succeeded: false,
                AgentName: agentOptions.AgentName,
                AgentSource,
                OutcomeCode: "agent_not_found",
                Summary: "Foundry agent was not found in the project. Provision the agent before invoking runtime commands.",
                AgentId: null,
                Created: false,
                UsedFallbackPayload: false);
        }

        var (created, usedFallbackPayload) = await TryCreateAgentAsync(agentOptions, cancellationToken);
        if (!created.Succeeded)
        {
            return new FoundryAgentBootstrapResult(
                Succeeded: false,
                AgentName: agentOptions.AgentName,
                AgentSource,
                OutcomeCode: created.OutcomeCode,
                Summary: created.Summary,
                AgentId: null,
                Created: false,
                UsedFallbackPayload: usedFallbackPayload);
        }

        return new FoundryAgentBootstrapResult(
            Succeeded: true,
            AgentName: agentOptions.AgentName,
            AgentSource,
            OutcomeCode: "agent_created",
            Summary: "Foundry agent created in the Foundry project.",
            AgentId: created.AgentId,
            Created: true,
            UsedFallbackPayload: usedFallbackPayload);
    }

    public async Task<FoundryAgentInvocationResult> InvokeAsync(
        FoundryAgentInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        var ensured = await EnsureAgentAsync(cancellationToken);
        if (!ensured.Succeeded || string.IsNullOrWhiteSpace(ensured.AgentId))
        {
            return BuildUnavailableResult(
                request,
                ensured.AgentName,
                "agent_runtime_unavailable",
                ensured.Summary,
                ensured.AgentId);
        }

        var threadId = await CreateThreadAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(threadId))
        {
            return BuildUnavailableResult(
                request,
                ensured.AgentName,
                "agent_thread_create_failed",
                "Unable to create Foundry thread; human review required.",
                ensured.AgentId);
        }

        var posted = await AddUserMessageAsync(threadId, BuildUserPrompt(request), cancellationToken);
        if (!posted)
        {
            return BuildUnavailableResult(
                request,
                ensured.AgentName,
                "agent_message_post_failed",
                "Unable to post message to Foundry thread; human review required.",
                ensured.AgentId);
        }

        var runId = await CreateRunAsync(threadId, ensured.AgentId, cancellationToken);
        if (string.IsNullOrWhiteSpace(runId))
        {
            return BuildUnavailableResult(
                request,
                ensured.AgentName,
                "agent_run_create_failed",
                "Unable to start Foundry run; human review required.",
                ensured.AgentId);
        }

        var runStatus = await WaitForTerminalRunStatusAsync(threadId, runId, cancellationToken);
        if (!string.Equals(runStatus, "completed", StringComparison.OrdinalIgnoreCase))
        {
            return BuildUnavailableResult(
                request,
                ensured.AgentName,
                "agent_run_not_completed",
                $"Foundry run ended with status '{runStatus ?? "unknown"}'; human review required.",
                ensured.AgentId);
        }

        var agentReply = await GetLatestAssistantMessageAsync(threadId, cancellationToken);
        var summary = string.IsNullOrWhiteSpace(agentReply)
            ? "Foundry agent run completed with no reply body."
            : Truncate(SanitizeSingleLine(agentReply), 500);

        return new FoundryAgentInvocationResult(
            Succeeded: true,
            NeedsReview: false,
            AgentName: ensured.AgentName,
            AgentSource,
            OutcomeCode: "agent_run_completed",
            Summary: "Foundry agent invocation completed.",
            AssignmentHint: request.PolicyDisposition,
            ResponseSummary: summary,
            AgentId: ensured.AgentId);
    }

    private FoundryAgentInvocationResult BuildUnavailableResult(
        FoundryAgentInvocationRequest request,
        string agentName,
        string outcomeCode,
        string summary,
        string? agentId)
    {
        return new FoundryAgentInvocationResult(
            Succeeded: false,
            NeedsReview: true,
            AgentName: agentName,
            AgentSource,
            OutcomeCode: outcomeCode,
            Summary: summary,
            AssignmentHint: request.PolicyDisposition,
            ResponseSummary: null,
            AgentId: agentId);
    }

    private async Task<FoundryAgentReference?> FindPreferredAgentAsync(string configuredAgentName, CancellationToken cancellationToken)
    {
        var response = await SendJsonAsync(
            HttpMethod.Get,
            BuildApiPath("/agents", includeApiVersion: true),
            body: null,
            cancellationToken);

        if (!response.Succeeded || response.Document is null)
        {
            return null;
        }

        var candidates = EnumerateAgentCandidates(response.Document.RootElement)
            .Select(candidate => ParseAgentReference(candidate, configuredAgentName))
            .Where(static candidate => candidate is not null)
            .Select(static candidate => candidate!)
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        if (!options.Value.PreferLatestVersion)
        {
            return candidates[0];
        }

        return candidates
            .OrderByDescending(candidate => candidate.Version)
            .ThenByDescending(candidate => candidate.UpdatedAtUtc)
            .ThenByDescending(candidate => candidate.CreatedAtUtc)
            .FirstOrDefault();
    }

    private static IEnumerable<JsonElement> EnumerateAgentCandidates(JsonElement root)
    {
        if (root.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in dataElement.EnumerateArray())
            {
                yield return element;
            }

            yield break;
        }

        if (root.TryGetProperty("value", out var valueElement) && valueElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in valueElement.EnumerateArray())
            {
                yield return element;
            }

            yield break;
        }

        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in root.EnumerateArray())
            {
                yield return element;
            }
        }
    }

    private static FoundryAgentReference? ParseAgentReference(JsonElement candidate, string configuredAgentName)
    {
        var discoveredAgentName = TryReadString(candidate, "name")
            ?? TryReadString(candidate, "agent_name")
            ?? TryReadString(candidate, "agentName");

        if (string.IsNullOrWhiteSpace(discoveredAgentName)
            || !IsMatchingAgentName(discoveredAgentName, configuredAgentName))
        {
            return null;
        }

        var agentId = TryReadString(candidate, "id")
            ?? TryReadString(candidate, "agent_id")
            ?? TryReadString(candidate, "agentId");

        if (string.IsNullOrWhiteSpace(agentId))
        {
            return null;
        }

        var version = TryReadInt(candidate, "version")
            ?? TryReadInt(candidate, "agent_version")
            ?? ResolveVersionFromName(discoveredAgentName, configuredAgentName)
            ?? 0;

        var updatedAtUtc = TryReadDateTimeOffset(candidate, "last_modified_at")
            ?? TryReadDateTimeOffset(candidate, "updated_at")
            ?? TryReadDateTimeOffset(candidate, "modifiedAt")
            ?? DateTimeOffset.MinValue;

        var createdAtUtc = TryReadDateTimeOffset(candidate, "created_at")
            ?? TryReadDateTimeOffset(candidate, "createdAt")
            ?? DateTimeOffset.MinValue;

        return new FoundryAgentReference(agentId, discoveredAgentName, version, updatedAtUtc, createdAtUtc);
    }

    private static bool IsMatchingAgentName(string discoveredAgentName, string configuredAgentName)
    {
        if (string.Equals(discoveredAgentName, configuredAgentName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return discoveredAgentName.StartsWith(configuredAgentName + ":", StringComparison.OrdinalIgnoreCase);
    }

    private static int? ResolveVersionFromName(string discoveredAgentName, string configuredAgentName)
    {
        if (!discoveredAgentName.StartsWith(configuredAgentName + ":", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var suffix = discoveredAgentName[(configuredAgentName.Length + 1)..].Trim();
        return int.TryParse(suffix, out var parsed)
            ? parsed
            : null;
    }

    private async Task<(FoundryAgentBootstrapResult Succeeded, bool UsedFallbackPayload)> TryCreateAgentAsync(
        FoundryAgentOptions agentOptions,
        CancellationToken cancellationToken)
    {
        var primaryPayload = BuildCreateAgentPayload(agentOptions, includeOptionalToolAndKnowledge: true);
        var createResponse = await SendJsonAsync(
            HttpMethod.Post,
            BuildApiPath("/agents", includeApiVersion: true),
            primaryPayload,
            cancellationToken);

        if (createResponse.Succeeded)
        {
            var agentId = TryReadString(createResponse.Document?.RootElement, "id")
                ?? TryReadString(createResponse.Document?.RootElement, "agent_id")
                ?? TryReadString(createResponse.Document?.RootElement, "agentId");

            return (
                new FoundryAgentBootstrapResult(
                    Succeeded: true,
                    AgentName: agentOptions.AgentName,
                    AgentSource,
                    OutcomeCode: "agent_created",
                    Summary: "Foundry agent created in the Foundry project.",
                    AgentId: agentId,
                    Created: true,
                    UsedFallbackPayload: false),
                false);
        }

        var shouldRetryMinimal = createResponse.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity;
        if (shouldRetryMinimal)
        {
            var minimalPayload = BuildCreateAgentPayload(agentOptions, includeOptionalToolAndKnowledge: false);
            var retry = await SendJsonAsync(
                HttpMethod.Post,
                BuildApiPath("/agents", includeApiVersion: true),
                minimalPayload,
                cancellationToken);

            if (retry.Succeeded)
            {
                var retryAgentId = TryReadString(retry.Document?.RootElement, "id")
                    ?? TryReadString(retry.Document?.RootElement, "agent_id")
                    ?? TryReadString(retry.Document?.RootElement, "agentId");

                return (
                    new FoundryAgentBootstrapResult(
                        Succeeded: true,
                        AgentName: agentOptions.AgentName,
                        AgentSource,
                        OutcomeCode: "agent_created_minimal",
                        Summary: "Foundry agent created with a minimal payload because optional fields were unsupported.",
                        AgentId: retryAgentId,
                        Created: true,
                        UsedFallbackPayload: true),
                    true);
            }
        }

        logger.LogWarning(
            "Foundry agent creation failed. Status={StatusCode}, Detail={Detail}",
            createResponse.StatusCode,
            Truncate(createResponse.ErrorDetail ?? string.Empty, 300));

        return (
            new FoundryAgentBootstrapResult(
                Succeeded: false,
                AgentName: agentOptions.AgentName,
                AgentSource,
                OutcomeCode: "agent_create_failed",
                Summary: "Unable to create a Foundry agent in the Foundry project.",
                AgentId: null,
                Created: false,
                UsedFallbackPayload: false),
            false);
    }

    private JsonObject BuildCreateAgentPayload(FoundryAgentOptions agentOptions, bool includeOptionalToolAndKnowledge)
    {
        var payload = new JsonObject
        {
            ["name"] = agentOptions.AgentName,
            ["model"] = agentOptions.Deployment,
            ["instructions"] = agentOptions.SystemPrompt,
        };

        if (!includeOptionalToolAndKnowledge)
        {
            return payload;
        }

        var tools = new JsonArray();
        foreach (var configuredTool in agentOptions.GetConfiguredMcpTools())
        {
            var toolNode = new JsonObject
            {
                ["type"] = "mcp",
                ["server_label"] = configuredTool.ServerLabel,
                ["server_url"] = configuredTool.ServerUrl,
                ["require_approval"] = configuredTool.RequireApproval,
            };

            if (configuredTool.AllowedTools.Count > 0)
            {
                var allowedTools = new JsonArray();
                foreach (var toolName in configuredTool.AllowedTools)
                {
                    allowedTools.Add(toolName);
                }

                toolNode["allowed_tools"] = allowedTools;
            }

            if (!string.IsNullOrWhiteSpace(configuredTool.ProjectConnectionId))
            {
                toolNode["project_connection_id"] = configuredTool.ProjectConnectionId;
            }

            tools.Add(toolNode);
        }

        if (tools.Count > 0)
        {
            payload["tools"] = tools;
        }

        if (!string.IsNullOrWhiteSpace(agentOptions.KnowledgeSourceUrl))
        {
            payload["knowledge_sources"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "url",
                    ["url"] = agentOptions.KnowledgeSourceUrl,
                },
            };
        }

        return payload;
    }

    private async Task<string?> CreateThreadAsync(CancellationToken cancellationToken)
    {
        var response = await SendJsonAsync(
            HttpMethod.Post,
            BuildApiPath("/threads", includeApiVersion: true),
            body: new JsonObject(),
            cancellationToken);

        if (!response.Succeeded || response.Document is null)
        {
            return null;
        }

        return TryReadString(response.Document.RootElement, "id")
            ?? TryReadString(response.Document.RootElement, "thread_id")
            ?? TryReadString(response.Document.RootElement, "threadId");
    }

    private async Task<bool> AddUserMessageAsync(string threadId, string message, CancellationToken cancellationToken)
    {
        var payload = new JsonObject
        {
            ["role"] = "user",
            ["content"] = message,
        };

        var response = await SendJsonAsync(
            HttpMethod.Post,
            BuildApiPath($"/threads/{Uri.EscapeDataString(threadId)}/messages", includeApiVersion: true),
            payload,
            cancellationToken);

        return response.Succeeded;
    }

    private async Task<string?> CreateRunAsync(string threadId, string agentId, CancellationToken cancellationToken)
    {
        var payload = new JsonObject
        {
            ["assistant_id"] = agentId,
        };

        var response = await SendJsonAsync(
            HttpMethod.Post,
            BuildApiPath($"/threads/{Uri.EscapeDataString(threadId)}/runs", includeApiVersion: true),
            payload,
            cancellationToken);

        if (!response.Succeeded || response.Document is null)
        {
            return null;
        }

        return TryReadString(response.Document.RootElement, "id")
            ?? TryReadString(response.Document.RootElement, "run_id")
            ?? TryReadString(response.Document.RootElement, "runId");
    }

    private async Task<string?> WaitForTerminalRunStatusAsync(string threadId, string runId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 25; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = await SendJsonAsync(
                HttpMethod.Get,
                BuildApiPath($"/threads/{Uri.EscapeDataString(threadId)}/runs/{Uri.EscapeDataString(runId)}", includeApiVersion: true),
                body: null,
                cancellationToken);

            if (!response.Succeeded || response.Document is null)
            {
                return "failed";
            }

            var status = TryReadString(response.Document.RootElement, "status");
            if (IsTerminalStatus(status))
            {
                return status;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        return "timeout";
    }

    private async Task<string?> GetLatestAssistantMessageAsync(string threadId, CancellationToken cancellationToken)
    {
        var response = await SendJsonAsync(
            HttpMethod.Get,
            BuildApiPath($"/threads/{Uri.EscapeDataString(threadId)}/messages?order=desc&limit=20", includeApiVersion: true),
            body: null,
            cancellationToken);

        if (!response.Succeeded || response.Document is null)
        {
            return null;
        }

        var root = response.Document.RootElement;
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in data.EnumerateArray())
        {
            var role = TryReadString(item, "role");
            if (!string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var topLevelContent = TryReadString(item, "content");
            if (!string.IsNullOrWhiteSpace(topLevelContent))
            {
                return topLevelContent;
            }

            if (!item.TryGetProperty("content", out var contentArray)
                || contentArray.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var contentEntry in contentArray.EnumerateArray())
            {
                var text = TryReadString(contentEntry, "text");
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }

                if (contentEntry.TryGetProperty("text", out var nestedText)
                    && nestedText.ValueKind == JsonValueKind.Object)
                {
                    var value = TryReadString(nestedText, "value");
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }
        }

        return null;
    }

    private string BuildUserPrompt(FoundryAgentInvocationRequest request)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"CommandType: {request.CommandType}");
        builder.AppendLine($"HouseholdId: {request.HouseholdId:D}");
        builder.AppendLine($"ConversationId: {request.ConversationId:D}");
        builder.AppendLine($"HouseholdUserId: {request.HouseholdUserId:D}");
        builder.AppendLine($"PolicyDisposition: {request.PolicyDisposition}");
        builder.AppendLine($"Message: {request.Message}");

        if (!string.IsNullOrWhiteSpace(request.UserNote))
        {
            builder.AppendLine($"UserNote: {request.UserNote}");
        }

        if (request.ApprovalId.HasValue)
        {
            builder.AppendLine($"ApprovalId: {request.ApprovalId.Value:D}");
        }

        if (!string.IsNullOrWhiteSpace(request.ApprovalDecision))
        {
            builder.AppendLine($"ApprovalDecision: {request.ApprovalDecision}");
        }

        if (!string.IsNullOrWhiteSpace(request.ApprovalRationale))
        {
            builder.AppendLine($"ApprovalRationale: {request.ApprovalRationale}");
        }

        return builder.ToString();
    }

    private async Task<(bool Succeeded, HttpStatusCode? StatusCode, JsonDocument? Document, string? ErrorDetail)> SendJsonAsync(
        HttpMethod method,
        string path,
        JsonNode? body,
        CancellationToken cancellationToken)
    {
        var agentOptions = options.Value;
        var authSetters = await ResolveAuthHeaderSettersAsync(agentOptions, cancellationToken);
        if (authSetters.Count == 0)
        {
            return (false, null, null, "No Foundry authentication method is configured.");
        }

        var serializedBody = body?.ToJsonString();
        string? lastError = null;
        HttpStatusCode? lastStatusCode = null;

        foreach (var applyAuthHeader in authSetters)
        {
            try
            {
                using var request = new HttpRequestMessage(method, BuildAbsoluteUri(path));
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                applyAuthHeader(request.Headers);

                if (serializedBody is not null)
                {
                    request.Content = new StringContent(serializedBody, Encoding.UTF8, "application/json");
                }

                using var client = httpClientFactory.CreateClient(nameof(FoundryAgentRuntimeService));
                using var response = await client.SendAsync(request, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    lastStatusCode = response.StatusCode;
                    lastError = Truncate(SanitizeSingleLine(responseBody), 500);

                    if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    {
                        continue;
                    }

                    return (false, response.StatusCode, null, lastError);
                }

                if (string.IsNullOrWhiteSpace(responseBody))
                {
                    return (true, response.StatusCode, null, null);
                }

                JsonDocument? parsedDocument = null;
                try
                {
                    parsedDocument = JsonDocument.Parse(responseBody);
                }
                catch (JsonException)
                {
                    logger.LogWarning("Foundry response payload was not JSON for path {Path}.", path);
                }

                return (true, response.StatusCode, parsedDocument, null);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Foundry request failed for path {Path}.", path);
                lastError = Truncate(exception.Message, 500);
            }
        }

        return (false, lastStatusCode, null, lastError ?? "Foundry request failed.");
    }

    private async Task<IReadOnlyList<Action<HttpRequestHeaders>>> ResolveAuthHeaderSettersAsync(
        FoundryAgentOptions agentOptions,
        CancellationToken cancellationToken)
    {
        var setters = new List<Action<HttpRequestHeaders>>();

        if (agentOptions.UseDefaultAzureCredential)
        {
            var bearer = await ResolveBearerTokenAsync(agentOptions.AzureAiScope, cancellationToken);
            if (!string.IsNullOrWhiteSpace(bearer))
            {
                setters.Add(headers =>
                {
                    headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(agentOptions.ApiKey))
        {
            setters.Add(headers =>
            {
                headers.Add("api-key", agentOptions.ApiKey);
            });
        }

        return setters;
    }

    private async Task<string?> ResolveBearerTokenAsync(string scope, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }

        await bearerTokenGate.WaitAsync(cancellationToken);
        try
        {
            if (cachedAccessToken is AccessToken existing
                && existing.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(2))
            {
                return existing.Token;
            }

            try
            {
                var token = await tokenCredential.GetTokenAsync(
                    new TokenRequestContext([scope]),
                    cancellationToken);
                cachedAccessToken = token;
                return token.Token;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Failed to acquire Foundry bearer token for scope {Scope}.", scope);
                return null;
            }
        }
        finally
        {
            bearerTokenGate.Release();
        }
    }

    private string BuildAbsoluteUri(string path)
    {
        return options.Value.GetProjectEndpoint() + path;
    }

    private string BuildApiPath(string relativePath, bool includeApiVersion)
    {
        if (!includeApiVersion)
        {
            return relativePath;
        }

        var separator = relativePath.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return relativePath + separator + "api-version=" + Uri.EscapeDataString(options.Value.ApiVersion);
    }

    private static bool IsTerminalStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        return status.Equals("completed", StringComparison.OrdinalIgnoreCase)
            || status.Equals("failed", StringComparison.OrdinalIgnoreCase)
            || status.Equals("cancelled", StringComparison.OrdinalIgnoreCase)
            || status.Equals("incomplete", StringComparison.OrdinalIgnoreCase)
            || status.Equals("expired", StringComparison.OrdinalIgnoreCase)
            || status.Equals("requires_action", StringComparison.OrdinalIgnoreCase)
            || status.Equals("requiresaction", StringComparison.OrdinalIgnoreCase)
            || status.Equals("canceled", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryReadString(JsonElement? element, string propertyName)
    {
        if (element is null)
        {
            return null;
        }

        return TryReadString(element.Value, propertyName);
    }

    private static string? TryReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };
    }

    private static int? TryReadInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsedNumber))
        {
            return parsedNumber;
        }

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsedString))
        {
            return parsedString;
        }

        return null;
    }

    private static DateTimeOffset? TryReadDateTimeOffset(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value)
            || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return DateTimeOffset.TryParse(value.GetString(), out var parsed)
            ? parsed
            : null;
    }

    private static string SanitizeSingleLine(string value)
    {
        return value
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }
}
