namespace MosaicMoney.Api.Domain.Ledger.Plaid;

public sealed class PlaidTransactionsSyncBackgroundService(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<PlaidTransactionsSyncBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Plaid transactions sync background service started.");

        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            using var scope = serviceScopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<PlaidTransactionsSyncProcessor>();
            var result = await processor.ProcessNextPendingSyncAsync(stoppingToken);

            if (!result.ClaimedWorkItem)
            {
                continue;
            }

            if (!result.Succeeded)
            {
                logger.LogWarning(
                    "Plaid sync processing failed for state {SyncStateId} with error code {ErrorCode}.",
                    result.SyncStateId,
                    result.ErrorCode);
                continue;
            }

            logger.LogInformation(
                "Processed Plaid sync state {SyncStateId}: accountsProvisioned={AccountsProvisionedCount}, ingested={TransactionsIngestedCount}, inserted={InsertedCount}, updated={UpdatedCount}, unchanged={UnchangedCount}, unmapped={UnmappedTransactionCount}, removed={RemovedTransactionCount}.",
                result.SyncStateId,
                result.AccountsProvisionedCount,
                result.TransactionsIngestedCount,
                result.InsertedCount,
                result.UpdatedCount,
                result.UnchangedCount,
                result.UnmappedTransactionCount,
                result.RemovedTransactionCount);
        }
    }
}
