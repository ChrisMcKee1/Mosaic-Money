using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger.Embeddings;
using MosaicMoney.Api.Domain.Ledger.Ingestion;

namespace MosaicMoney.Api.Domain.Ledger.Plaid;

public sealed record PlaidTransactionsSyncProcessingResult(
    bool ClaimedWorkItem,
    bool Succeeded,
    Guid? SyncStateId,
    int AccountsProvisionedCount,
    int TransactionsIngestedCount,
    int InsertedCount,
    int UpdatedCount,
    int UnchangedCount,
    int UnmappedTransactionCount,
    int RemovedTransactionCount,
    string? ErrorCode);

public sealed class PlaidTransactionsSyncProcessor(
    MosaicMoneyDbContext dbContext,
    PlaidItemSyncStateService syncStateService,
    PlaidDeltaIngestionService ingestionService,
    PlaidAccessTokenProtector tokenProtector,
    IPlaidTokenProvider tokenProvider,
    ITransactionEmbeddingQueueService embeddingQueueService,
    ILogger<PlaidTransactionsSyncProcessor> logger)
{
    private const int TransactionsSyncPageCount = 500;
    private const int MaxMutationRetryCount = 2;

    public async Task<PlaidTransactionsSyncProcessingResult> ProcessNextPendingSyncAsync(
        CancellationToken cancellationToken = default)
    {
        var workItem = await syncStateService.ClaimNextPendingSyncAsync(cancellationToken);
        if (workItem is null)
        {
            return new PlaidTransactionsSyncProcessingResult(
                ClaimedWorkItem: false,
                Succeeded: true,
                SyncStateId: null,
                AccountsProvisionedCount: 0,
                TransactionsIngestedCount: 0,
                InsertedCount: 0,
                UpdatedCount: 0,
                UnchangedCount: 0,
                UnmappedTransactionCount: 0,
                RemovedTransactionCount: 0,
                ErrorCode: null);
        }

        try
        {
            var credential = await dbContext.PlaidItemCredentials
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.PlaidEnvironment == workItem.Environment && x.ItemId == workItem.ItemId,
                    cancellationToken);

            if (credential is null)
            {
                return await FailClaimedSyncAsync(workItem.SyncStateId, "plaid_item_credential_not_found", cancellationToken);
            }

            if (credential.Status != PlaidItemCredentialStatus.Active)
            {
                return await FailClaimedSyncAsync(workItem.SyncStateId, "plaid_item_credential_not_active", cancellationToken);
            }

            var accessToken = tokenProtector.Unprotect(credential.AccessTokenCiphertext);
            var syncPayload = await PullAllPagesAsync(workItem, accessToken, cancellationToken);

            var deltaTransactions = syncPayload.Added
                .Concat(syncPayload.Modified)
                .ToList();

            var mappingResult = await ResolveAccountMappingsAsync(
                credential,
                syncPayload.Accounts,
                deltaTransactions,
                cancellationToken);
            var accountMappings = mappingResult.AccountMappings;
            var accountsProvisionedCount = mappingResult.AccountsProvisionedCount;

            var groupedByLocalAccount = new Dictionary<Guid, List<PlaidDeltaIngestionItemInput>>();
            var unmappedTransactionCount = 0;

            foreach (var deltaTransaction in deltaTransactions)
            {
                if (!accountMappings.TryGetValue(deltaTransaction.PlaidAccountId, out var localAccountId))
                {
                    unmappedTransactionCount++;
                    continue;
                }

                if (!groupedByLocalAccount.TryGetValue(localAccountId, out var ingestionItems))
                {
                    ingestionItems = [];
                    groupedByLocalAccount[localAccountId] = ingestionItems;
                }

                ingestionItems.Add(MapToIngestionItem(deltaTransaction));
            }

            // Fail closed when all delta transactions are unmapped so we don't silently advance cursor state.
            if (deltaTransactions.Count > 0 && groupedByLocalAccount.Count == 0)
            {
                throw new PlaidSyncProcessingException(
                    "plaid_sync_account_mapping_missing",
                    $"No local account mapping exists for item {workItem.ItemId} ({workItem.Environment}).");
            }

            var insertedCount = 0;
            var updatedCount = 0;
            var unchangedCount = 0;
            var transactionIdsForEmbeddings = new HashSet<Guid>();

            foreach (var (accountId, items) in groupedByLocalAccount)
            {
                if (items.Count == 0)
                {
                    continue;
                }

                var ingestionResult = await ingestionService.IngestAsync(
                    new PlaidDeltaIngestionRequest(
                        accountId,
                        workItem.Cursor,
                        items),
                    cancellationToken);

                insertedCount += ingestionResult.InsertedCount;
                updatedCount += ingestionResult.UpdatedCount;
                unchangedCount += ingestionResult.UnchangedCount;

                foreach (var item in ingestionResult.Items)
                {
                    transactionIdsForEmbeddings.Add(item.EnrichedTransactionId);
                }
            }

            if (transactionIdsForEmbeddings.Count > 0)
            {
                try
                {
                    await embeddingQueueService.EnqueueTransactionsAsync(
                        transactionIdsForEmbeddings,
                        cancellationToken);
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    // Embedding failures must not block durable ledger ingestion.
                    logger.LogError(
                        ex,
                        "Plaid sync succeeded but embedding enqueue failed for item {ItemId}.",
                        workItem.ItemId);
                }
            }

            await syncStateService.CompleteClaimedSyncAsync(
                new CompletePlaidItemCursorSyncCommand(
                    workItem.SyncStateId,
                    syncPayload.NextCursor,
                    workItem.InitialUpdateComplete,
                    workItem.HistoricalUpdateComplete,
                    syncPayload.LastRequestId),
                cancellationToken);

            return new PlaidTransactionsSyncProcessingResult(
                ClaimedWorkItem: true,
                Succeeded: true,
                SyncStateId: workItem.SyncStateId,
                AccountsProvisionedCount: accountsProvisionedCount,
                TransactionsIngestedCount: deltaTransactions.Count - unmappedTransactionCount,
                InsertedCount: insertedCount,
                UpdatedCount: updatedCount,
                UnchangedCount: unchangedCount,
                UnmappedTransactionCount: unmappedTransactionCount,
                RemovedTransactionCount: syncPayload.RemovedTransactionIds.Count,
                ErrorCode: null);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            var errorCode = ResolveErrorCode(ex);
            logger.LogError(
                ex,
                "Failed processing Plaid sync state {SyncStateId} for item {ItemId} ({Environment}).",
                workItem.SyncStateId,
                workItem.ItemId,
                workItem.Environment);

            return await FailClaimedSyncAsync(workItem.SyncStateId, errorCode, cancellationToken);
        }
    }

    private async Task<PulledPlaidSyncPayload> PullAllPagesAsync(
        PlaidItemCursorSyncWorkItem workItem,
        string accessToken,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt <= MaxMutationRetryCount; attempt++)
        {
            try
            {
                var cursor = workItem.Cursor;
                string? lastRequestId = null;
                var accountsById = new Dictionary<string, PlaidTransactionsSyncAccount>(StringComparer.Ordinal);
                var added = new List<PlaidTransactionsSyncDeltaTransaction>();
                var modified = new List<PlaidTransactionsSyncDeltaTransaction>();
                var removedTransactionIds = new HashSet<string>(StringComparer.Ordinal);

                var hasMore = true;
                while (hasMore)
                {
                    var page = await tokenProvider.PullTransactionsSyncAsync(
                        new PlaidTransactionsSyncPullRequest(
                            accessToken,
                            workItem.Environment,
                            cursor,
                            TransactionsSyncPageCount),
                        cancellationToken);

                    lastRequestId = page.RequestId;

                    foreach (var account in page.Accounts)
                    {
                        accountsById[account.PlaidAccountId] = account;
                    }

                    added.AddRange(page.Added);
                    modified.AddRange(page.Modified);
                    foreach (var removedTransactionId in page.RemovedTransactionIds)
                    {
                        removedTransactionIds.Add(removedTransactionId);
                    }

                    if (page.HasMore && string.Equals(page.NextCursor, cursor, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Plaid transactions sync cursor did not advance during pagination.");
                    }

                    cursor = page.NextCursor;
                    hasMore = page.HasMore;
                }

                return new PulledPlaidSyncPayload(
                    cursor,
                    lastRequestId,
                    accountsById.Values.ToList(),
                    added,
                    modified,
                    removedTransactionIds.ToList());
            }
            catch (PlaidApiException ex) when (ShouldRetryPaginationMutation(ex) && attempt < MaxMutationRetryCount)
            {
                logger.LogWarning(
                    "Retrying Plaid pagination after mutation error for item {ItemId}; attempt {Attempt} of {MaxAttempts}.",
                    workItem.ItemId,
                    attempt + 1,
                    MaxMutationRetryCount + 1);
            }
        }

        throw new InvalidOperationException("Plaid transactions sync pagination failed after retry attempts.");
    }

    private async Task<(Dictionary<string, Guid> AccountMappings, int AccountsProvisionedCount)> ResolveAccountMappingsAsync(
        PlaidItemCredential credential,
        IReadOnlyCollection<PlaidTransactionsSyncAccount> syncedAccounts,
        IReadOnlyCollection<PlaidTransactionsSyncDeltaTransaction> deltaTransactions,
        CancellationToken cancellationToken)
    {
        var plaidAccountIds = deltaTransactions
            .Select(x => x.PlaidAccountId)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (plaidAccountIds.Count == 0)
        {
            return (new Dictionary<string, Guid>(StringComparer.Ordinal), 0);
        }

        var existingAccountsQuery = dbContext.Accounts
            .Where(x => x.ExternalAccountKey != null && plaidAccountIds.Contains(x.ExternalAccountKey));

        if (credential.HouseholdId.HasValue)
        {
            existingAccountsQuery = existingAccountsQuery.Where(x => x.HouseholdId == credential.HouseholdId.Value);
        }

        var existingAccounts = await existingAccountsQuery.ToListAsync(cancellationToken);
        var mappings = existingAccounts.ToDictionary(x => x.ExternalAccountKey!, x => x.Id, StringComparer.Ordinal);

        if (!credential.HouseholdId.HasValue)
        {
            return (mappings, 0);
        }

        var syncedAccountById = syncedAccounts.ToDictionary(x => x.PlaidAccountId, StringComparer.Ordinal);
        var provisionedCount = 0;

        foreach (var plaidAccountId in plaidAccountIds)
        {
            if (mappings.ContainsKey(plaidAccountId))
            {
                continue;
            }

            syncedAccountById.TryGetValue(plaidAccountId, out var syncedAccount);
            var account = new Account
            {
                Id = Guid.NewGuid(),
                HouseholdId = credential.HouseholdId.Value,
                Name = ResolveAccountName(syncedAccount, plaidAccountId),
                InstitutionName = credential.InstitutionId,
                ExternalAccountKey = plaidAccountId,
                IsActive = true,
            };

            dbContext.Accounts.Add(account);
            mappings[plaidAccountId] = account.Id;
            provisionedCount++;
        }

        if (provisionedCount > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return (mappings, provisionedCount);
    }

    private static PlaidDeltaIngestionItemInput MapToIngestionItem(PlaidTransactionsSyncDeltaTransaction deltaTransaction)
    {
        var description = string.IsNullOrWhiteSpace(deltaTransaction.MerchantName)
            ? deltaTransaction.Description
            : deltaTransaction.MerchantName;
        var transactionDate = deltaTransaction.TransactionDate ?? default;

        var isAmbiguous = string.IsNullOrWhiteSpace(description)
            || deltaTransaction.Amount == 0m
            || transactionDate == default;

        return new PlaidDeltaIngestionItemInput(
            deltaTransaction.PlaidTransactionId,
            description,
            deltaTransaction.Amount,
            transactionDate,
            deltaTransaction.RawPayloadJson,
            isAmbiguous,
            ResolveReviewReason(description, deltaTransaction.Amount, transactionDate));
    }

    private static string? ResolveReviewReason(string description, decimal amount, DateOnly transactionDate)
    {
        if (amount == 0m)
        {
            return "plaid_sync_amount_zero";
        }

        if (transactionDate == default)
        {
            return "plaid_sync_missing_transaction_date";
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return "plaid_sync_missing_description";
        }

        return null;
    }

    private static string ResolveAccountName(PlaidTransactionsSyncAccount? account, string plaidAccountId)
    {
        if (account is not null)
        {
            if (!string.IsNullOrWhiteSpace(account.Name))
            {
                return account.Name.Trim();
            }

            if (!string.IsNullOrWhiteSpace(account.OfficialName))
            {
                return account.OfficialName.Trim();
            }
        }

        var suffix = plaidAccountId.Length <= 6
            ? plaidAccountId
            : plaidAccountId[^6..];

        return $"Plaid Account {suffix}";
    }

    private static bool ShouldRetryPaginationMutation(PlaidApiException exception)
    {
        return string.Equals(
            exception.ErrorCode,
            "TRANSACTIONS_SYNC_MUTATION_DURING_PAGINATION",
            StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveErrorCode(Exception exception)
    {
        if (exception is PlaidSyncProcessingException syncProcessingException)
        {
            return syncProcessingException.ErrorCode;
        }

        if (exception is PlaidApiException plaidException && !string.IsNullOrWhiteSpace(plaidException.ErrorCode))
        {
            return plaidException.ErrorCode;
        }

        return "plaid_sync_processing_error";
    }

    private async Task<PlaidTransactionsSyncProcessingResult> FailClaimedSyncAsync(
        Guid syncStateId,
        string errorCode,
        CancellationToken cancellationToken)
    {
        await syncStateService.RecordSyncFailureAsync(
            new FailPlaidItemCursorSyncCommand(syncStateId, errorCode),
            cancellationToken);

        return new PlaidTransactionsSyncProcessingResult(
            ClaimedWorkItem: true,
            Succeeded: false,
            SyncStateId: syncStateId,
            AccountsProvisionedCount: 0,
            TransactionsIngestedCount: 0,
            InsertedCount: 0,
            UpdatedCount: 0,
            UnchangedCount: 0,
            UnmappedTransactionCount: 0,
            RemovedTransactionCount: 0,
            ErrorCode: errorCode);
    }

    private sealed record PulledPlaidSyncPayload(
        string NextCursor,
        string? LastRequestId,
        IReadOnlyList<PlaidTransactionsSyncAccount> Accounts,
        IReadOnlyList<PlaidTransactionsSyncDeltaTransaction> Added,
        IReadOnlyList<PlaidTransactionsSyncDeltaTransaction> Modified,
        IReadOnlyList<string> RemovedTransactionIds);

    private sealed class PlaidSyncProcessingException(string errorCode, string message)
        : InvalidOperationException(message)
    {
        public string ErrorCode { get; } = errorCode;
    }
}
