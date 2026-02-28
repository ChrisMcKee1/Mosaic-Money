using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace MosaicMoney.Api.Domain.Assistant;

public interface IFoundryAgentRuntimeService
{
    Task<FoundryAgentBootstrapResult> EnsureAgentAsync(CancellationToken cancellationToken = default);

    Task<FoundryAgentInvocationResult> InvokeAsync(
        FoundryAgentInvocationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class FoundryAgentRuntimeService(
    IHttpClientFactory httpClientFactory,
    IOptions<FoundryAgentOptions> options,
    ILogger<FoundryAgentRuntimeService> logger) : IFoundryAgentRuntimeService
{
    private const string AgentSource = "foundry";

    public async Task<FoundryAgentBootstrapResult> EnsureAgentAsync(CancellationToken cancellationToken = default)
    {
        var assistantOptions = options.Value;
        if (!assistantOptions.IsConfigured())
        {
            return new FoundryAgentBootstrapResult(
                Succeeded: false,
                AgentName: assistantOptions.AgentName,
                AgentSource,
                OutcomeCode: "assistant_foundry_not_configured",
                Summary: "Foundry agent configuration is not complete; routing to human review.",
                AgentId: null,
                Created: false,
                UsedFallbackPayload: false);
        }

        var existingAgent = await FindAgentByNameAsync(assistantOptions.AgentName, cancellationToken);
        if (existingAgent.Found && !string.IsNullOrWhiteSpace(existingAgent.AgentId))
        {
            return new FoundryAgentBootstrapResult(
                Succeeded: true,
                assistantOptions.AgentName,
                AgentSource,
                OutcomeCode: "assistant_agent_ready",
                Summary: "Foundry agent already exists in the Foundry project.",
                AgentId: existingAgent.AgentId,
                Created: false,
                UsedFallbackPayload: false);
        }

        var (created, usedFallbackPayload) = await TryCreateAgentAsync(assistantOptions, cancellationToken);
        if (!created.Succeeded)
        {
            return new FoundryAgentBootstrapResult(
                Succeeded: false,
                assistantOptions.AgentName,
                AgentSource,
                OutcomeCode: created.OutcomeCode,
                Summary: created.Summary,
                AgentId: null,
                Created: false,
                UsedFallbackPayload: usedFallbackPayload);
        }

        return new FoundryAgentBootstrapResult(
            Succeeded: true,
            assistantOptions.AgentName,
            AgentSource,
            OutcomeCode: "assistant_agent_created",
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
            return new FoundryAgentInvocationResult(
                Succeeded: false,
                NeedsReview: true,
                AgentName: options.Value.AgentName,
                AgentSource,
                OutcomeCode: ensured.OutcomeCode,
                Summary: ensured.Summary,
                AssignmentHint: request.PolicyDisposition,
                ResponseSummary: null,
                AgentId: ensured.AgentId);
        }

        var threadId = await CreateThreadAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(threadId))
        {
            return BuildUnavailableResult(request, "assistant_thread_create_failed", "Unable to create Foundry thread; human review required.", ensured.AgentId);
        }

        var posted = await AddUserMessageAsync(threadId, BuildUserPrompt(request), cancellationToken);
        if (!posted)
        {
            return BuildUnavailableResult(request, "assistant_message_post_failed", "Unable to post message to Foundry thread; human review required.", ensured.AgentId);
        }

        var runId = await CreateRunAsync(threadId, ensured.AgentId, cancellationToken);
        if (string.IsNullOrWhiteSpace(runId))
        {
            return BuildUnavailableResult(request, "assistant_run_create_failed", "Unable to start Foundry run; human review required.", ensured.AgentId);
        }

        var runStatus = await WaitForTerminalRunStatusAsync(threadId, runId, cancellationToken);
        if (!string.Equals(runStatus, "completed", StringComparison.OrdinalIgnoreCase))
        {
            return BuildUnavailableResult(
                request,
                "assistant_run_not_completed",
                $"Foundry run ended with status '{runStatus ?? "unknown"}'; human review required.",
                ensured.AgentId);
        }

        var assistantReply = await GetLatestAssistantMessageAsync(threadId, cancellationToken);
        var summary = string.IsNullOrWhiteSpace(assistantReply)
            ? "Foundry agent run completed with no reply body."
            : Truncate(SanitizeSingleLine(assistantReply), 500);

        return new FoundryAgentInvocationResult(
            Succeeded: true,
            NeedsReview: false,
            AgentName: options.Value.AgentName,
            AgentSource,
            OutcomeCode: "assistant_run_completed",
            Summary: "Foundry agent invocation completed.",
            AssignmentHint: request.PolicyDisposition,
            ResponseSummary: summary,
            AgentId: ensured.AgentId);
    }

    private FoundryAgentInvocationResult BuildUnavailableResult(
        FoundryAgentInvocationRequest request,
        string outcomeCode,
        string summary,
        string? agentId)
    {
        return new FoundryAgentInvocationResult(
            Succeeded: false,
            NeedsReview: true,
            AgentName: options.Value.AgentName,
            AgentSource,
            OutcomeCode: outcomeCode,
            Summary: summary,
            AssignmentHint: request.PolicyDisposition,
            ResponseSummary: null,
            AgentId: agentId);
    }

    private async Task<(bool Found, string? AgentId)> FindAgentByNameAsync(string agentName, CancellationToken cancellationToken)
    {
        var response = await SendJsonAsync(
            HttpMethod.Get,
            BuildApiPath("/agents", includeApiVersion: true),
            body: null,
            cancellationToken);

        if (!response.Succeeded || response.Document is null)
        {
            return (false, null);
        }

        var root = response.Document.RootElement;
        var candidates = root.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Array
            ? dataElement.EnumerateArray()
            : root.ValueKind == JsonValueKind.Array
                ? root.EnumerateArray()
                : Enumerable.Empty<JsonElement>();

        foreach (var candidate in candidates)
        {
            var name = TryReadString(candidate, "name")
                ?? TryReadString(candidate, "agent_name")
                ?? TryReadString(candidate, "agentName");

            if (!string.Equals(name, agentName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var id = TryReadString(candidate, "id")
                ?? TryReadString(candidate, "agent_id")
                ?? TryReadString(candidate, "agentId");

            return (!string.IsNullOrWhiteSpace(id), id);
        }

        return (false, null);
    }

    private async Task<(FoundryAgentBootstrapResult Succeeded, bool UsedFallbackPayload)> TryCreateAgentAsync(
        FoundryAgentOptions assistantOptions,
        CancellationToken cancellationToken)
    {
        var primaryPayload = BuildCreateAgentPayload(assistantOptions, includeOptionalToolAndKnowledge: true);
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
                    assistantOptions.AgentName,
                    AgentSource,
                    OutcomeCode: "assistant_agent_created",
                    Summary: "Foundry agent created in the Foundry project.",
                    AgentId: agentId,
                    Created: true,
                    UsedFallbackPayload: false),
                false);
        }

        // If optional tool or knowledge-source fields are rejected, retry with a minimum payload.
        var shouldRetryMinimal = createResponse.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity;
        if (shouldRetryMinimal)
        {
            var minimalPayload = BuildCreateAgentPayload(assistantOptions, includeOptionalToolAndKnowledge: false);
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
                        assistantOptions.AgentName,
                        AgentSource,
                        OutcomeCode: "assistant_agent_created_minimal",
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
                assistantOptions.AgentName,
                AgentSource,
                OutcomeCode: "assistant_agent_create_failed",
                Summary: "Unable to create a Foundry agent in the Foundry project.",
                AgentId: null,
                Created: false,
                UsedFallbackPayload: false),
            false);
    }

    private JsonObject BuildCreateAgentPayload(FoundryAgentOptions assistantOptions, bool includeOptionalToolAndKnowledge)
    {
        var payload = new JsonObject
        {
            ["name"] = assistantOptions.AgentName,
            ["model"] = assistantOptions.Deployment,
            ["instructions"] = assistantOptions.SystemPrompt,
        };

        if (!includeOptionalToolAndKnowledge)
        {
            return payload;
        }

        var tools = new JsonArray();
        if (!string.IsNullOrWhiteSpace(assistantOptions.McpDatabaseToolName)
            && !string.IsNullOrWhiteSpace(assistantOptions.McpDatabaseToolEndpoint))
        {
            tools.Add(new JsonObject
            {
                ["type"] = "mcp",
                ["name"] = assistantOptions.McpDatabaseToolName,
                ["server_url"] = assistantOptions.McpDatabaseToolEndpoint,
            });
        }

        if (tools.Count > 0)
        {
            payload["tools"] = tools;
        }

        if (!string.IsNullOrWhiteSpace(assistantOptions.KnowledgeSourceUrl))
        {
            payload["knowledge_sources"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "url",
                    ["url"] = assistantOptions.KnowledgeSourceUrl,
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
        var assistantOptions = options.Value;
        try
        {
            using var request = new HttpRequestMessage(method, BuildAbsoluteUri(path));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add("api-key", assistantOptions.ApiKey);

            if (body is not null)
            {
                request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            }

            using var client = httpClientFactory.CreateClient(nameof(FoundryAgentRuntimeService));
            using var response = await client.SendAsync(request, cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return (false, response.StatusCode, null, Truncate(SanitizeSingleLine(responseBody), 500));
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
            return (false, null, null, Truncate(exception.Message, 500));
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
