namespace MosaicMoney.Api.Domain.Ledger.Embeddings;

public interface ITransactionEmbeddingQueueService
{
    Task<int> EnqueueTransactionsAsync(IReadOnlyCollection<Guid> transactionIds, CancellationToken cancellationToken = default);
}
