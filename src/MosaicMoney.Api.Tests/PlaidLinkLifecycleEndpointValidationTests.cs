using MosaicMoney.Api.Apis;
using MosaicMoney.Api.Contracts.V1;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class PlaidLinkLifecycleEndpointValidationTests
{
    [Fact]
    public void ValidateCreateLinkTokenRequest_RejectsNonLoopbackHttpRedirectUri()
    {
        var request = new CreatePlaidLinkTokenRequest
        {
            ClientUserId = "user-1",
            RedirectUri = "http://example.com/callback",
        };

        var errors = PlaidLinkLifecycleEndpoints.ValidateCreateLinkTokenRequest(request);

        Assert.Contains(errors, x => x.Field == nameof(CreatePlaidLinkTokenRequest.RedirectUri));
    }

    [Theory]
    [InlineData("http://localhost:53832/onboarding/plaid")]
    [InlineData("http://127.0.0.1:53832/onboarding/plaid")]
    [InlineData("http://[::1]:53832/onboarding/plaid")]
    public void ValidateCreateLinkTokenRequest_AcceptsLoopbackHttpRedirectUri(string redirectUri)
    {
        var request = new CreatePlaidLinkTokenRequest
        {
            ClientUserId = "user-1",
            RedirectUri = redirectUri,
        };

        var errors = PlaidLinkLifecycleEndpoints.ValidateCreateLinkTokenRequest(request);

        Assert.DoesNotContain(errors, x => x.Field == nameof(CreatePlaidLinkTokenRequest.RedirectUri));
    }

    [Fact]
    public void ValidateLogLinkSessionEventRequest_RejectsUnknownEventType()
    {
        var request = new LogPlaidLinkSessionEventRequest
        {
            EventType = "token_leaked",
        };

        var errors = PlaidLinkLifecycleEndpoints.ValidateLogLinkSessionEventRequest(request);

        Assert.Contains(errors, x => x.Field == nameof(LogPlaidLinkSessionEventRequest.EventType));
    }

    [Fact]
    public void ValidateExchangePublicTokenRequest_RequiresContextAndJsonValidity()
    {
        var request = new ExchangePlaidPublicTokenRequest
        {
            PublicToken = "public-sandbox-token",
            ClientMetadataJson = "{invalid",
        };

        var errors = PlaidLinkLifecycleEndpoints.ValidateExchangePublicTokenRequest(request);

        Assert.Contains(errors, x => x.Field == nameof(ExchangePlaidPublicTokenRequest.LinkSessionId));
        Assert.Contains(errors, x => x.Field == nameof(ExchangePlaidPublicTokenRequest.ClientMetadataJson));
    }

    [Theory]
    [InlineData("ERROR")]
    [InlineData("PENDING_EXPIRATION")]
    [InlineData("USER_PERMISSION_REVOKED")]
    public void ValidatePlaidItemRecoveryWebhookRequest_AcceptsSupportedWebhookCodes(string webhookCode)
    {
        var request = new PlaidItemRecoveryWebhookRequest
        {
            WebhookType = "ITEM",
            WebhookCode = webhookCode,
            ItemId = "item-123",
            Environment = "sandbox",
            MetadataJson = "{\"source\":\"test\"}",
        };

        var errors = PlaidLinkLifecycleEndpoints.ValidatePlaidItemRecoveryWebhookRequest(request);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidatePlaidItemRecoveryWebhookRequest_RejectsUnsupportedTypeAndCode_AndInvalidMetadata()
    {
        var request = new PlaidItemRecoveryWebhookRequest
        {
            WebhookType = "TRANSACTIONS",
            WebhookCode = "INITIAL_UPDATE",
            ItemId = "item-123",
            Environment = "sandbox",
            MetadataJson = "{invalid",
        };

        var errors = PlaidLinkLifecycleEndpoints.ValidatePlaidItemRecoveryWebhookRequest(request);

        Assert.Contains(errors, x => x.Field == nameof(PlaidItemRecoveryWebhookRequest.WebhookType));
        Assert.Contains(errors, x => x.Field == nameof(PlaidItemRecoveryWebhookRequest.WebhookCode));
        Assert.Contains(errors, x => x.Field == nameof(PlaidItemRecoveryWebhookRequest.MetadataJson));
    }

    [Fact]
    public void ValidatePlaidTransactionsWebhookRequest_AcceptsSyncUpdatesAvailablePayload()
    {
        var request = new PlaidTransactionsWebhookRequest
        {
            WebhookType = "TRANSACTIONS",
            WebhookCode = "SYNC_UPDATES_AVAILABLE",
            ItemId = "item-123",
            Environment = "sandbox",
            Cursor = "cursor-123",
            ProviderRequestId = "req-123",
            InitialUpdateComplete = true,
            HistoricalUpdateComplete = false,
        };

        var errors = PlaidLinkLifecycleEndpoints.ValidatePlaidTransactionsWebhookRequest(request);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidatePlaidTransactionsWebhookRequest_RejectsUnsupportedTypeAndWebhookCode()
    {
        var request = new PlaidTransactionsWebhookRequest
        {
            WebhookType = "ITEM",
            WebhookCode = "INITIAL_UPDATE",
            ItemId = "item-123",
            Environment = "sandbox",
        };

        var errors = PlaidLinkLifecycleEndpoints.ValidatePlaidTransactionsWebhookRequest(request);

        Assert.Contains(errors, x => x.Field == nameof(PlaidTransactionsWebhookRequest.WebhookType));
        Assert.Contains(errors, x => x.Field == nameof(PlaidTransactionsWebhookRequest.WebhookCode));
    }
}
