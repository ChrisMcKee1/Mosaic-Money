namespace MosaicMoney.Api.Domain.Ledger.Embeddings;

public interface ITransactionEmbeddingGenerator
{
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
}
