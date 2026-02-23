using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Data;
using Pgvector;

namespace MosaicMoney.Api.Domain.Ledger.Classification;

public static class SemanticRetrievalStatusCodes
{
    public const string Ok = "ok";
    public const string NoCandidates = "no_candidates";
    public const string ProviderNotSupported = "provider_not_supported";
    public const string TransactionNotFound = "transaction_not_found";
    public const string QueryEmbeddingMissing = "query_embedding_missing";
    public const string QueryFailed = "query_failed";
}

public sealed record SemanticRetrievalRequest(
    int MaxCandidates = 5,
    int CandidateScanLimit = 30,
    decimal MinimumNormalizedScore = 0.7000m);

public sealed record SemanticRetrievalCandidate(
    Guid ProposedSubcategoryId,
    decimal NormalizedScore,
    Guid SourceTransactionId,
    int SupportingMatchCount,
    string ProvenanceSource,
    string ProvenanceReference,
    string ProvenancePayloadJson);

public sealed record SemanticRetrievalResult(
    bool Succeeded,
    string StatusCode,
    string StatusMessage,
    IReadOnlyList<SemanticRetrievalCandidate> Candidates);

public sealed record SemanticQueryTransaction(Guid TransactionId, Vector? QueryEmbedding);

public sealed record SemanticNeighborHit(
    Guid SourceTransactionId,
    Guid ProposedSubcategoryId,
    decimal CosineDistance,
    TransactionReviewStatus SourceReviewStatus,
    DateOnly SourceTransactionDate);

public interface IPostgresSemanticNeighborQuery
{
    bool SupportsSemanticSearch { get; }

    Task<SemanticQueryTransaction?> GetQueryTransactionAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SemanticNeighborHit>> QueryNearestNeighborsAsync(
        Guid transactionId,
        Vector queryEmbedding,
        int fetchLimit,
        CancellationToken cancellationToken = default);
}

public sealed class PostgresSemanticNeighborQuery(MosaicMoneyDbContext dbContext) : IPostgresSemanticNeighborQuery
{
    private sealed class SemanticNeighborSqlRow
    {
        public Guid SourceTransactionId { get; set; }

        public Guid ProposedSubcategoryId { get; set; }

        public double CosineDistance { get; set; }

        public int SourceReviewStatus { get; set; }

        public DateOnly SourceTransactionDate { get; set; }
    }

    public bool SupportsSemanticSearch =>
        dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;

    public async Task<SemanticQueryTransaction?> GetQueryTransactionAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        if (!SupportsSemanticSearch)
        {
            return null;
        }

        return await dbContext.EnrichedTransactions
            .AsNoTracking()
            .Where(x => x.Id == transactionId)
            .Select(x => new SemanticQueryTransaction(x.Id, x.DescriptionEmbedding))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SemanticNeighborHit>> QueryNearestNeighborsAsync(
        Guid transactionId,
        Vector queryEmbedding,
        int fetchLimit,
        CancellationToken cancellationToken = default)
    {
        if (!SupportsSemanticSearch)
        {
            return [];
        }

        var boundedLimit = Math.Clamp(fetchLimit, 1, 200);

        var rows = await dbContext.Database.SqlQuery<SemanticNeighborSqlRow>($"""
            SELECT
                et."Id" AS "SourceTransactionId",
                et."SubcategoryId" AS "ProposedSubcategoryId",
                (et."DescriptionEmbedding" <=> {queryEmbedding})::double precision AS "CosineDistance",
                et."ReviewStatus" AS "SourceReviewStatus",
                et."TransactionDate" AS "SourceTransactionDate"
            FROM "EnrichedTransactions" et
            WHERE et."Id" <> {transactionId}
              AND et."SubcategoryId" IS NOT NULL
              AND et."DescriptionEmbedding" IS NOT NULL
              AND et."ReviewStatus" <> {(int)TransactionReviewStatus.NeedsReview}
            ORDER BY et."DescriptionEmbedding" <=> {queryEmbedding}
            LIMIT {boundedLimit}
            """)
            .ToListAsync(cancellationToken);

        return rows.Select(row => new SemanticNeighborHit(
                row.SourceTransactionId,
                row.ProposedSubcategoryId,
                decimal.Round((decimal)row.CosineDistance, 6, MidpointRounding.AwayFromZero),
                Enum.IsDefined(typeof(TransactionReviewStatus), row.SourceReviewStatus)
                    ? (TransactionReviewStatus)row.SourceReviewStatus
                    : TransactionReviewStatus.None,
                row.SourceTransactionDate))
            .ToList();
    }
}

public interface IPostgresSemanticRetrievalService
{
    Task<SemanticRetrievalResult> RetrieveCandidatesAsync(
        Guid transactionId,
        SemanticRetrievalRequest? request = null,
        CancellationToken cancellationToken = default);
}

public sealed class PostgresSemanticRetrievalService(
    IPostgresSemanticNeighborQuery query,
    ILogger<PostgresSemanticRetrievalService> logger) : IPostgresSemanticRetrievalService
{
    private const string ProvenanceSource = "postgresql.pgvector.cosine_distance";

    private sealed record CandidateProjection(
        SemanticNeighborHit Hit,
        decimal NormalizedScore);

    private sealed record SemanticCandidateProvenancePayload(
        Guid SourceTransactionId,
        decimal CosineDistance,
        decimal NormalizedScore,
        int SupportingMatchCount,
        string SourceReviewStatus,
        DateOnly SourceTransactionDate);

    public async Task<SemanticRetrievalResult> RetrieveCandidatesAsync(
        Guid transactionId,
        SemanticRetrievalRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        if (transactionId == Guid.Empty)
        {
            return new SemanticRetrievalResult(
                Succeeded: false,
                SemanticRetrievalStatusCodes.TransactionNotFound,
                "Transaction id is required.",
                []);
        }

        if (!query.SupportsSemanticSearch)
        {
            return new SemanticRetrievalResult(
                Succeeded: false,
                SemanticRetrievalStatusCodes.ProviderNotSupported,
                "Semantic retrieval requires PostgreSQL with pgvector support.",
                []);
        }

        var resolvedRequest = request ?? new SemanticRetrievalRequest();
        var maxCandidates = Math.Clamp(resolvedRequest.MaxCandidates, 1, 20);
        var candidateScanLimit = Math.Clamp(resolvedRequest.CandidateScanLimit, maxCandidates, 200);
        var minimumScore = decimal.Clamp(resolvedRequest.MinimumNormalizedScore, 0m, 1m);

        var queryTransaction = await query.GetQueryTransactionAsync(transactionId, cancellationToken);
        if (queryTransaction is null)
        {
            return new SemanticRetrievalResult(
                Succeeded: false,
                SemanticRetrievalStatusCodes.TransactionNotFound,
                "Query transaction was not found.",
                []);
        }

        if (queryTransaction.QueryEmbedding is null)
        {
            return new SemanticRetrievalResult(
                Succeeded: false,
                SemanticRetrievalStatusCodes.QueryEmbeddingMissing,
                "Query transaction does not have an embedding yet.",
                []);
        }

        IReadOnlyList<SemanticNeighborHit> neighbors;
        try
        {
            neighbors = await query.QueryNearestNeighborsAsync(
                transactionId,
                queryTransaction.QueryEmbedding,
                candidateScanLimit,
                cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(
                ex,
                "Semantic retrieval query failed for transaction {TransactionId}.",
                transactionId);

            return new SemanticRetrievalResult(
                Succeeded: false,
                SemanticRetrievalStatusCodes.QueryFailed,
                "Semantic retrieval query failed.",
                []);
        }

        var projected = neighbors
            .Select(hit => new CandidateProjection(hit, NormalizeScore(hit.CosineDistance)))
            .Where(x => x.NormalizedScore >= minimumScore)
            .ToList();

        var aggregated = projected
            .GroupBy(x => x.Hit.ProposedSubcategoryId)
            .Select(group =>
            {
                var top = group
                    .OrderByDescending(x => x.NormalizedScore)
                    .ThenBy(x => x.Hit.SourceTransactionId)
                    .First();

                var payload = new SemanticCandidateProvenancePayload(
                    top.Hit.SourceTransactionId,
                    top.Hit.CosineDistance,
                    top.NormalizedScore,
                    group.Count(),
                    top.Hit.SourceReviewStatus.ToString(),
                    top.Hit.SourceTransactionDate);

                return new SemanticRetrievalCandidate(
                    group.Key,
                    top.NormalizedScore,
                    top.Hit.SourceTransactionId,
                    group.Count(),
                    ProvenanceSource,
                    top.Hit.SourceTransactionId.ToString("D"),
                    JsonSerializer.Serialize(payload));
            })
            .OrderByDescending(x => x.NormalizedScore)
            .ThenBy(x => x.ProposedSubcategoryId)
            .Take(maxCandidates)
            .ToList();

        if (aggregated.Count == 0)
        {
            return new SemanticRetrievalResult(
                Succeeded: true,
                SemanticRetrievalStatusCodes.NoCandidates,
                "No semantic candidates met the configured score threshold.",
                []);
        }

        return new SemanticRetrievalResult(
            Succeeded: true,
            SemanticRetrievalStatusCodes.Ok,
            "Semantic candidates resolved successfully.",
            aggregated);
    }

    private static decimal NormalizeScore(decimal cosineDistance)
    {
        var normalized = decimal.Clamp(1m - cosineDistance, 0m, 1m);
        return decimal.Round(normalized, 4, MidpointRounding.AwayFromZero);
    }
}
