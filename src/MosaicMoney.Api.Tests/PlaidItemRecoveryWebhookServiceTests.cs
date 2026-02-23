using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.Plaid;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class PlaidItemRecoveryWebhookServiceTests
{
    [Fact]
    public async Task ProcessItemRecoveryWebhook_PendingExpiration_RoutesToRequiresUpdateModeWithAuditMetadata()
    {
        await using var dbContext = CreateDbContext();
        var householdId = await SeedHouseholdAsync(dbContext);
        var service = CreateLifecycleService(dbContext);

        var issued = await service.IssueLinkTokenAsync(new IssuePlaidLinkTokenCommand(
            householdId,
            "household-user-recovery-update",
            null,
            ["transactions"],
            "{\"platform\":\"web\"}"));

        var exchange = await service.ExchangePublicTokenAsync(new ExchangePlaidPublicTokenCommand(
            householdId,
            issued.LinkSessionId,
            "public-sandbox-recovery-update",
            "ins_123",
            "{\"institution\":\"Test Bank\"}"));

        var result = await service.ProcessItemRecoveryWebhookAsync(new ProcessPlaidItemRecoveryWebhookCommand(
            "ITEM",
            "PENDING_EXPIRATION",
            exchange.ItemId,
            exchange.Environment,
            "req-update-1",
            "OAUTH_LOGIN_REQUIRED",
            "ITEM_ERROR",
            "{\"delivery\":\"webhook\"}"));

        var credential = await dbContext.PlaidItemCredentials.SingleAsync(x => x.Id == exchange.CredentialId);
        var session = await dbContext.PlaidLinkSessions.SingleAsync(x => x.Id == issued.LinkSessionId);

        Assert.NotNull(result);
        Assert.Equal(PlaidItemCredentialStatus.RequiresUpdateMode, credential.Status);
        Assert.Equal("requires_update_mode", credential.RecoveryAction);
        Assert.Equal("oauth_pending_expiration", credential.RecoveryReasonCode);
        Assert.NotNull(credential.RecoverySignaledAtUtc);

        Assert.Equal(PlaidLinkSessionStatus.RequiresUpdateMode, session.Status);
        Assert.Equal("requires_update_mode", session.RecoveryAction);
        Assert.Equal("oauth_pending_expiration", session.RecoveryReasonCode);
        Assert.NotNull(session.RecoverySignaledAtUtc);
    }

    [Fact]
    public async Task ProcessItemRecoveryWebhook_RevocationSignal_RoutesToRequiresRelinkWithAuditMetadata()
    {
        await using var dbContext = CreateDbContext();
        var householdId = await SeedHouseholdAsync(dbContext);
        var service = CreateLifecycleService(dbContext);

        var issued = await service.IssueLinkTokenAsync(new IssuePlaidLinkTokenCommand(
            householdId,
            "household-user-recovery-relink",
            null,
            ["transactions"],
            null));

        var exchange = await service.ExchangePublicTokenAsync(new ExchangePlaidPublicTokenCommand(
            householdId,
            issued.LinkSessionId,
            "public-sandbox-recovery-relink",
            "ins_456",
            null));

        var result = await service.ProcessItemRecoveryWebhookAsync(new ProcessPlaidItemRecoveryWebhookCommand(
            "ITEM",
            "USER_PERMISSION_REVOKED",
            exchange.ItemId,
            exchange.Environment,
            "req-relink-1",
            "USER_PERMISSION_REVOKED",
            "ITEM_ERROR",
            "{\"delivery\":\"webhook\"}"));

        var credential = await dbContext.PlaidItemCredentials.SingleAsync(x => x.Id == exchange.CredentialId);
        var session = await dbContext.PlaidLinkSessions.SingleAsync(x => x.Id == issued.LinkSessionId);

        Assert.NotNull(result);
        Assert.Equal(PlaidItemCredentialStatus.RequiresRelink, credential.Status);
        Assert.Equal("requires_relink", credential.RecoveryAction);
        Assert.Equal("item_permission_revoked", credential.RecoveryReasonCode);
        Assert.NotNull(credential.RecoverySignaledAtUtc);

        Assert.Equal(PlaidLinkSessionStatus.RequiresRelink, session.Status);
        Assert.Equal("requires_relink", session.RecoveryAction);
        Assert.Equal("item_permission_revoked", session.RecoveryReasonCode);
        Assert.NotNull(session.RecoverySignaledAtUtc);
    }

    [Fact]
    public async Task ProcessItemRecoveryWebhook_AmbiguousError_FailsClosedToNeedsReview()
    {
        await using var dbContext = CreateDbContext();
        var householdId = await SeedHouseholdAsync(dbContext);
        var service = CreateLifecycleService(dbContext);

        var issued = await service.IssueLinkTokenAsync(new IssuePlaidLinkTokenCommand(
            householdId,
            "household-user-recovery-review",
            null,
            ["transactions"],
            null));

        var exchange = await service.ExchangePublicTokenAsync(new ExchangePlaidPublicTokenCommand(
            householdId,
            issued.LinkSessionId,
            "public-sandbox-recovery-review",
            "ins_789",
            null));

        var result = await service.ProcessItemRecoveryWebhookAsync(new ProcessPlaidItemRecoveryWebhookCommand(
            "ITEM",
            "ERROR",
            exchange.ItemId,
            exchange.Environment,
            "req-review-1",
            null,
            "ITEM_ERROR",
            "{\"delivery\":\"webhook\"}"));

        var credential = await dbContext.PlaidItemCredentials.SingleAsync(x => x.Id == exchange.CredentialId);
        var session = await dbContext.PlaidLinkSessions.SingleAsync(x => x.Id == issued.LinkSessionId);

        Assert.NotNull(result);
        Assert.Equal(PlaidItemCredentialStatus.NeedsReview, credential.Status);
        Assert.Equal("needs_review", credential.RecoveryAction);
        Assert.Equal("item_error_ambiguous", credential.RecoveryReasonCode);

        Assert.Equal(PlaidLinkSessionStatus.NeedsReview, session.Status);
        Assert.Equal("needs_review", session.RecoveryAction);
        Assert.Equal("item_error_ambiguous", session.RecoveryReasonCode);
    }

    private static PlaidLinkLifecycleService CreateLifecycleService(MosaicMoneyDbContext dbContext)
    {
        var options = CreatePlaidOptions();
        var provider = new DeterministicPlaidTokenProvider(Options.Create(options));
        var tokenProtector = new PlaidAccessTokenProtector(CreateDataProtectionProvider());

        return new PlaidLinkLifecycleService(
            dbContext,
            provider,
            tokenProtector,
            Options.Create(options));
    }

    private static IDataProtectionProvider CreateDataProtectionProvider()
    {
        var services = new ServiceCollection();
        services.AddDataProtection();
        return services.BuildServiceProvider().GetRequiredService<IDataProtectionProvider>();
    }

    private static PlaidOptions CreatePlaidOptions()
    {
        return new PlaidOptions
        {
            Environment = "sandbox",
            ClientId = "test-client-id",
            Secret = "test-secret",
            Products = ["transactions"],
            CountryCodes = ["US"],
        };
    }

    private static MosaicMoneyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MosaicMoneyDbContext>()
            .UseInMemoryDatabase($"mosaicmoney-plaid-webhook-tests-{Guid.NewGuid()}")
            .Options;

        return new MosaicMoneyDbContext(options);
    }

    private static async Task<Guid> SeedHouseholdAsync(MosaicMoneyDbContext dbContext)
    {
        var household = new Household
        {
            Id = Guid.NewGuid(),
            Name = "Plaid Recovery Household",
            CreatedAtUtc = DateTime.UtcNow,
        };

        dbContext.Households.Add(household);
        await dbContext.SaveChangesAsync();

        return household.Id;
    }
}
