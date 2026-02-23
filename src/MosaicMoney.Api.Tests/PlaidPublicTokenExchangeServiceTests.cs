using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MosaicMoney.Api.Contracts.V1;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.Plaid;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class PlaidPublicTokenExchangeServiceTests
{
    [Fact]
    public async Task IssueLinkTokenAndSessionEvent_PersistsMetadataForDiagnostics()
    {
        await using var dbContext = CreateDbContext();
        var householdId = await SeedHouseholdAsync(dbContext);

        var service = CreateLifecycleService(dbContext);

        var issued = await service.IssueLinkTokenAsync(new IssuePlaidLinkTokenCommand(
            householdId,
            "household-user-1",
            "https://app.mosaicmoney.test/plaid/oauth",
            ["transactions"],
            "{\"platform\":\"web\"}"));

        var logged = await service.LogLinkSessionEventAsync(new LogPlaidLinkSessionEventCommand(
            issued.LinkSessionId,
            "SUCCESS",
            "client",
            "{\"institution_id\":\"ins_123\"}"));

        var session = await dbContext.PlaidLinkSessions
            .Include(x => x.Events)
            .SingleAsync(x => x.Id == issued.LinkSessionId);

        Assert.True(issued.OAuthEnabled);
        Assert.NotNull(logged);
        Assert.Equal(PlaidLinkSessionStatus.Success, session.Status);
        Assert.Equal("{\"institution_id\":\"ins_123\"}", session.LastClientMetadataJson);
        Assert.NotEqual(issued.LinkToken, session.LinkTokenHash);
        Assert.Equal(2, session.Events.Count);
    }

    [Fact]
    public async Task ExchangePublicToken_StoresProtectedCredential_AndResponseContractOmitsAccessToken()
    {
        await using var dbContext = CreateDbContext();
        var householdId = await SeedHouseholdAsync(dbContext);

        var options = CreatePlaidOptions();
        var provider = new DeterministicPlaidTokenProvider(Options.Create(options));
        var lifecycleService = CreateLifecycleService(dbContext, provider, options);

        var issued = await lifecycleService.IssueLinkTokenAsync(new IssuePlaidLinkTokenCommand(
            householdId,
            "household-user-2",
            null,
            ["transactions"],
            "{\"platform\":\"web\"}"));

        var exchange = await lifecycleService.ExchangePublicTokenAsync(new ExchangePlaidPublicTokenCommand(
            householdId,
            issued.LinkSessionId,
            "public-sandbox-123",
            "ins_123",
            "{\"institution\":\"Test Bank\"}"));

        var persistedCredential = await dbContext.PlaidItemCredentials.SingleAsync(x => x.Id == exchange.CredentialId);
        var session = await dbContext.PlaidLinkSessions.SingleAsync(x => x.Id == issued.LinkSessionId);

        var providerResult = await provider.ExchangePublicTokenAsync(new PlaidPublicTokenExchangeRequest(
            "public-sandbox-123",
            options.Environment,
            "ins_123",
            null));

        Assert.NotEqual(providerResult.AccessToken, persistedCredential.AccessTokenCiphertext);
        Assert.Equal(64, persistedCredential.AccessTokenFingerprint.Length);
        Assert.Equal(PlaidLinkSessionStatus.Exchanged, session.Status);

        var responseDto = new PlaidPublicTokenExchangeResultDto(
            exchange.CredentialId,
            exchange.LinkSessionId,
            exchange.ItemId,
            exchange.Environment,
            exchange.Status.ToString(),
            exchange.InstitutionId,
            exchange.StoredAtUtc);

        var json = JsonSerializer.Serialize(responseDto);
        Assert.DoesNotContain("accessToken", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("access_token", json, StringComparison.OrdinalIgnoreCase);
    }

    private static PlaidLinkLifecycleService CreateLifecycleService(
        MosaicMoneyDbContext dbContext,
        IPlaidTokenProvider? provider = null,
        PlaidOptions? options = null)
    {
        options ??= CreatePlaidOptions();

        provider ??= new DeterministicPlaidTokenProvider(Options.Create(options));
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
            .UseInMemoryDatabase($"mosaicmoney-plaid-tests-{Guid.NewGuid()}")
            .Options;

        return new MosaicMoneyDbContext(options);
    }

    private static async Task<Guid> SeedHouseholdAsync(MosaicMoneyDbContext dbContext)
    {
        var household = new Household
        {
            Id = Guid.NewGuid(),
            Name = "Plaid Household",
            CreatedAtUtc = DateTime.UtcNow,
        };

        dbContext.Households.Add(household);
        await dbContext.SaveChangesAsync();

        return household.Id;
    }
}
