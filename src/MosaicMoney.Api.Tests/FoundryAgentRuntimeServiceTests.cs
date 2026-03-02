using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MosaicMoney.Api.Domain.Agent;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class FoundryAgentRuntimeServiceTests
{
    [Fact]
    public async Task InvokeAsync_ReturnsNeedsReview_WhenFoundryIsUnavailable()
    {
        var handler = new UnavailableHttpMessageHandler();
        var httpClient = new HttpClient(handler);

        var options = Options.Create(new FoundryAgentOptions
        {
            Enabled = true,
            Endpoint = "https://foundry.tests.mosaic-money.local/projects/test-project",
            ApiKey = "test-api-key",
            CreateAgentIfMissing = true,
            Deployment = "gpt-5.3-codex",
            AgentName = "Mosaic",
            ApiVersion = "v1",
        });

        var runtime = new FoundryAgentRuntimeService(
            new StubHttpClientFactory(httpClient),
            options,
            NullLogger<FoundryAgentRuntimeService>.Instance);

        var result = await runtime.InvokeAsync(new FoundryAgentInvocationRequest(
            HouseholdId: Guid.CreateVersion7(),
            ConversationId: Guid.CreateVersion7(),
            HouseholdUserId: Guid.CreateVersion7(),
            CommandType: "agent_message_posted",
            Message: "Can you email my landlord?",
            UserNote: "Need confirmation first",
            PolicyDisposition: "approval_required",
            ApprovalId: null,
            ApprovalDecision: null,
            ApprovalRationale: null));

        Assert.False(result.Succeeded);
        Assert.True(result.NeedsReview);
        Assert.Equal("Mosaic", result.AgentName);
        Assert.Equal("foundry", result.AgentSource);
        Assert.Equal("approval_required", result.AssignmentHint);
        Assert.Equal("agent_runtime_unavailable", result.OutcomeCode);
        Assert.True(handler.RequestCount >= 1);
    }

    [Fact]
    public async Task InvokeAsync_RoutesGreetingThroughFoundry_WhenMessageIsSimpleSalutation()
    {
        var handler = new UnavailableHttpMessageHandler();
        var httpClient = new HttpClient(handler);

        var options = Options.Create(new FoundryAgentOptions
        {
            Enabled = true,
            Endpoint = "https://foundry.tests.mosaic-money.local/projects/test-project",
            ApiKey = "test-api-key",
            CreateAgentIfMissing = true,
            Deployment = "gpt-5.3-codex",
            AgentName = "Mosaic",
            ApiVersion = "v1",
        });

        var runtime = new FoundryAgentRuntimeService(
            new StubHttpClientFactory(httpClient),
            options,
            NullLogger<FoundryAgentRuntimeService>.Instance);

        var result = await runtime.InvokeAsync(new FoundryAgentInvocationRequest(
            HouseholdId: Guid.CreateVersion7(),
            ConversationId: Guid.CreateVersion7(),
            HouseholdUserId: Guid.CreateVersion7(),
            CommandType: "agent_message_posted",
            Message: "Hi",
            UserNote: null,
            PolicyDisposition: "advisory_only",
            ApprovalId: null,
            ApprovalDecision: null,
            ApprovalRationale: null));

        Assert.False(result.Succeeded);
        Assert.True(result.NeedsReview);
        Assert.Equal("agent_runtime_unavailable", result.OutcomeCode);
        Assert.Equal("advisory_only", result.AssignmentHint);
        Assert.Null(result.ResponseSummary);
        Assert.True(handler.RequestCount >= 1);
    }

    [Fact]
    public async Task InvokeAsync_ParsesResponsesApiOutputTextFromMessageContent()
    {
        var handler = new ResponsesApiSuccessMessageHandler();
        var httpClient = new HttpClient(handler);

        var options = Options.Create(new FoundryAgentOptions
        {
            Enabled = true,
            Endpoint = "https://foundry.tests.mosaic-money.local/projects/test-project",
            ApiKey = "test-api-key",
            CreateAgentIfMissing = false,
            Deployment = "gpt-5.3-codex",
            AgentName = "Mosaic",
            ApiVersion = "v1",
        });

        var runtime = new FoundryAgentRuntimeService(
            new StubHttpClientFactory(httpClient),
            options,
            NullLogger<FoundryAgentRuntimeService>.Instance);

        var result = await runtime.InvokeAsync(new FoundryAgentInvocationRequest(
            HouseholdId: Guid.CreateVersion7(),
            ConversationId: Guid.CreateVersion7(),
            HouseholdUserId: Guid.CreateVersion7(),
            CommandType: "agent_message_posted",
            Message: "Hi",
            UserNote: null,
            PolicyDisposition: "advisory_only",
            ApprovalId: null,
            ApprovalDecision: null,
            ApprovalRationale: null));

        Assert.True(result.Succeeded);
        Assert.False(result.NeedsReview);
        Assert.Equal("agent_run_completed", result.OutcomeCode);
        Assert.NotNull(result.ResponseSummary);
        Assert.Contains("Foundry says hello", result.ResponseSummary);
        Assert.Contains(handler.RequestedPaths, path => path.EndsWith("/agents", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(handler.RequestedPaths, path => path.EndsWith("/openai/v1/responses", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class StubHttpClientFactory(HttpClient httpClient) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return httpClient;
        }
    }

    private sealed class UnavailableHttpMessageHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("{\"error\":\"service_unavailable\"}", Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class ResponsesApiSuccessMessageHandler : HttpMessageHandler
    {
        public List<string> RequestedPaths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            RequestedPaths.Add(path);

            if (request.Method == HttpMethod.Get && path.EndsWith("/agents", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"data\":[{\"id\":\"agent_123\",\"name\":\"Mosaic\",\"version\":1}]}",
                        Encoding.UTF8,
                        "application/json"),
                });
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/openai/v1/responses", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"id\":\"resp_1\",\"output\":[{\"type\":\"message\",\"role\":\"assistant\",\"content\":[{\"type\":\"output_text\",\"text\":\"Foundry says hello.\"}]}]}",
                        Encoding.UTF8,
                        "application/json"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"error\":\"not_found\"}", Encoding.UTF8, "application/json"),
            });
        }
    }
}
