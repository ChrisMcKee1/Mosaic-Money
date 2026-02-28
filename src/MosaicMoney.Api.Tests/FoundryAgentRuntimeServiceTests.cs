using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MosaicMoney.Api.Domain.Assistant;
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
            Deployment = "gpt-5.3-codex",
            AgentName = "Mosaic",
            ApiVersion = "2025-05-01",
        });

        var runtime = new FoundryAgentRuntimeService(
            new StubHttpClientFactory(httpClient),
            options,
            NullLogger<FoundryAgentRuntimeService>.Instance);

        var result = await runtime.InvokeAsync(new FoundryAgentInvocationRequest(
            HouseholdId: Guid.CreateVersion7(),
            ConversationId: Guid.CreateVersion7(),
            HouseholdUserId: Guid.CreateVersion7(),
            CommandType: "assistant_message_posted",
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
        Assert.Equal("assistant_agent_create_failed", result.OutcomeCode);
        Assert.True(handler.RequestCount >= 1);
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
}
