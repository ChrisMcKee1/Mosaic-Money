using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Data;

namespace MosaicMoney.Api.Domain.Ledger.Embeddings;

public sealed class TransactionEmbeddingQueueService(
    MosaicMoneyDbContext dbContext,
    ILogger<TransactionEmbeddingQueueService> logger) : ITransactionEmbeddingQueueService
{
    private const int DefaultMaxAttempts = 5;

    public async Task<int> EnqueueTransactionsAsync(
        IReadOnlyCollection<Guid> transactionIds,
        CancellationToken cancellationToken = default)
    {
        if (transactionIds.Count == 0)
        {
            return 0;
        }

        var ids = transactionIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return 0;
        }

        var transactions = await dbContext.EnrichedTransactions
            .Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.Description })
            .ToListAsync(cancellationToken);

        if (transactions.Count == 0)
        {
            return 0;
        }

        var now = DateTime.UtcNow;
        var existing = await dbContext.TransactionEmbeddingQueueItems
            .Where(x => ids.Contains(x.TransactionId))
            .ToListAsync(cancellationToken);

        var existingByKey = existing.ToDictionary(
            x => BuildKey(x.TransactionId, x.DescriptionHash),
            x => x,
            StringComparer.Ordinal);

        var enqueuedCount = 0;
        foreach (var transaction in transactions)
        {
            var normalizedDescription = EmbeddingTextHasher.Normalize(transaction.Description);
            if (normalizedDescription.Length == 0)
            {
                continue;
            }

            var hash = EmbeddingTextHasher.ComputeHash(normalizedDescription);

            var key = BuildKey(transaction.Id, hash);
            if (!existingByKey.TryGetValue(key, out var queueItem))
            {
                queueItem = new TransactionEmbeddingQueueItem
                {
                    Id = Guid.NewGuid(),
                    TransactionId = transaction.Id,
                    DescriptionHash = hash,
                    Status = EmbeddingQueueStatus.Pending,
                    AttemptCount = 0,
                    MaxAttempts = DefaultMaxAttempts,
                    EnqueuedAtUtc = now,
                    NextAttemptAtUtc = now,
                };

                dbContext.TransactionEmbeddingQueueItems.Add(queueItem);
                existingByKey[key] = queueItem;
                enqueuedCount++;
                continue;
            }

            if (queueItem.Status == EmbeddingQueueStatus.DeadLetter)
            {
                queueItem.Status = EmbeddingQueueStatus.Pending;
                queueItem.AttemptCount = 0;
                queueItem.NextAttemptAtUtc = now;
                queueItem.DeadLetteredAtUtc = null;
                queueItem.CompletedAtUtc = null;
                queueItem.LastError = null;
                enqueuedCount++;
            }
        }

        if (enqueuedCount > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            TransactionEmbeddingQueueMetrics.Enqueued.Add(enqueuedCount);
            logger.LogInformation(
                "Queued {QueuedCount} transaction embedding jobs for {TransactionCount} transaction(s).",
                enqueuedCount,
                transactions.Count);
        }

        return enqueuedCount;
    }

    private static string BuildKey(Guid transactionId, string descriptionHash)
    {
        return string.Concat(transactionId.ToString("N"), ":", descriptionHash);
    }
}
