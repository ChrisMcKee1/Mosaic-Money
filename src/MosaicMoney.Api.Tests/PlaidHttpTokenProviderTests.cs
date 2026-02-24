using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MosaicMoney.Api.Domain.Ledger.Plaid;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class PlaidHttpTokenProviderTests
{
    [Fact]
    public async Task CreateLinkTokenAsync_UsesConfiguredHeadersAndPayload()
    {
        var handler = new StubHttpMessageHandler();
                handler.EnqueueResponse(HttpStatusCode.OK, @"{
    ""link_token"": ""link-sandbox-123"",
    ""expiration"": ""2026-02-24T01:02:03Z"",
    ""request_id"": ""req-link-123""
}");

        var provider = CreateProvider(handler, CreateOptions(webhookUrl: "https://example.test/plaid/webhooks"));

        var result = await provider.CreateLinkTokenAsync(new PlaidLinkTokenCreateRequest(
            ClientUserId: "client-user-1",
            Environment: "sandbox",
            RedirectUri: "http://localhost:53832/onboarding/plaid",
            Products: ["transactions"],
            CountryCodes: ["US"],
            OAuthEnabled: true,
            OAuthStateId: "state-1",
            ClientMetadataJson: null));

        Assert.Equal("link-sandbox-123", result.LinkToken);
        Assert.Equal("req-link-123", result.RequestId);
        Assert.Equal("sandbox", result.Environment);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://sandbox.plaid.com/link/token/create", request.Uri);
        Assert.Equal("test-client-id", request.Headers["PLAID-CLIENT-ID"]);
        Assert.Equal("test-secret", request.Headers["PLAID-SECRET"]);

        using var payload = JsonDocument.Parse(request.Body);
        Assert.Equal("Mosaic Money", payload.RootElement.GetProperty("client_name").GetString());
        Assert.Equal("en", payload.RootElement.GetProperty("language").GetString());
        Assert.Equal("client-user-1", payload.RootElement.GetProperty("user").GetProperty("client_user_id").GetString());
        Assert.Equal("http://localhost:53832/onboarding/plaid", payload.RootElement.GetProperty("redirect_uri").GetString());
        Assert.Equal("https://example.test/plaid/webhooks", payload.RootElement.GetProperty("webhook").GetString());
    }

    [Fact]
    public async Task ExchangePublicTokenAsync_ThrowsInvalidOperation_WhenPlaidReturnsError()
    {
        var handler = new StubHttpMessageHandler();
                handler.EnqueueResponse(HttpStatusCode.BadRequest, @"{
    ""error_type"": ""INVALID_REQUEST"",
    ""error_code"": ""INVALID_PUBLIC_TOKEN"",
    ""request_id"": ""req-error-123""
}");

        var provider = CreateProvider(handler, CreateOptions());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.ExchangePublicTokenAsync(
            new PlaidPublicTokenExchangeRequest(
                PublicToken: "public-sandbox-bad",
                Environment: "sandbox",
                InstitutionId: null,
                ClientMetadataJson: null)));

        Assert.Contains("/item/public_token/exchange", exception.Message, StringComparison.Ordinal);
        Assert.Contains("req-error-123", exception.Message, StringComparison.Ordinal);
        Assert.Contains("INVALID_PUBLIC_TOKEN", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BootstrapTransactionsSyncAsync_ReturnsNextCursorAndHasMore()
    {
        var handler = new StubHttpMessageHandler();
                handler.EnqueueResponse(HttpStatusCode.OK, @"{
    ""added"": [],
    ""modified"": [],
    ""removed"": [],
    ""next_cursor"": ""cursor-next-123"",
    ""has_more"": false,
    ""request_id"": ""req-sync-123""
}");

        var provider = CreateProvider(handler, CreateOptions());

        var result = await provider.BootstrapTransactionsSyncAsync(new PlaidTransactionsSyncBootstrapRequest(
            AccessToken: "access-sandbox-123",
            Environment: "sandbox",
            Cursor: "",
            Count: 1));

        Assert.Equal("cursor-next-123", result.NextCursor);
        Assert.False(result.HasMore);
        Assert.Equal("req-sync-123", result.RequestId);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://sandbox.plaid.com/transactions/sync", request.Uri);

        using var payload = JsonDocument.Parse(request.Body);
        Assert.Equal("access-sandbox-123", payload.RootElement.GetProperty("access_token").GetString());
        Assert.Equal(1, payload.RootElement.GetProperty("count").GetInt32());
    }

    private static PlaidHttpTokenProvider CreateProvider(StubHttpMessageHandler handler, PlaidOptions options)
    {
        return new PlaidHttpTokenProvider(
            new HttpClient(handler),
            Options.Create(options),
            NullLogger<PlaidHttpTokenProvider>.Instance);
    }

    private static PlaidOptions CreateOptions(string? webhookUrl = null)
    {
        return new PlaidOptions
        {
            Environment = "sandbox",
            ClientId = "test-client-id",
            Secret = "test-secret",
            ClientName = "Mosaic Money",
            Language = "en",
            Products = ["transactions"],
            CountryCodes = ["US"],
            WebhookUrl = webhookUrl,
        };
    }

    private sealed record CapturedRequest(string Uri, string Body, IReadOnlyDictionary<string, string> Headers);

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responses = [];

        public List<CapturedRequest> Requests { get; } = [];

        public void EnqueueResponse(HttpStatusCode statusCode, string jsonBody)
        {
            responses.Enqueue(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json"),
            });
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in request.Headers)
            {
                headers[header.Key] = string.Join(",", header.Value);
            }

            Requests.Add(new CapturedRequest(
                request.RequestUri?.ToString() ?? string.Empty,
                body,
                headers));

            if (responses.Count == 0)
            {
                throw new InvalidOperationException("No stub response was configured.");
            }

            return responses.Dequeue();
        }
    }
}
