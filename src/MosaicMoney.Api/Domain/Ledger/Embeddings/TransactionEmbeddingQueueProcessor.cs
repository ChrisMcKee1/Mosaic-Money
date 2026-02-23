using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Data;
using Pgvector;

namespace MosaicMoney.Api.Domain.Ledger.Embeddings;

public sealed class TransactionEmbeddingQueueProcessor(
    MosaicMoneyDbContext dbContext,
    ITransactionEmbeddingGenerator embeddingGenerator,
    ILogger<TransactionEmbeddingQueueProcessor> logger) : ITransactionEmbeddingQueueProcessor
{
    public async Task<TransactionEmbeddingQueueProcessingResult> ProcessDueItemsAsync(
        int maxItems,
        CancellationToken cancellationToken = default)
    {
        if (maxItems <= 0)
        {
            return new TransactionEmbeddingQueueProcessingResult(0, 0, 0, 0, 0);
        }

        var now = DateTime.UtcNow;
        var dueItems = await dbContext.TransactionEmbeddingQueueItems
            .Where(x => x.Status == EmbeddingQueueStatus.Pending && x.NextAttemptAtUtc <= now)
            .OrderBy(x => x.NextAttemptAtUtc)
            .ThenBy(x => x.EnqueuedAtUtc)
            .Take(maxItems)
            .ToListAsync(cancellationToken);

        if (dueItems.Count == 0)
        {
            return new TransactionEmbeddingQueueProcessingResult(0, 0, 0, 0, 0);
        }

        var succeededCount = 0;
        var retriedCount = 0;
        var deadLetteredCount = 0;
        var skippedStaleCount = 0;

        foreach (var queueItem in dueItems)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ClaimAsync(queueItem, now, cancellationToken);

            try
            {
                var transaction = await dbContext.EnrichedTransactions
                    .FirstOrDefaultAsync(x => x.Id == queueItem.TransactionId, cancellationToken);

                if (transaction is null)
                {
                    throw new InvalidOperationException("Queue item references a transaction that no longer exists.");
                }

                var currentDescriptionHash = EmbeddingTextHasher.ComputeHash(transaction.Description);
                if (!string.Equals(currentDescriptionHash, queueItem.DescriptionHash, StringComparison.Ordinal))
                {
                    MarkSucceeded(queueItem, now, "stale_payload_skipped");
                    skippedStaleCount++;
                    TransactionEmbeddingQueueMetrics.SkippedStale.Add(1);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    continue;
                }

                if (string.Equals(transaction.DescriptionEmbeddingHash, queueItem.DescriptionHash, StringComparison.Ordinal))
                {
                    MarkSucceeded(queueItem, now, null);
                    succeededCount++;
                    TransactionEmbeddingQueueMetrics.Processed.Add(1);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    continue;
                }

                var embedding = await embeddingGenerator.GenerateEmbeddingAsync(transaction.Description, cancellationToken);
                transaction.DescriptionEmbedding = new Vector(embedding);
                transaction.DescriptionEmbeddingHash = queueItem.DescriptionHash;
                transaction.LastModifiedAtUtc = now;

                MarkSucceeded(queueItem, now, null);
                succeededCount++;
                TransactionEmbeddingQueueMetrics.Processed.Add(1);

                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                var errorMessage = Truncate(ex.GetBaseException().Message, 300);
                if (queueItem.AttemptCount >= Math.Max(1, queueItem.MaxAttempts))
                {
                    queueItem.Status = EmbeddingQueueStatus.DeadLetter;
                    queueItem.DeadLetteredAtUtc = now;
                    queueItem.LastError = errorMessage;
                    queueItem.NextAttemptAtUtc = DateTime.MaxValue;
                    deadLetteredCount++;
                    TransactionEmbeddingQueueMetrics.DeadLettered.Add(1);

                    logger.LogError(
                        ex,
                        "Embedding queue item {QueueItemId} moved to dead-letter after {AttemptCount} attempt(s).",
                        queueItem.Id,
                        queueItem.AttemptCount);
                }
                else
                {
                    queueItem.Status = EmbeddingQueueStatus.Pending;
                    queueItem.LastError = errorMessage;
                    queueItem.NextAttemptAtUtc = now.Add(ResolveRetryDelay(queueItem.AttemptCount));
                    retriedCount++;
                    TransactionEmbeddingQueueMetrics.Retried.Add(1);

                    logger.LogWarning(
                        ex,
                        "Embedding queue item {QueueItemId} failed attempt {AttemptCount}; scheduled retry at {NextAttemptAtUtc}.",
                        queueItem.Id,
                        queueItem.AttemptCount,
                        queueItem.NextAttemptAtUtc);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        return new TransactionEmbeddingQueueProcessingResult(
            dueItems.Count,
            succeededCount,
            retriedCount,
            deadLetteredCount,
            skippedStaleCount);
    }

    private async Task ClaimAsync(
        TransactionEmbeddingQueueItem queueItem,
        DateTime now,
        CancellationToken cancellationToken)
    {
        queueItem.Status = EmbeddingQueueStatus.Processing;
        queueItem.AttemptCount++;
        queueItem.LastAttemptedAtUtc = now;
        queueItem.LastError = null;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void MarkSucceeded(TransactionEmbeddingQueueItem queueItem, DateTime completedAtUtc, string? marker)
    {
        queueItem.Status = EmbeddingQueueStatus.Succeeded;
        queueItem.CompletedAtUtc = completedAtUtc;
        queueItem.DeadLetteredAtUtc = null;
        queueItem.LastError = marker;
    }

    private static TimeSpan ResolveRetryDelay(int attemptCount)
    {
        var boundedAttempt = Math.Clamp(attemptCount, 1, 6);
        var seconds = Math.Pow(2, boundedAttempt);
        return TimeSpan.FromSeconds(seconds);
    }

    private static string Truncate(string message, int maxLength)
    {
        return message.Length <= maxLength
            ? message
            : message[..maxLength];
    }
}
