namespace MosaicMoney.Api.Domain.Ledger.Embeddings;

public sealed class TransactionEmbeddingQueueBackgroundService(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<TransactionEmbeddingQueueBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private const int MaxItemsPerPoll = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Transaction embedding queue background service started.");

        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            using var scope = serviceScopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<ITransactionEmbeddingQueueProcessor>();
            var result = await processor.ProcessDueItemsAsync(MaxItemsPerPoll, stoppingToken);

            if (result.ClaimedCount == 0)
            {
                continue;
            }

            logger.LogInformation(
                "Processed embedding queue batch: claimed={ClaimedCount}, succeeded={SucceededCount}, retried={RetriedCount}, deadLettered={DeadLetteredCount}, staleSkipped={SkippedStaleCount}.",
                result.ClaimedCount,
                result.SucceededCount,
                result.RetriedCount,
                result.DeadLetteredCount,
                result.SkippedStaleCount);
        }
    }
}
