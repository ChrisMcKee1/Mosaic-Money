using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MosaicMoney.Api.Domain.Ledger.Classification;

public sealed record FoundryAgentLifecycleResult(
    bool Succeeded,
    string Operation,
    string AgentName,
    HttpStatusCode? StatusCode,
    string Message);

public interface IFoundryAgentLifecycleClient
{
    Task<FoundryAgentLifecycleResult> CreateAgentIfMissingAsync(
        string agentName,
        string instructions,
        CancellationToken cancellationToken = default);

    Task<FoundryAgentLifecycleResult> GetAgentAsync(
        string agentName,
        CancellationToken cancellationToken = default);

    Task<FoundryAgentLifecycleResult> DeleteAgentAsync(
        string agentName,
        CancellationToken cancellationToken = default);
}

public sealed class FoundryAgentLifecycleClient(
    IHttpClientFactory httpClientFactory,
    IOptions<FoundryClassificationOptions> options) : IFoundryAgentLifecycleClient
{
    public async Task<FoundryAgentLifecycleResult> CreateAgentIfMissingAsync(
        string agentName,
        string instructions,
        CancellationToken cancellationToken = default)
    {
        var getResult = await GetAgentAsync(agentName, cancellationToken);
        if (getResult.Succeeded)
        {
            return new FoundryAgentLifecycleResult(
                Succeeded: true,
                Operation: "create_if_missing",
                AgentName: agentName,
                StatusCode: getResult.StatusCode,
                Message: "Agent already exists.");
        }

        var foundryOptions = options.Value;
        if (!foundryOptions.IsConfigured())
        {
            return new FoundryAgentLifecycleResult(false, "create", agentName, null, "Foundry classification options are not configured.");
        }

        var requestBody = JsonSerializer.Serialize(new
        {
            name = agentName,
            model = foundryOptions.Deployment,
            instructions,
        });

        using var request = BuildRequest(
            HttpMethod.Post,
            GetAgentsEndpoint(foundryOptions.Endpoint),
            requestBody);

        using var client = httpClientFactory.CreateClient(nameof(FoundryAgentLifecycleClient));
        using var response = await client.SendAsync(request, cancellationToken);

        var message = response.IsSuccessStatusCode
            ? "Agent created."
            : await response.Content.ReadAsStringAsync(cancellationToken);

        return new FoundryAgentLifecycleResult(
            Succeeded: response.IsSuccessStatusCode,
            Operation: "create",
            AgentName: agentName,
            StatusCode: response.StatusCode,
            Message: Truncate(message, 500));
    }

    public async Task<FoundryAgentLifecycleResult> GetAgentAsync(
        string agentName,
        CancellationToken cancellationToken = default)
    {
        var foundryOptions = options.Value;
        if (!foundryOptions.IsConfigured())
        {
            return new FoundryAgentLifecycleResult(false, "get", agentName, null, "Foundry classification options are not configured.");
        }

        using var request = BuildRequest(HttpMethod.Get, GetAgentEndpoint(foundryOptions.Endpoint, agentName));
        using var client = httpClientFactory.CreateClient(nameof(FoundryAgentLifecycleClient));
        using var response = await client.SendAsync(request, cancellationToken);

        var message = response.IsSuccessStatusCode
            ? "Agent found."
            : await response.Content.ReadAsStringAsync(cancellationToken);

        return new FoundryAgentLifecycleResult(
            Succeeded: response.IsSuccessStatusCode,
            Operation: "get",
            AgentName: agentName,
            StatusCode: response.StatusCode,
            Message: Truncate(message, 500));
    }

    public async Task<FoundryAgentLifecycleResult> DeleteAgentAsync(
        string agentName,
        CancellationToken cancellationToken = default)
    {
        var foundryOptions = options.Value;
        if (!foundryOptions.IsConfigured())
        {
            return new FoundryAgentLifecycleResult(false, "delete", agentName, null, "Foundry classification options are not configured.");
        }

        using var request = BuildRequest(HttpMethod.Delete, GetAgentEndpoint(foundryOptions.Endpoint, agentName));
        using var client = httpClientFactory.CreateClient(nameof(FoundryAgentLifecycleClient));
        using var response = await client.SendAsync(request, cancellationToken);

        var message = response.IsSuccessStatusCode
            ? "Agent deleted."
            : await response.Content.ReadAsStringAsync(cancellationToken);

        return new FoundryAgentLifecycleResult(
            Succeeded: response.IsSuccessStatusCode,
            Operation: "delete",
            AgentName: agentName,
            StatusCode: response.StatusCode,
            Message: Truncate(message, 500));
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string endpoint, string? body = null)
    {
        var foundryOptions = options.Value;
        var request = new HttpRequestMessage(method, endpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("api-key", foundryOptions.ApiKey);

        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        return request;
    }

    private static string GetAgentEndpoint(string baseEndpoint, string agentName)
    {
        var trimmed = baseEndpoint.Trim().TrimEnd('/');
        return $"{trimmed}/agents/{Uri.EscapeDataString(agentName)}";
    }

    private static string GetAgentsEndpoint(string baseEndpoint)
    {
        var trimmed = baseEndpoint.Trim().TrimEnd('/');
        return $"{trimmed}/agents";
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
