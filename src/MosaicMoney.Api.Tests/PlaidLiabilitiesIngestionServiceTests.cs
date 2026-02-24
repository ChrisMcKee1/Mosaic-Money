using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.Plaid;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class PlaidLiabilitiesIngestionServiceTests
{
    [Fact]
    public async Task ProcessDefaultUpdateWebhookAsync_UpsertsAccountsAndPersistsSnapshots()
    {
        await using var dbContext = CreateDbContext();
        var tokenProtector = CreateTokenProtector();

        var household = new Household
        {
            Id = Guid.NewGuid(),
            Name = "Liabilities Household",
            CreatedAtUtc = DateTime.UtcNow,
        };

        dbContext.Households.Add(household);
        dbContext.PlaidItemCredentials.Add(new PlaidItemCredential
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            ItemId = "item-liability-1",
            PlaidEnvironment = "sandbox",
            AccessTokenCiphertext = tokenProtector.Protect("access-token-liability-1"),
            AccessTokenFingerprint = PlaidAccessTokenProtector.ComputeFingerprint("access-token-liability-1"),
            Status = PlaidItemCredentialStatus.Active,
            CreatedAtUtc = DateTime.UtcNow,
            LastRotatedAtUtc = DateTime.UtcNow,
        });

        await dbContext.SaveChangesAsync();

        var provider = new StubPlaidTokenProvider(
            new PlaidLiabilitiesGetResult(
                RequestId: "req-liability-test-1",
                Accounts:
                [
                    new PlaidLiabilityAccount(
                        PlaidAccountId: "plaid-account-liability-1",
                        Name: "Plaid Credit Card",
                        OfficialName: "Plaid Platinum Card",
                        Mask: "1234",
                        Type: "credit",
                        Subtype: "credit card",
                        CurrentBalance: 1250.43m),
                ],
                Snapshots:
                [
                    new PlaidLiabilitySnapshot(
                        PlaidAccountId: "plaid-account-liability-1",
                        LiabilityType: "credit",
                        AsOfDate: new DateOnly(2026, 2, 1),
                        CurrentBalance: 1250.43m,
                        LastStatementBalance: 1175.20m,
                        MinimumPayment: 45.00m,
                        LastPaymentAmount: 80.00m,
                        LastPaymentDate: new DateOnly(2026, 2, 10),
                        NextPaymentDueDate: new DateOnly(2026, 3, 5),
                        Apr: 24.99m,
                        RawLiabilityJson: "{\"account_id\":\"plaid-account-liability-1\",\"last_statement_balance\":1175.20}"),
                ]));

        var service = new PlaidLiabilitiesIngestionService(dbContext, provider, tokenProtector);

        var result = await service.ProcessDefaultUpdateWebhookAsync(new ProcessPlaidLiabilitiesWebhookCommand(
            WebhookType: "LIABILITIES",
            WebhookCode: "DEFAULT_UPDATE",
            ItemId: "item-liability-1",
            Environment: "sandbox",
            ProviderRequestId: "req-webhook-1"));

        Assert.NotNull(result);
        Assert.Equal(1, result!.AccountsUpsertedCount);
        Assert.Equal(1, result.SnapshotsInsertedCount);

        var persistedAccount = await dbContext.LiabilityAccounts.SingleAsync();
        Assert.Equal(household.Id, persistedAccount.HouseholdId);
        Assert.Equal("plaid-account-liability-1", persistedAccount.PlaidAccountId);
        Assert.True(persistedAccount.IsActive);

        var persistedSnapshot = await dbContext.LiabilitySnapshots.SingleAsync();
        Assert.Equal(persistedAccount.Id, persistedSnapshot.LiabilityAccountId);
        Assert.Equal("credit", persistedSnapshot.LiabilityType);
        Assert.Equal(1250.43m, persistedSnapshot.CurrentBalance);
        Assert.Equal(1175.20m, persistedSnapshot.LastStatementBalance);
        Assert.Equal(new DateOnly(2026, 3, 5), persistedSnapshot.NextPaymentDueDate);
        Assert.Equal(24.9900m, persistedSnapshot.Apr);
    }

    [Fact]
    public async Task ProcessDefaultUpdateWebhookAsync_DeduplicatesSnapshotPayloadAcrossReplays()
    {
        await using var dbContext = CreateDbContext();
        var tokenProtector = CreateTokenProtector();

        dbContext.PlaidItemCredentials.Add(new PlaidItemCredential
        {
            Id = Guid.NewGuid(),
            HouseholdId = null,
            ItemId = "item-liability-2",
            PlaidEnvironment = "sandbox",
            AccessTokenCiphertext = tokenProtector.Protect("access-token-liability-2"),
            AccessTokenFingerprint = PlaidAccessTokenProtector.ComputeFingerprint("access-token-liability-2"),
            Status = PlaidItemCredentialStatus.Active,
            CreatedAtUtc = DateTime.UtcNow,
            LastRotatedAtUtc = DateTime.UtcNow,
        });

        await dbContext.SaveChangesAsync();

        var resultPayload = new PlaidLiabilitiesGetResult(
            RequestId: "req-liability-replay-1",
            Accounts:
            [
                new PlaidLiabilityAccount(
                    PlaidAccountId: "plaid-account-liability-2",
                    Name: "Plaid Mortgage",
                    OfficialName: null,
                    Mask: "2222",
                    Type: "loan",
                    Subtype: "mortgage",
                    CurrentBalance: 250000.00m),
            ],
            Snapshots:
            [
                new PlaidLiabilitySnapshot(
                    PlaidAccountId: "plaid-account-liability-2",
                    LiabilityType: "mortgage",
                    AsOfDate: null,
                    CurrentBalance: 250000.00m,
                    LastStatementBalance: null,
                    MinimumPayment: 1500.00m,
                    LastPaymentAmount: 1500.00m,
                    LastPaymentDate: new DateOnly(2026, 2, 1),
                    NextPaymentDueDate: new DateOnly(2026, 3, 1),
                    Apr: 6.125m,
                    RawLiabilityJson: "{\"account_id\":\"plaid-account-liability-2\",\"minimum_payment_amount\":1500.00}"),
            ]);

        var provider = new StubPlaidTokenProvider(resultPayload, resultPayload);
        var service = new PlaidLiabilitiesIngestionService(dbContext, provider, tokenProtector);

        var first = await service.ProcessDefaultUpdateWebhookAsync(new ProcessPlaidLiabilitiesWebhookCommand(
            "LIABILITIES",
            "DEFAULT_UPDATE",
            "item-liability-2",
            "sandbox",
            "req-replay-1"));

        var second = await service.ProcessDefaultUpdateWebhookAsync(new ProcessPlaidLiabilitiesWebhookCommand(
            "LIABILITIES",
            "DEFAULT_UPDATE",
            "item-liability-2",
            "sandbox",
            "req-replay-2"));

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(1, await dbContext.LiabilityAccounts.CountAsync());
        Assert.Equal(1, await dbContext.LiabilitySnapshots.CountAsync());
    }

    private static MosaicMoneyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MosaicMoneyDbContext>()
            .UseInMemoryDatabase($"mosaicmoney-plaid-liabilities-tests-{Guid.NewGuid()}")
            .Options;

        return new MosaicMoneyDbContext(options);
    }

    private static PlaidAccessTokenProtector CreateTokenProtector()
    {
        var services = new ServiceCollection();
        services.AddDataProtection();
        using var provider = services.BuildServiceProvider();

        return new PlaidAccessTokenProtector(provider.GetRequiredService<IDataProtectionProvider>());
    }

    private sealed class StubPlaidTokenProvider(params PlaidLiabilitiesGetResult[] liabilityResults) : IPlaidTokenProvider
    {
        private readonly Queue<PlaidLiabilitiesGetResult> liabilities = new(liabilityResults);

        public Task<PlaidLinkTokenCreateResult> CreateLinkTokenAsync(
            PlaidLinkTokenCreateRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<PlaidPublicTokenExchangeResult> ExchangePublicTokenAsync(
            PlaidPublicTokenExchangeRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<PlaidTransactionsSyncBootstrapResult> BootstrapTransactionsSyncAsync(
            PlaidTransactionsSyncBootstrapRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
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
            if (liabilities.Count == 0)
            {
                throw new InvalidOperationException("No liabilities result configured.");
            }

            return Task.FromResult(liabilities.Dequeue());
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
