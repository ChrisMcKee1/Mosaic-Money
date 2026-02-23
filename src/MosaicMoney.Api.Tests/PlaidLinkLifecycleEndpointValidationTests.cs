using MosaicMoney.Api.Apis;
using MosaicMoney.Api.Contracts.V1;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class PlaidLinkLifecycleEndpointValidationTests
{
    [Fact]
    public void ValidateCreateLinkTokenRequest_RejectsNonHttpsRedirectUri()
    {
        var request = new CreatePlaidLinkTokenRequest
        {
            ClientUserId = "user-1",
            RedirectUri = "http://example.com/callback",
        };

        var errors = PlaidLinkLifecycleEndpoints.ValidateCreateLinkTokenRequest(request);

        Assert.Contains(errors, x => x.Field == nameof(CreatePlaidLinkTokenRequest.RedirectUri));
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
}
