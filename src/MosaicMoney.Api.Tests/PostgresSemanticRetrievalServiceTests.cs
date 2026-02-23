using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.Classification;
using Pgvector;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class PostgresSemanticRetrievalServiceTests
{
    [Fact]
    public async Task RetrieveCandidatesAsync_NormalizesScoresAndPreservesProvenance()
    {
        var subcategoryA = Guid.NewGuid();
        var subcategoryB = Guid.NewGuid();
        var sourceA = Guid.NewGuid();
        var sourceA2 = Guid.NewGuid();
        var sourceB = Guid.NewGuid();

        var query = new StubSemanticNeighborQuery
        {
            QueryTransaction = new SemanticQueryTransaction(Guid.NewGuid(), new Vector(new[] { 0.5f, 0.5f, 0.5f })),
            NeighborHits =
            [
                new SemanticNeighborHit(sourceA, subcategoryA, 0.0500m, TransactionReviewStatus.Reviewed, new DateOnly(2026, 2, 20)),
                new SemanticNeighborHit(sourceA2, subcategoryA, 0.0800m, TransactionReviewStatus.None, new DateOnly(2026, 2, 19)),
                new SemanticNeighborHit(sourceB, subcategoryB, 0.1400m, TransactionReviewStatus.Reviewed, new DateOnly(2026, 2, 18)),
                new SemanticNeighborHit(Guid.NewGuid(), Guid.NewGuid(), 0.4000m, TransactionReviewStatus.Reviewed, new DateOnly(2026, 2, 17)),
            ]
        };

        var service = CreateService(query);

        var result = await service.RetrieveCandidatesAsync(
            Guid.NewGuid(),
            new SemanticRetrievalRequest(
                MaxCandidates: 2,
                CandidateScanLimit: 20,
                MinimumNormalizedScore: 0.8000m));

        Assert.True(result.Succeeded);
        Assert.Equal(SemanticRetrievalStatusCodes.Ok, result.StatusCode);
        Assert.Equal(2, result.Candidates.Count);

        var top = result.Candidates[0];
        Assert.Equal(subcategoryA, top.ProposedSubcategoryId);
        Assert.Equal(0.9500m, top.NormalizedScore);
        Assert.Equal(2, top.SupportingMatchCount);
        Assert.Equal("postgresql.pgvector.cosine_distance", top.ProvenanceSource);
        Assert.Equal(sourceA.ToString("D"), top.ProvenanceReference);

        using var payload = JsonDocument.Parse(top.ProvenancePayloadJson);
        Assert.Equal(sourceA.ToString("D"), payload.RootElement.GetProperty("SourceTransactionId").GetString());
        Assert.Equal(2, payload.RootElement.GetProperty("SupportingMatchCount").GetInt32());

        var second = result.Candidates[1];
        Assert.Equal(subcategoryB, second.ProposedSubcategoryId);
        Assert.Equal(0.8600m, second.NormalizedScore);
    }

    [Fact]
    public async Task RetrieveCandidatesAsync_ThresholdAndBounds_AreApplied()
    {
        var query = new StubSemanticNeighborQuery
        {
            QueryTransaction = new SemanticQueryTransaction(Guid.NewGuid(), new Vector(new[] { 0.1f, 0.2f, 0.3f })),
            NeighborHits =
            [
                new SemanticNeighborHit(Guid.NewGuid(), Guid.NewGuid(), 0.1100m, TransactionReviewStatus.Reviewed, new DateOnly(2026, 2, 20)),
                new SemanticNeighborHit(Guid.NewGuid(), Guid.NewGuid(), 0.1200m, TransactionReviewStatus.Reviewed, new DateOnly(2026, 2, 20)),
                new SemanticNeighborHit(Guid.NewGuid(), Guid.NewGuid(), 0.1300m, TransactionReviewStatus.Reviewed, new DateOnly(2026, 2, 20)),
                new SemanticNeighborHit(Guid.NewGuid(), Guid.NewGuid(), 0.4000m, TransactionReviewStatus.Reviewed, new DateOnly(2026, 2, 20)),
            ]
        };

        var service = CreateService(query);

        var result = await service.RetrieveCandidatesAsync(
            Guid.NewGuid(),
            new SemanticRetrievalRequest(
                MaxCandidates: 2,
                CandidateScanLimit: 50,
                MinimumNormalizedScore: 0.8500m));

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Candidates.Count);
        Assert.All(result.Candidates, x => Assert.True(x.NormalizedScore >= 0.8500m));
        Assert.Equal(50, query.LastFetchLimit);
    }

    [Fact]
    public async Task RetrieveCandidatesAsync_ProviderUnsupported_FailsSafely()
    {
        var query = new StubSemanticNeighborQuery
        {
            SupportsSemanticSearch = false,
            QueryTransaction = new SemanticQueryTransaction(Guid.NewGuid(), new Vector(new[] { 0.1f, 0.2f, 0.3f }))
        };

        var service = CreateService(query);
        var result = await service.RetrieveCandidatesAsync(Guid.NewGuid());

        Assert.False(result.Succeeded);
        Assert.Equal(SemanticRetrievalStatusCodes.ProviderNotSupported, result.StatusCode);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public async Task RetrieveCandidatesAsync_MissingQueryEmbedding_FailsSafely()
    {
        var query = new StubSemanticNeighborQuery
        {
            QueryTransaction = new SemanticQueryTransaction(Guid.NewGuid(), null)
        };

        var service = CreateService(query);
        var result = await service.RetrieveCandidatesAsync(Guid.NewGuid());

        Assert.False(result.Succeeded);
        Assert.Equal(SemanticRetrievalStatusCodes.QueryEmbeddingMissing, result.StatusCode);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public async Task RetrieveCandidatesAsync_QueryFailure_ReturnsFailureResult()
    {
        var query = new StubSemanticNeighborQuery
        {
            QueryTransaction = new SemanticQueryTransaction(Guid.NewGuid(), new Vector(new[] { 0.1f, 0.2f, 0.3f })),
            ThrowOnQuery = true
        };

        var service = CreateService(query);
        var result = await service.RetrieveCandidatesAsync(Guid.NewGuid());

        Assert.False(result.Succeeded);
        Assert.Equal(SemanticRetrievalStatusCodes.QueryFailed, result.StatusCode);
        Assert.Empty(result.Candidates);
    }

    private static PostgresSemanticRetrievalService CreateService(StubSemanticNeighborQuery query)
    {
        return new PostgresSemanticRetrievalService(
            query,
            NullLogger<PostgresSemanticRetrievalService>.Instance);
    }

    private sealed class StubSemanticNeighborQuery : IPostgresSemanticNeighborQuery
    {
        public bool SupportsSemanticSearch { get; set; } = true;

        public SemanticQueryTransaction? QueryTransaction { get; set; }

        public IReadOnlyList<SemanticNeighborHit> NeighborHits { get; set; } = [];

        public bool ThrowOnQuery { get; set; }

        public int LastFetchLimit { get; private set; }

        public Task<SemanticQueryTransaction?> GetQueryTransactionAsync(
            Guid transactionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(QueryTransaction);
        }

        public Task<IReadOnlyList<SemanticNeighborHit>> QueryNearestNeighborsAsync(
            Guid transactionId,
            Vector queryEmbedding,
            int fetchLimit,
            CancellationToken cancellationToken = default)
        {
            LastFetchLimit = fetchLimit;

            if (ThrowOnQuery)
            {
                throw new InvalidOperationException("Simulated semantic query failure.");
            }

            return Task.FromResult(NeighborHits);
        }
    }
}
