using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.Embeddings;
using MosaicMoney.Api.Domain.Ledger.Ingestion;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class TransactionEmbeddingQueuePipelineTests
{
    [Fact]
    public async Task IngestionAndEnqueue_EmbeddingsAreGeneratedAsynchronously()
    {
        await using var dbContext = CreateDbContext();
        var accountId = await SeedAccountAsync(dbContext);

        var ingestionService = new PlaidDeltaIngestionService(dbContext);
        var queueService = new TransactionEmbeddingQueueService(
            dbContext,
            NullLogger<TransactionEmbeddingQueueService>.Instance);

        var ingestRequest = BuildRequest(
            accountId,
            "cursor-mm-be-10-1",
            "plaid-tx-embed-1",
            "Utilities Electric",
            -95.42m,
            new DateOnly(2026, 2, 23));

        var ingestResult = await ingestionService.IngestAsync(ingestRequest);
        await queueService.EnqueueTransactionsAsync(
            ingestResult.Items.Select(x => x.EnrichedTransactionId).ToArray());

        var beforeProcessing = await dbContext.EnrichedTransactions
            .SingleAsync(x => x.PlaidTransactionId == "plaid-tx-embed-1");
        var queuedItem = await dbContext.TransactionEmbeddingQueueItems
            .SingleAsync(x => x.TransactionId == beforeProcessing.Id);

        Assert.Null(beforeProcessing.DescriptionEmbeddingHash);
        Assert.Equal(EmbeddingQueueStatus.Pending, queuedItem.Status);

        var processor = new TransactionEmbeddingQueueProcessor(
            dbContext,
            new StableEmbeddingGenerator(),
            NullLogger<TransactionEmbeddingQueueProcessor>.Instance);

        var processingResult = await processor.ProcessDueItemsAsync(10);

        var afterProcessing = await dbContext.EnrichedTransactions
            .SingleAsync(x => x.Id == beforeProcessing.Id);

        Assert.Equal(1, processingResult.SucceededCount);
        Assert.NotNull(afterProcessing.DescriptionEmbeddingHash);
    }

    [Fact]
    public async Task ProcessDueItemsAsync_FailingGenerator_RetriesThenDeadLetters()
    {
        await using var dbContext = CreateDbContext();
        var transaction = await SeedTransactionAsync(dbContext, "Retry Merchant", -12.00m);

        dbContext.TransactionEmbeddingQueueItems.Add(new TransactionEmbeddingQueueItem
        {
            Id = Guid.NewGuid(),
            TransactionId = transaction.Id,
            DescriptionHash = EmbeddingTextHasher.ComputeHash(transaction.Description),
            Status = EmbeddingQueueStatus.Pending,
            AttemptCount = 0,
            MaxAttempts = 2,
            EnqueuedAtUtc = DateTime.UtcNow,
            NextAttemptAtUtc = DateTime.UtcNow.AddSeconds(-1),
        });
        await dbContext.SaveChangesAsync();

        var processor = new TransactionEmbeddingQueueProcessor(
            dbContext,
            new AlwaysFailEmbeddingGenerator(),
            NullLogger<TransactionEmbeddingQueueProcessor>.Instance);

        var firstAttempt = await processor.ProcessDueItemsAsync(1);
        var queueItem = await dbContext.TransactionEmbeddingQueueItems.SingleAsync();

        Assert.Equal(1, firstAttempt.RetriedCount);
        Assert.Equal(EmbeddingQueueStatus.Pending, queueItem.Status);
        Assert.Equal(1, queueItem.AttemptCount);
        Assert.NotNull(queueItem.LastError);

        queueItem.NextAttemptAtUtc = DateTime.UtcNow.AddSeconds(-1);
        await dbContext.SaveChangesAsync();

        var secondAttempt = await processor.ProcessDueItemsAsync(1);
        queueItem = await dbContext.TransactionEmbeddingQueueItems.SingleAsync();

        Assert.Equal(1, secondAttempt.DeadLetteredCount);
        Assert.Equal(EmbeddingQueueStatus.DeadLetter, queueItem.Status);
        Assert.Equal(2, queueItem.AttemptCount);
        Assert.NotNull(queueItem.DeadLetteredAtUtc);
    }

    [Fact]
    public async Task ProcessDueItemsAsync_DuplicateQueueEntries_AreIdempotent()
    {
        await using var dbContext = CreateDbContext();
        var transaction = await SeedTransactionAsync(dbContext, "Duplicate Merchant", -44.10m);
        var descriptionHash = EmbeddingTextHasher.ComputeHash(transaction.Description);

        dbContext.TransactionEmbeddingQueueItems.AddRange(
            new TransactionEmbeddingQueueItem
            {
                Id = Guid.NewGuid(),
                TransactionId = transaction.Id,
                DescriptionHash = descriptionHash,
                Status = EmbeddingQueueStatus.Pending,
                AttemptCount = 0,
                MaxAttempts = 5,
                EnqueuedAtUtc = DateTime.UtcNow,
                NextAttemptAtUtc = DateTime.UtcNow.AddSeconds(-1),
            },
            new TransactionEmbeddingQueueItem
            {
                Id = Guid.NewGuid(),
                TransactionId = transaction.Id,
                DescriptionHash = descriptionHash,
                Status = EmbeddingQueueStatus.Pending,
                AttemptCount = 0,
                MaxAttempts = 5,
                EnqueuedAtUtc = DateTime.UtcNow,
                NextAttemptAtUtc = DateTime.UtcNow.AddSeconds(-1),
            });

        await dbContext.SaveChangesAsync();

        var generator = new CountingEmbeddingGenerator();
        var processor = new TransactionEmbeddingQueueProcessor(
            dbContext,
            generator,
            NullLogger<TransactionEmbeddingQueueProcessor>.Instance);

        var result = await processor.ProcessDueItemsAsync(10);
        transaction = await dbContext.EnrichedTransactions.SingleAsync(x => x.Id == transaction.Id);

        Assert.Equal(2, result.ClaimedCount);
        Assert.Equal(2, result.SucceededCount);
        Assert.Equal(1, generator.CallCount);
        Assert.Equal(descriptionHash, transaction.DescriptionEmbeddingHash);
    }

    [Fact]
    public async Task ProcessDueItemsAsync_StalePayload_IsSkippedSafely()
    {
        await using var dbContext = CreateDbContext();
        var transaction = await SeedTransactionAsync(dbContext, "Current Merchant", -17.35m);

        dbContext.TransactionEmbeddingQueueItems.Add(new TransactionEmbeddingQueueItem
        {
            Id = Guid.NewGuid(),
            TransactionId = transaction.Id,
            DescriptionHash = EmbeddingTextHasher.ComputeHash("Old Merchant"),
            Status = EmbeddingQueueStatus.Pending,
            AttemptCount = 0,
            MaxAttempts = 5,
            EnqueuedAtUtc = DateTime.UtcNow,
            NextAttemptAtUtc = DateTime.UtcNow.AddSeconds(-1),
        });

        await dbContext.SaveChangesAsync();

        var generator = new CountingEmbeddingGenerator();
        var processor = new TransactionEmbeddingQueueProcessor(
            dbContext,
            generator,
            NullLogger<TransactionEmbeddingQueueProcessor>.Instance);

        var result = await processor.ProcessDueItemsAsync(1);
        var queueItem = await dbContext.TransactionEmbeddingQueueItems.SingleAsync();
        transaction = await dbContext.EnrichedTransactions.SingleAsync(x => x.Id == transaction.Id);

        Assert.Equal(1, result.SkippedStaleCount);
        Assert.Equal(0, generator.CallCount);
        Assert.Equal(EmbeddingQueueStatus.Succeeded, queueItem.Status);
        Assert.Equal("stale_payload_skipped", queueItem.LastError);
        Assert.Null(transaction.DescriptionEmbeddingHash);
    }

    private static MosaicMoneyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MosaicMoneyDbContext>()
            .UseInMemoryDatabase($"mosaicmoney-embedding-tests-{Guid.NewGuid()}")
            .Options;

        return new MosaicMoneyDbContext(options);
    }

    private static async Task<Guid> SeedAccountAsync(MosaicMoneyDbContext dbContext)
    {
        var household = new Household
        {
            Id = Guid.NewGuid(),
            Name = "Test Household",
            CreatedAtUtc = DateTime.UtcNow,
        };

        var account = new Account
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            Name = "Primary Checking",
            InstitutionName = "Test Bank",
            ExternalAccountKey = "acct-seed",
            IsActive = true,
        };

        dbContext.Households.Add(household);
        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync();

        return account.Id;
    }

    private static async Task<EnrichedTransaction> SeedTransactionAsync(
        MosaicMoneyDbContext dbContext,
        string description,
        decimal amount)
    {
        var accountId = await SeedAccountAsync(dbContext);
        var transaction = new EnrichedTransaction
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            PlaidTransactionId = $"seed-{Guid.NewGuid():N}",
            Description = description,
            Amount = amount,
            TransactionDate = new DateOnly(2026, 2, 23),
            ReviewStatus = TransactionReviewStatus.None,
            CreatedAtUtc = DateTime.UtcNow,
            LastModifiedAtUtc = DateTime.UtcNow,
        };

        dbContext.EnrichedTransactions.Add(transaction);
        await dbContext.SaveChangesAsync();
        return transaction;
    }

    private static PlaidDeltaIngestionRequest BuildRequest(
        Guid accountId,
        string cursor,
        string plaidTransactionId,
        string description,
        decimal amount,
        DateOnly transactionDate)
    {
        return new PlaidDeltaIngestionRequest(
            accountId,
            cursor,
            [new PlaidDeltaIngestionItemInput(
                plaidTransactionId,
                description,
                amount,
                transactionDate,
                "{}",
                false,
                null)]);
    }

    private sealed class StableEmbeddingGenerator : ITransactionEmbeddingGenerator
    {
        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Enumerable.Repeat(0.001f, DeterministicTransactionEmbeddingGenerator.EmbeddingDimensions).ToArray());
        }
    }

    private sealed class AlwaysFailEmbeddingGenerator : ITransactionEmbeddingGenerator
    {
        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated embedding generation failure.");
        }
    }

    private sealed class CountingEmbeddingGenerator : ITransactionEmbeddingGenerator
    {
        public int CallCount { get; private set; }

        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(Enumerable.Repeat(0.002f, DeterministicTransactionEmbeddingGenerator.EmbeddingDimensions).ToArray());
        }
    }
}
