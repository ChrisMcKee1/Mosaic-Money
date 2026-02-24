using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.Embeddings;
using MosaicMoney.Api.Domain.Ledger.Ingestion;
using MosaicMoney.Api.Domain.Ledger.Plaid;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class PlaidTransactionsSyncProcessorTests
{
    [Fact]
    public async Task ProcessNextPendingSyncAsync_PersistsTransactionsAndCompletesSyncState()
    {
        await using var dbContext = CreateDbContext();
        var protector = CreateTokenProtector();

        var household = new Household
        {
            Id = Guid.NewGuid(),
            Name = "Plaid Sync Household",
            CreatedAtUtc = DateTime.UtcNow,
        };

        dbContext.Households.Add(household);

        dbContext.PlaidItemCredentials.Add(new PlaidItemCredential
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            ItemId = "item-sync-1",
            PlaidEnvironment = "sandbox",
            AccessTokenCiphertext = protector.Protect("access-token-1"),
            AccessTokenFingerprint = PlaidAccessTokenProtector.ComputeFingerprint("access-token-1"),
            InstitutionId = "ins_test",
            Status = PlaidItemCredentialStatus.Active,
            CreatedAtUtc = DateTime.UtcNow,
            LastRotatedAtUtc = DateTime.UtcNow,
        });

        dbContext.PlaidItemSyncStates.Add(new PlaidItemSyncState
        {
            Id = Guid.NewGuid(),
            ItemId = "item-sync-1",
            PlaidEnvironment = "sandbox",
            Cursor = "cursor-0",
            SyncStatus = PlaidItemSyncStatus.Pending,
            PendingWebhookCount = 1,
            LastWebhookAtUtc = DateTime.UtcNow,
        });

        await dbContext.SaveChangesAsync();

        var tokenProvider = new StubPlaidTokenProvider(
            new PlaidTransactionsSyncPullResult(
                NextCursor: "cursor-1",
                HasMore: false,
                RequestId: "req-sync-1",
                Accounts:
                [
                    new PlaidTransactionsSyncAccount(
                        PlaidAccountId: "plaid-account-1",
                        Name: "Plaid Checking",
                        OfficialName: "Plaid Gold Checking",
                        Mask: "0000",
                        Type: "depository",
                        Subtype: "checking"),
                ],
                Added:
                [
                    new PlaidTransactionsSyncDeltaTransaction(
                        PlaidTransactionId: "plaid-tx-sync-1",
                        PlaidAccountId: "plaid-account-1",
                        Description: "PLAID STORE",
                        MerchantName: "Plaid Store",
                        Amount: -42.75m,
                        TransactionDate: new DateOnly(2026, 2, 23),
                        RawPayloadJson: "{\"transaction_id\":\"plaid-tx-sync-1\",\"name\":\"PLAID STORE\"}",
                        Pending: false),
                ],
                Modified: [],
                RemovedTransactionIds: []));

        var embeddingQueueService = new StubEmbeddingQueueService();
        var processor = CreateProcessor(dbContext, tokenProvider, protector, embeddingQueueService);

        var result = await processor.ProcessNextPendingSyncAsync();

        Assert.True(result.ClaimedWorkItem);
        Assert.True(result.Succeeded);
        Assert.Equal(1, result.AccountsProvisionedCount);
        Assert.Equal(1, result.TransactionsIngestedCount);
        Assert.Equal(1, result.InsertedCount);

        var state = await dbContext.PlaidItemSyncStates.SingleAsync();
        Assert.Equal(PlaidItemSyncStatus.Idle, state.SyncStatus);
        Assert.Equal(0, state.PendingWebhookCount);
        Assert.Equal("cursor-1", state.Cursor);
        Assert.NotNull(state.LastSyncedAtUtc);

        var account = await dbContext.Accounts.SingleAsync();
        Assert.Equal("plaid-account-1", account.ExternalAccountKey);

        var transaction = await dbContext.EnrichedTransactions.SingleAsync();
        Assert.Equal("plaid-tx-sync-1", transaction.PlaidTransactionId);
        Assert.Equal(-42.75m, transaction.Amount);

        Assert.Equal(1, await dbContext.RawTransactionIngestionRecords.CountAsync());
        Assert.Single(embeddingQueueService.Enqueued);
    }

    [Fact]
    public async Task ProcessNextPendingSyncAsync_ReplayedWebhookSync_IsIdempotentForPersistence()
    {
        await using var dbContext = CreateDbContext();
        var protector = CreateTokenProtector();

        var household = new Household
        {
            Id = Guid.NewGuid(),
            Name = "Plaid Sync Household 2",
            CreatedAtUtc = DateTime.UtcNow,
        };

        var account = new Account
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            Name = "Linked Plaid Account",
            InstitutionName = "Plaid",
            ExternalAccountKey = "plaid-account-2",
            IsActive = true,
        };

        dbContext.Households.Add(household);
        dbContext.Accounts.Add(account);

        dbContext.PlaidItemCredentials.Add(new PlaidItemCredential
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            ItemId = "item-sync-2",
            PlaidEnvironment = "sandbox",
            AccessTokenCiphertext = protector.Protect("access-token-2"),
            AccessTokenFingerprint = PlaidAccessTokenProtector.ComputeFingerprint("access-token-2"),
            InstitutionId = "ins_test",
            Status = PlaidItemCredentialStatus.Active,
            CreatedAtUtc = DateTime.UtcNow,
            LastRotatedAtUtc = DateTime.UtcNow,
        });

        dbContext.PlaidItemSyncStates.Add(new PlaidItemSyncState
        {
            Id = Guid.NewGuid(),
            ItemId = "item-sync-2",
            PlaidEnvironment = "sandbox",
            Cursor = "cursor-static",
            SyncStatus = PlaidItemSyncStatus.Pending,
            PendingWebhookCount = 2,
            LastWebhookAtUtc = DateTime.UtcNow,
        });

        await dbContext.SaveChangesAsync();

        var replayResult = new PlaidTransactionsSyncPullResult(
            NextCursor: "cursor-static",
            HasMore: false,
            RequestId: "req-sync-replay",
            Accounts:
            [
                new PlaidTransactionsSyncAccount(
                    PlaidAccountId: "plaid-account-2",
                    Name: "Plaid Checking",
                    OfficialName: null,
                    Mask: "0001",
                    Type: "depository",
                    Subtype: "checking"),
            ],
            Added:
            [
                new PlaidTransactionsSyncDeltaTransaction(
                    PlaidTransactionId: "plaid-tx-replay-1",
                    PlaidAccountId: "plaid-account-2",
                    Description: "REPLAYED TRANSACTION",
                    MerchantName: "Replay Merchant",
                    Amount: -9.50m,
                    TransactionDate: new DateOnly(2026, 2, 23),
                    RawPayloadJson: "{\"transaction_id\":\"plaid-tx-replay-1\",\"name\":\"REPLAYED TRANSACTION\"}",
                    Pending: false),
            ],
            Modified: [],
            RemovedTransactionIds: []);

        var tokenProvider = new StubPlaidTokenProvider(replayResult, replayResult);
        var embeddingQueueService = new StubEmbeddingQueueService();
        var processor = CreateProcessor(dbContext, tokenProvider, protector, embeddingQueueService);

        var first = await processor.ProcessNextPendingSyncAsync();
        var second = await processor.ProcessNextPendingSyncAsync();

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);

        Assert.Equal(1, await dbContext.EnrichedTransactions.CountAsync());
        Assert.Equal(1, await dbContext.RawTransactionIngestionRecords.CountAsync());

        var state = await dbContext.PlaidItemSyncStates.SingleAsync();
        Assert.Equal(PlaidItemSyncStatus.Idle, state.SyncStatus);
        Assert.Equal(0, state.PendingWebhookCount);
        Assert.Equal("cursor-static", state.Cursor);
    }

    [Fact]
    public async Task ProcessNextPendingSyncAsync_NoAccountMappings_FailsClosedWithoutCursorAdvance()
    {
        await using var dbContext = CreateDbContext();
        var protector = CreateTokenProtector();

        dbContext.PlaidItemCredentials.Add(new PlaidItemCredential
        {
            Id = Guid.NewGuid(),
            HouseholdId = null,
            ItemId = "item-sync-no-mapping",
            PlaidEnvironment = "sandbox",
            AccessTokenCiphertext = protector.Protect("access-token-no-mapping"),
            AccessTokenFingerprint = PlaidAccessTokenProtector.ComputeFingerprint("access-token-no-mapping"),
            InstitutionId = "ins_test",
            Status = PlaidItemCredentialStatus.Active,
            CreatedAtUtc = DateTime.UtcNow,
            LastRotatedAtUtc = DateTime.UtcNow,
        });

        dbContext.PlaidItemSyncStates.Add(new PlaidItemSyncState
        {
            Id = Guid.NewGuid(),
            ItemId = "item-sync-no-mapping",
            PlaidEnvironment = "sandbox",
            Cursor = "cursor-before",
            SyncStatus = PlaidItemSyncStatus.Pending,
            PendingWebhookCount = 1,
            LastWebhookAtUtc = DateTime.UtcNow,
        });

        await dbContext.SaveChangesAsync();

        var tokenProvider = new StubPlaidTokenProvider(
            new PlaidTransactionsSyncPullResult(
                NextCursor: "cursor-after",
                HasMore: false,
                RequestId: "req-no-mapping",
                Accounts: [],
                Added:
                [
                    new PlaidTransactionsSyncDeltaTransaction(
                        PlaidTransactionId: "plaid-tx-unmapped-1",
                        PlaidAccountId: "plaid-account-unmapped",
                        Description: "UNMAPPED TRANSACTION",
                        MerchantName: "Unmapped Merchant",
                        Amount: -15.25m,
                        TransactionDate: new DateOnly(2026, 2, 23),
                        RawPayloadJson: "{\"transaction_id\":\"plaid-tx-unmapped-1\"}",
                        Pending: false),
                ],
                Modified: [],
                RemovedTransactionIds: []));

        var embeddingQueueService = new StubEmbeddingQueueService();
        var processor = CreateProcessor(dbContext, tokenProvider, protector, embeddingQueueService);

        var result = await processor.ProcessNextPendingSyncAsync();

        Assert.True(result.ClaimedWorkItem);
        Assert.False(result.Succeeded);
        Assert.Equal("plaid_sync_account_mapping_missing", result.ErrorCode);

        var state = await dbContext.PlaidItemSyncStates.SingleAsync();
        Assert.Equal("cursor-before", state.Cursor);
        Assert.Equal(PlaidItemSyncStatus.Pending, state.SyncStatus);
        Assert.Equal("plaid_sync_account_mapping_missing", state.LastSyncErrorCode);
        Assert.Equal(1, state.PendingWebhookCount);

        Assert.Equal(0, await dbContext.RawTransactionIngestionRecords.CountAsync());
        Assert.Equal(0, await dbContext.EnrichedTransactions.CountAsync());
    }

    private static PlaidTransactionsSyncProcessor CreateProcessor(
        MosaicMoneyDbContext dbContext,
        IPlaidTokenProvider tokenProvider,
        PlaidAccessTokenProtector tokenProtector,
        ITransactionEmbeddingQueueService embeddingQueueService)
    {
        return new PlaidTransactionsSyncProcessor(
            dbContext,
            new PlaidItemSyncStateService(dbContext),
            new PlaidDeltaIngestionService(dbContext),
            tokenProtector,
            tokenProvider,
            embeddingQueueService,
            NullLogger<PlaidTransactionsSyncProcessor>.Instance);
    }

    private static PlaidAccessTokenProtector CreateTokenProtector()
    {
        var services = new ServiceCollection();
        services.AddDataProtection();
        using var provider = services.BuildServiceProvider();
        return new PlaidAccessTokenProtector(provider.GetRequiredService<IDataProtectionProvider>());
    }

    private static MosaicMoneyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MosaicMoneyDbContext>()
            .UseInMemoryDatabase($"mosaicmoney-plaid-sync-processor-tests-{Guid.NewGuid()}")
            .Options;

        return new MosaicMoneyDbContext(options);
    }

    private sealed class StubPlaidTokenProvider(params PlaidTransactionsSyncPullResult[] pullResults) : IPlaidTokenProvider
    {
        private readonly Queue<PlaidTransactionsSyncPullResult> results = new(pullResults);

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
            if (results.Count == 0)
            {
                throw new InvalidOperationException("No pull result configured.");
            }

            return Task.FromResult(results.Dequeue());
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

    private sealed class StubEmbeddingQueueService : ITransactionEmbeddingQueueService
    {
        public List<Guid> Enqueued { get; } = [];

        public Task<int> EnqueueTransactionsAsync(IReadOnlyCollection<Guid> transactionIds, CancellationToken cancellationToken = default)
        {
            Enqueued.AddRange(transactionIds);
            return Task.FromResult(transactionIds.Count);
        }
    }
}
