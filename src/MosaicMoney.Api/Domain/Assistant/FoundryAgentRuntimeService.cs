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

    public string ApiVersion { get; init; } = "v1";

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
        if (!ensured.Succeeded)
        {
            return BuildUnavailableResult(
                request,
                ensured.AgentName,
                "agent_runtime_unavailable",
                ensured.Summary,
                ensured.AgentId);
        }

        var response = await CreateResponseAsync(
            agentName: ensured.AgentName,
            input: BuildUserPrompt(request),
            cancellationToken);

        if (!response.Succeeded)
        {
            return BuildUnavailableResult(
                request,
                ensured.AgentName,
                "agent_response_create_failed",
                string.IsNullOrWhiteSpace(response.ErrorDetail)
                    ? "Unable to create Foundry response; human review required."
                    : $"Unable to create Foundry response; human review required. Detail: {response.ErrorDetail}",
                ensured.AgentId);
        }

        var agentReply = response.ResponseText;
        var summary = string.IsNullOrWhiteSpace(agentReply)
            ? "Foundry response completed with no reply body."
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
        var primaryPayload = BuildCreateAgentPayload(agentOptions, includeOptionalTools: true);
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
            var minimalPayload = BuildCreateAgentPayload(agentOptions, includeOptionalTools: false);
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

    private JsonObject BuildCreateAgentPayload(FoundryAgentOptions agentOptions, bool includeOptionalTools)
    {
        var definition = new JsonObject
        {
            ["model"] = agentOptions.Deployment,
            ["kind"] = "prompt",
            ["instructions"] = agentOptions.SystemPrompt,
        };

        if (includeOptionalTools)
        {
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
                definition["tools"] = tools;
            }
        }

        return new JsonObject
        {
            ["name"] = agentOptions.AgentName,
            ["definition"] = definition,
        };
    }

    private async Task<(bool Succeeded, string? ResponseText, HttpStatusCode? StatusCode, string? ErrorDetail)> CreateResponseAsync(
        string agentName,
        string input,
        CancellationToken cancellationToken)
    {
        var primary = await SendJsonAsync(
            HttpMethod.Post,
            "/openai/v1/responses",
            BuildResponsePayload(agentName, input, useAgentField: true),
            cancellationToken);

        var response = primary;
        if (!response.Succeeded
            && response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity)
        {
            response = await SendJsonAsync(
                HttpMethod.Post,
                "/openai/v1/responses",
                BuildResponsePayload(agentName, input, useAgentField: false),
                cancellationToken);
        }

        if (!response.Succeeded)
        {
            return (false, null, response.StatusCode, response.ErrorDetail);
        }

        var responseText = response.Document is null
            ? null
            : ExtractResponseText(response.Document.RootElement);

        return (true, responseText, response.StatusCode, null);
    }

    private static JsonObject BuildResponsePayload(string agentName, string input, bool useAgentField)
    {
        var payload = new JsonObject
        {
            ["input"] = input,
        };

        var agentReference = new JsonObject
        {
            ["type"] = "agent_reference",
            ["name"] = agentName,
        };

        payload[useAgentField ? "agent" : "agent_reference"] = agentReference;
        return payload;
    }

    private static string? ExtractResponseText(JsonElement responseRoot)
    {
        var outputText = TryReadString(responseRoot, "output_text");
        if (!string.IsNullOrWhiteSpace(outputText))
        {
            return outputText;
        }

        if (!responseRoot.TryGetProperty("output", out var outputItems)
            || outputItems.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var outputItem in outputItems.EnumerateArray())
        {
            var itemText = ExtractOutputItemText(outputItem);
            if (!string.IsNullOrWhiteSpace(itemText))
            {
                return itemText;
            }
        }

        return null;
    }

    private static string? ExtractOutputItemText(JsonElement outputItem)
    {
        var directText = TryReadString(outputItem, "text");
        if (!string.IsNullOrWhiteSpace(directText))
        {
            return directText;
        }

        if (outputItem.TryGetProperty("content", out var content))
        {
            var contentText = ExtractContentText(content);
            if (!string.IsNullOrWhiteSpace(contentText))
            {
                return contentText;
            }
        }

        if (outputItem.TryGetProperty("message", out var messageElement))
        {
            var messageText = ExtractOutputItemText(messageElement);
            if (!string.IsNullOrWhiteSpace(messageText))
            {
                return messageText;
            }
        }

        return null;
    }

    private static string? ExtractContentText(JsonElement contentElement)
    {
        if (contentElement.ValueKind == JsonValueKind.String)
        {
            return contentElement.GetString();
        }

        if (contentElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in contentElement.EnumerateArray())
            {
                var itemText = ExtractContentText(item);
                if (!string.IsNullOrWhiteSpace(itemText))
                {
                    return itemText;
                }
            }

            return null;
        }

        if (contentElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var directText = TryReadString(contentElement, "text")
            ?? TryReadString(contentElement, "output_text")
            ?? TryReadString(contentElement, "value");
        if (!string.IsNullOrWhiteSpace(directText))
        {
            return directText;
        }

        if (contentElement.TryGetProperty("text", out var nestedText))
        {
            var nested = ExtractContentText(nestedText);
            if (!string.IsNullOrWhiteSpace(nested))
            {
                return nested;
            }
        }

        if (contentElement.TryGetProperty("content", out var nestedContent))
        {
            var nested = ExtractContentText(nestedContent);
            if (!string.IsNullOrWhiteSpace(nested))
            {
                return nested;
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

                var client = httpClientFactory.CreateClient(nameof(FoundryAgentRuntimeService));
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
