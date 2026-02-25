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
        var syncState = await dbContext.PlaidItemSyncStates.SingleAsync(x => x.ItemId == exchange.ItemId);

        var providerResult = await provider.ExchangePublicTokenAsync(new PlaidPublicTokenExchangeRequest(
            "public-sandbox-123",
            options.Environment,
            "ins_123",
            null));

        Assert.NotEqual(providerResult.AccessToken, persistedCredential.AccessTokenCiphertext);
        Assert.Equal(64, persistedCredential.AccessTokenFingerprint.Length);
        Assert.Equal(PlaidLinkSessionStatus.Exchanged, session.Status);
        Assert.Equal(PlaidItemSyncStatus.Pending, syncState.SyncStatus);
        Assert.True(syncState.PendingWebhookCount >= 1);
        Assert.False(string.IsNullOrWhiteSpace(syncState.Cursor));

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

    [Fact]
    public async Task ExchangePublicToken_WhenBootstrapCursorModeIsStart_UsesEmptyCursorForHistoricalSyncBootstrap()
    {
        await using var dbContext = CreateDbContext();
        var householdId = await SeedHouseholdAsync(dbContext);

        var options = new PlaidOptions
        {
            Environment = "sandbox",
            ClientId = "test-client-id",
            Secret = "test-secret",
            Products = ["transactions"],
            CountryCodes = ["US"],
            TransactionsSyncBootstrapCursor = "start",
            TransactionsSyncBootstrapCount = 250,
            TransactionsHistoryDaysRequested = 999,
        };

        var provider = new CaptureBootstrapCursorPlaidTokenProvider();
        var lifecycleService = CreateLifecycleService(dbContext, provider, options);

        var exchange = await lifecycleService.ExchangePublicTokenAsync(new ExchangePlaidPublicTokenCommand(
            householdId,
            LinkSessionId: null,
            PublicToken: "public-sandbox-start-mode",
            InstitutionId: "ins_109508",
            ClientMetadataJson: null));

        Assert.NotEqual(Guid.Empty, exchange.CredentialId);
        Assert.NotNull(provider.LastBootstrapRequest);
        Assert.Equal(string.Empty, provider.LastBootstrapRequest!.Cursor);
        Assert.Equal(250, provider.LastBootstrapRequest.Count);
        Assert.Equal(730, provider.LastBootstrapRequest.DaysRequested);
    }

    [Fact]
    public async Task IssueLinkToken_WhenTransactionsHistoryDaysBelowMinimum_UsesMinimumBound()
    {
        await using var dbContext = CreateDbContext();
        var householdId = await SeedHouseholdAsync(dbContext);

        var options = new PlaidOptions
        {
            Environment = "sandbox",
            ClientId = "test-client-id",
            Secret = "test-secret",
            Products = ["transactions"],
            CountryCodes = ["US"],
            TransactionsHistoryDaysRequested = 10,
        };

        var provider = new CaptureBootstrapCursorPlaidTokenProvider();
        var lifecycleService = CreateLifecycleService(dbContext, provider, options);

        var result = await lifecycleService.IssueLinkTokenAsync(new IssuePlaidLinkTokenCommand(
            householdId,
            "client-user-days-min",
            "http://localhost:53832/onboarding/plaid",
            ["transactions"],
            null));

        Assert.NotEqual(Guid.Empty, result.LinkSessionId);
        Assert.NotNull(provider.LastCreateLinkTokenRequest);
        Assert.Equal(30, provider.LastCreateLinkTokenRequest!.TransactionsDaysRequested);
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

    private sealed class CaptureBootstrapCursorPlaidTokenProvider : IPlaidTokenProvider
    {
        public PlaidLinkTokenCreateRequest? LastCreateLinkTokenRequest { get; private set; }
        public PlaidTransactionsSyncBootstrapRequest? LastBootstrapRequest { get; private set; }

        public Task<PlaidLinkTokenCreateResult> CreateLinkTokenAsync(
            PlaidLinkTokenCreateRequest request,
            CancellationToken cancellationToken = default)
        {
            LastCreateLinkTokenRequest = request;

            return Task.FromResult(new PlaidLinkTokenCreateResult(
                LinkToken: "link-sandbox-capture-001",
                ExpiresAtUtc: DateTime.UtcNow.AddHours(4),
                Environment: request.Environment,
                Products: request.Products,
                OAuthEnabled: request.OAuthEnabled,
                RedirectUri: request.RedirectUri,
                RequestId: "req-link-capture-001"));
        }

        public Task<PlaidPublicTokenExchangeResult> ExchangePublicTokenAsync(
            PlaidPublicTokenExchangeRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PlaidPublicTokenExchangeResult(
                ItemId: "item-start-mode-001",
                AccessToken: "access-start-mode-001",
                Environment: "sandbox",
                InstitutionId: request.InstitutionId,
                RequestId: "req-start-mode-001"));
        }

        public Task<PlaidTransactionsSyncBootstrapResult> BootstrapTransactionsSyncAsync(
            PlaidTransactionsSyncBootstrapRequest request,
            CancellationToken cancellationToken = default)
        {
            LastBootstrapRequest = request;

            return Task.FromResult(new PlaidTransactionsSyncBootstrapResult(
                NextCursor: "cursor-after-bootstrap",
                HasMore: false,
                RequestId: "req-bootstrap-start-mode"));
        }

        public Task<PlaidTransactionsSyncPullResult> PullTransactionsSyncAsync(
            PlaidTransactionsSyncPullRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<PlaidLiabilitiesGetResult> GetLiabilitiesAsync(
            PlaidLiabilitiesGetRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
        public Task<PlaidTransactionsRecurringGetResult> GetTransactionsRecurringAsync(PlaidTransactionsRecurringGetRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<PlaidInvestmentsHoldingsGetResult> GetInvestmentsHoldingsAsync(PlaidInvestmentsHoldingsGetRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
