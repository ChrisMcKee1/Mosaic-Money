using System.Diagnostics.Metrics;

namespace MosaicMoney.Api.Domain.Ledger.Embeddings;

internal static class TransactionEmbeddingQueueMetrics
{
    private static readonly Meter Meter = new("MosaicMoney.Api.EmbeddingQueue", "1.0.0");

    public static readonly Counter<long> Enqueued = Meter.CreateCounter<long>(
        "mosaicmoney.embedding_queue.enqueued",
        unit: "items",
        description: "Number of transaction embedding jobs enqueued.");

    public static readonly Counter<long> Processed = Meter.CreateCounter<long>(
        "mosaicmoney.embedding_queue.processed",
        unit: "items",
        description: "Number of transaction embedding jobs completed successfully.");

    public static readonly Counter<long> Retried = Meter.CreateCounter<long>(
        "mosaicmoney.embedding_queue.retried",
        unit: "items",
        description: "Number of transaction embedding jobs retried after failure.");

    public static readonly Counter<long> DeadLettered = Meter.CreateCounter<long>(
        "mosaicmoney.embedding_queue.deadlettered",
        unit: "items",
        description: "Number of transaction embedding jobs moved to dead-letter state.");

    public static readonly Counter<long> SkippedStale = Meter.CreateCounter<long>(
        "mosaicmoney.embedding_queue.skipped_stale",
        unit: "items",
        description: "Number of stale payload queue items skipped safely.");
}
