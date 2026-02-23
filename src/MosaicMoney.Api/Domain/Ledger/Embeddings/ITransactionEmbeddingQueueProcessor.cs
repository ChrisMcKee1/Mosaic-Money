namespace MosaicMoney.Api.Domain.Ledger.Embeddings;

public sealed record TransactionEmbeddingQueueProcessingResult(
    int ClaimedCount,
    int SucceededCount,
    int RetriedCount,
    int DeadLetteredCount,
    int SkippedStaleCount);

public interface ITransactionEmbeddingQueueProcessor
{
    Task<TransactionEmbeddingQueueProcessingResult> ProcessDueItemsAsync(int maxItems, CancellationToken cancellationToken = default);
}
