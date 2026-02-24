using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Data;

namespace MosaicMoney.Api.Domain.Ledger.Plaid;

public sealed record ProcessPlaidTransactionsSyncWebhookCommand(
    string WebhookType,
    string WebhookCode,
    string ItemId,
    string Environment,
    string? Cursor,
    string? ProviderRequestId,
    bool? InitialUpdateComplete,
    bool? HistoricalUpdateComplete);

public sealed record ProcessPlaidTransactionsSyncWebhookResult(
    Guid SyncStateId,
    string ItemId,
    string Environment,
    string Cursor,
    PlaidItemSyncStatus SyncStatus,
    int PendingWebhookCount,
    bool InitialUpdateComplete,
    bool HistoricalUpdateComplete,
    DateTime ProcessedAtUtc,
    DateTime? LastWebhookAtUtc,
    string? LastProviderRequestId,
    bool DuplicateRequestIgnored);

public sealed record PlaidItemCursorSyncWorkItem(
    Guid SyncStateId,
    string ItemId,
    string Environment,
    string Cursor,
    bool InitialUpdateComplete,
    bool HistoricalUpdateComplete,
    int RemainingPendingWebhookCount,
    DateTime? LastWebhookAtUtc);

public sealed record CompletePlaidItemCursorSyncCommand(
    Guid SyncStateId,
    string? NextCursor,
    bool InitialUpdateComplete,
    bool HistoricalUpdateComplete,
    string? ProviderRequestId);

public sealed record FailPlaidItemCursorSyncCommand(
    Guid SyncStateId,
    string? ErrorCode);

public sealed class PlaidItemSyncStateService(MosaicMoneyDbContext dbContext)
{
    private const string DefaultCursorPlaceholder = "pending-initial-sync";

    public async Task<ProcessPlaidTransactionsSyncWebhookResult?> ProcessSyncUpdatesAvailableWebhookAsync(
        ProcessPlaidTransactionsSyncWebhookCommand command,
        CancellationToken cancellationToken = default)
    {
        var normalizedEnvironment = NormalizeEnvironment(command.Environment);
        var normalizedItemId = command.ItemId.Trim();
        var normalizedProviderRequestId = NormalizeOptional(command.ProviderRequestId);

        var credentialExists = await dbContext.PlaidItemCredentials
            .AsNoTracking()
            .AnyAsync(
                x => x.PlaidEnvironment == normalizedEnvironment && x.ItemId == normalizedItemId,
                cancellationToken);

        if (!credentialExists)
        {
            return null;
        }

        var state = await dbContext.PlaidItemSyncStates
            .FirstOrDefaultAsync(
                x => x.PlaidEnvironment == normalizedEnvironment && x.ItemId == normalizedItemId,
                cancellationToken);

        var now = DateTime.UtcNow;
        if (state is null)
        {
            state = new PlaidItemSyncState
            {
                Id = Guid.NewGuid(),
                ItemId = normalizedItemId,
                PlaidEnvironment = normalizedEnvironment,
                Cursor = ResolveCursor(command.Cursor, null),
                SyncStatus = PlaidItemSyncStatus.Idle,
                PendingWebhookCount = 0,
            };

            dbContext.PlaidItemSyncStates.Add(state);
        }

        var duplicateRequest = normalizedProviderRequestId is not null
            && string.Equals(state.LastProviderRequestId, normalizedProviderRequestId, StringComparison.Ordinal);

        state.Cursor = ResolveCursor(command.Cursor, state.Cursor);
        state.InitialUpdateComplete = state.InitialUpdateComplete || (command.InitialUpdateComplete ?? false);
        state.HistoricalUpdateComplete = state.HistoricalUpdateComplete || (command.HistoricalUpdateComplete ?? false);
        state.LastWebhookAtUtc = now;

        if (normalizedProviderRequestId is not null)
        {
            state.LastProviderRequestId = normalizedProviderRequestId;
        }

        if (!duplicateRequest)
        {
            state.PendingWebhookCount++;
            if (state.SyncStatus != PlaidItemSyncStatus.Processing)
            {
                state.SyncStatus = PlaidItemSyncStatus.Pending;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ProcessPlaidTransactionsSyncWebhookResult(
            state.Id,
            state.ItemId,
            state.PlaidEnvironment,
            state.Cursor,
            state.SyncStatus,
            state.PendingWebhookCount,
            state.InitialUpdateComplete,
            state.HistoricalUpdateComplete,
            now,
            state.LastWebhookAtUtc,
            state.LastProviderRequestId,
            duplicateRequest);
    }

    public async Task<PlaidItemCursorSyncWorkItem?> ClaimNextPendingSyncAsync(CancellationToken cancellationToken = default)
    {
        var state = await dbContext.PlaidItemSyncStates
            .OrderBy(x => x.LastWebhookAtUtc ?? DateTime.MinValue)
            .ThenBy(x => x.Id)
            .FirstOrDefaultAsync(x => x.SyncStatus == PlaidItemSyncStatus.Pending, cancellationToken);

        if (state is null)
        {
            return null;
        }

        state.SyncStatus = PlaidItemSyncStatus.Processing;
        if (state.PendingWebhookCount > 0)
        {
            state.PendingWebhookCount--;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new PlaidItemCursorSyncWorkItem(
            state.Id,
            state.ItemId,
            state.PlaidEnvironment,
            state.Cursor,
            state.InitialUpdateComplete,
            state.HistoricalUpdateComplete,
            state.PendingWebhookCount,
            state.LastWebhookAtUtc);
    }

    public async Task<bool> CompleteClaimedSyncAsync(
        CompletePlaidItemCursorSyncCommand command,
        CancellationToken cancellationToken = default)
    {
        var state = await dbContext.PlaidItemSyncStates
            .FirstOrDefaultAsync(x => x.Id == command.SyncStateId, cancellationToken);

        if (state is null)
        {
            return false;
        }

        state.Cursor = ResolveCursor(command.NextCursor, state.Cursor);
        state.InitialUpdateComplete = state.InitialUpdateComplete || command.InitialUpdateComplete;
        state.HistoricalUpdateComplete = state.HistoricalUpdateComplete || command.HistoricalUpdateComplete;
        state.LastSyncedAtUtc = DateTime.UtcNow;
        state.LastSyncErrorCode = null;
        state.LastSyncErrorAtUtc = null;

        var normalizedProviderRequestId = NormalizeOptional(command.ProviderRequestId);
        if (normalizedProviderRequestId is not null)
        {
            state.LastProviderRequestId = normalizedProviderRequestId;
        }

        state.SyncStatus = state.PendingWebhookCount > 0
            ? PlaidItemSyncStatus.Pending
            : PlaidItemSyncStatus.Idle;

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RecordSyncFailureAsync(
        FailPlaidItemCursorSyncCommand command,
        CancellationToken cancellationToken = default)
    {
        var state = await dbContext.PlaidItemSyncStates
            .FirstOrDefaultAsync(x => x.Id == command.SyncStateId, cancellationToken);

        if (state is null)
        {
            return false;
        }

        state.LastSyncErrorCode = string.IsNullOrWhiteSpace(command.ErrorCode)
            ? "unknown_sync_error"
            : command.ErrorCode.Trim();
        state.LastSyncErrorAtUtc = DateTime.UtcNow;

        state.PendingWebhookCount = Math.Max(1, state.PendingWebhookCount);
        state.SyncStatus = PlaidItemSyncStatus.Pending;

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string ResolveCursor(string? requestedCursor, string? existingCursor)
    {
        if (!string.IsNullOrWhiteSpace(requestedCursor))
        {
            return requestedCursor.Trim();
        }

        if (!string.IsNullOrWhiteSpace(existingCursor))
        {
            return existingCursor.Trim();
        }

        return DefaultCursorPlaceholder;
    }

    private static string NormalizeEnvironment(string environment)
    {
        return string.IsNullOrWhiteSpace(environment)
            ? "sandbox"
            : environment.Trim().ToLowerInvariant();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
