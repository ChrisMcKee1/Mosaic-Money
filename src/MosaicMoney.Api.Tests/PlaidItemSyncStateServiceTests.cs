using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.Plaid;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class PlaidItemSyncStateServiceTests
{
    [Fact]
    public async Task ProcessSyncUpdatesAvailableWebhook_CreatesDurablePendingState()
    {
        await using var dbContext = CreateDbContext();
        var itemId = await SeedCredentialAsync(dbContext);
        var service = new PlaidItemSyncStateService(dbContext);

        var result = await service.ProcessSyncUpdatesAvailableWebhookAsync(
            new ProcessPlaidTransactionsSyncWebhookCommand(
                "TRANSACTIONS",
                "SYNC_UPDATES_AVAILABLE",
                itemId,
                "sandbox",
                Cursor: null,
                ProviderRequestId: "req-sync-1",
                InitialUpdateComplete: true,
                HistoricalUpdateComplete: false));

        var state = await dbContext.PlaidItemSyncStates.SingleAsync();

        Assert.NotNull(result);
        Assert.Equal(PlaidItemSyncStatus.Pending, state.SyncStatus);
        Assert.Equal(1, state.PendingWebhookCount);
        Assert.Equal("pending-initial-sync", state.Cursor);
        Assert.True(state.InitialUpdateComplete);
        Assert.False(state.HistoricalUpdateComplete);
        Assert.Equal("req-sync-1", state.LastProviderRequestId);
        Assert.NotNull(state.LastWebhookAtUtc);
    }

    [Fact]
    public async Task ProcessSyncUpdatesAvailableWebhook_DuplicateProviderRequest_DoesNotDoubleEnqueue()
    {
        await using var dbContext = CreateDbContext();
        var itemId = await SeedCredentialAsync(dbContext);
        var service = new PlaidItemSyncStateService(dbContext);

        var first = await service.ProcessSyncUpdatesAvailableWebhookAsync(
            new ProcessPlaidTransactionsSyncWebhookCommand(
                "TRANSACTIONS",
                "SYNC_UPDATES_AVAILABLE",
                itemId,
                "sandbox",
                Cursor: "cursor-1",
                ProviderRequestId: "req-dup",
                InitialUpdateComplete: false,
                HistoricalUpdateComplete: false));

        var second = await service.ProcessSyncUpdatesAvailableWebhookAsync(
            new ProcessPlaidTransactionsSyncWebhookCommand(
                "TRANSACTIONS",
                "SYNC_UPDATES_AVAILABLE",
                itemId,
                "sandbox",
                Cursor: "cursor-1",
                ProviderRequestId: "req-dup",
                InitialUpdateComplete: false,
                HistoricalUpdateComplete: false));

        var state = await dbContext.PlaidItemSyncStates.SingleAsync();

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.False(first.DuplicateRequestIgnored);
        Assert.True(second.DuplicateRequestIgnored);
        Assert.Equal(1, state.PendingWebhookCount);
        Assert.Equal(PlaidItemSyncStatus.Pending, state.SyncStatus);
    }

    [Fact]
    public async Task ClaimAndCompleteSync_TransitionsStatusAndPreservesPendingReplaySignals()
    {
        await using var dbContext = CreateDbContext();
        var itemId = await SeedCredentialAsync(dbContext);
        var service = new PlaidItemSyncStateService(dbContext);

        await service.ProcessSyncUpdatesAvailableWebhookAsync(
            new ProcessPlaidTransactionsSyncWebhookCommand(
                "TRANSACTIONS",
                "SYNC_UPDATES_AVAILABLE",
                itemId,
                "sandbox",
                Cursor: "cursor-start",
                ProviderRequestId: "req-claim-1",
                InitialUpdateComplete: false,
                HistoricalUpdateComplete: false));

        var claimed = await service.ClaimNextPendingSyncAsync();
        Assert.NotNull(claimed);

        await service.ProcessSyncUpdatesAvailableWebhookAsync(
            new ProcessPlaidTransactionsSyncWebhookCommand(
                "TRANSACTIONS",
                "SYNC_UPDATES_AVAILABLE",
                itemId,
                "sandbox",
                Cursor: "cursor-start",
                ProviderRequestId: "req-claim-2",
                InitialUpdateComplete: true,
                HistoricalUpdateComplete: true));

        var completed = await service.CompleteClaimedSyncAsync(
            new CompletePlaidItemCursorSyncCommand(
                claimed!.SyncStateId,
                NextCursor: "cursor-next",
                InitialUpdateComplete: true,
                HistoricalUpdateComplete: true,
                ProviderRequestId: "req-complete-1"));

        var state = await dbContext.PlaidItemSyncStates.SingleAsync();

        Assert.True(completed);
        Assert.Equal("cursor-next", state.Cursor);
        Assert.True(state.InitialUpdateComplete);
        Assert.True(state.HistoricalUpdateComplete);
        Assert.NotNull(state.LastSyncedAtUtc);
        Assert.Equal(PlaidItemSyncStatus.Pending, state.SyncStatus);
        Assert.Equal(1, state.PendingWebhookCount);
    }

    [Fact]
    public async Task ProcessSyncUpdatesAvailableWebhook_UnknownCredential_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();
        var service = new PlaidItemSyncStateService(dbContext);

        var result = await service.ProcessSyncUpdatesAvailableWebhookAsync(
            new ProcessPlaidTransactionsSyncWebhookCommand(
                "TRANSACTIONS",
                "SYNC_UPDATES_AVAILABLE",
                "item-missing",
                "sandbox",
                Cursor: null,
                ProviderRequestId: "req-missing",
                InitialUpdateComplete: null,
                HistoricalUpdateComplete: null));

        Assert.Null(result);
        Assert.Equal(0, await dbContext.PlaidItemSyncStates.CountAsync());
    }

    private static MosaicMoneyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MosaicMoneyDbContext>()
            .UseInMemoryDatabase($"mosaicmoney-plaid-sync-state-tests-{Guid.NewGuid()}")
            .Options;

        return new MosaicMoneyDbContext(options);
    }

    private static async Task<string> SeedCredentialAsync(MosaicMoneyDbContext dbContext)
    {
        var household = new Household
        {
            Id = Guid.NewGuid(),
            Name = "Plaid Sync Household",
            CreatedAtUtc = DateTime.UtcNow,
        };

        dbContext.Households.Add(household);

        var itemId = $"item-{Guid.NewGuid():N}";
        dbContext.PlaidItemCredentials.Add(new PlaidItemCredential
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            ItemId = itemId,
            PlaidEnvironment = "sandbox",
            AccessTokenCiphertext = "ciphertext",
            AccessTokenFingerprint = "fingerprint",
            Status = PlaidItemCredentialStatus.Active,
            CreatedAtUtc = DateTime.UtcNow,
            LastRotatedAtUtc = DateTime.UtcNow,
        });

        await dbContext.SaveChangesAsync();
        return itemId;
    }
}
