using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.Ingestion;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class PlaidDeltaIngestionServiceTests
{
    [Fact]
    public async Task IngestAsync_ReprocessingSameDelta_IsIdempotentForRawAndEnriched()
    {
        await using var dbContext = CreateDbContext();
        var accountId = await SeedAccountAsync(dbContext);
        var service = new PlaidDeltaIngestionService(dbContext);

        var request = BuildRequest(
            accountId,
            "cursor-1",
            "plaid-tx-1",
            "Coffee",
            -4.25m,
            new DateOnly(2026, 2, 21),
            "{\"transaction_id\":\"plaid-tx-1\",\"name\":\"Coffee\",\"amount\":-4.25}",
            isAmbiguous: false,
            reviewReason: null);

        var firstResult = await service.IngestAsync(request);
        var secondResult = await service.IngestAsync(request);

        Assert.Equal(1, firstResult.InsertedCount);
        Assert.Equal(1, firstResult.RawStoredCount);

        Assert.Equal(1, secondResult.UnchangedCount);
        Assert.Equal(1, secondResult.RawDuplicateCount);
        Assert.True(secondResult.Items[0].RawDuplicate);
        Assert.Equal(IngestionDisposition.Unchanged, secondResult.Items[0].Disposition);

        Assert.Equal(1, await dbContext.RawTransactionIngestionRecords.CountAsync());
        Assert.Equal(1, await dbContext.EnrichedTransactions.CountAsync());
    }

    [Fact]
    public async Task IngestAsync_ChangedDeltaPayload_UpdatesExistingTransactionWithoutDuplicates()
    {
        await using var dbContext = CreateDbContext();
        var accountId = await SeedAccountAsync(dbContext);
        var service = new PlaidDeltaIngestionService(dbContext);

        var firstRequest = BuildRequest(
            accountId,
            "cursor-1",
            "plaid-tx-2",
            "Coffee",
            -4.25m,
            new DateOnly(2026, 2, 21),
            "{\"transaction_id\":\"plaid-tx-2\",\"name\":\"Coffee\",\"amount\":-4.25}",
            isAmbiguous: false,
            reviewReason: null);

        var secondRequest = BuildRequest(
            accountId,
            "cursor-2",
            "plaid-tx-2",
            "Coffee Shop",
            -5.10m,
            new DateOnly(2026, 2, 22),
            "{\"transaction_id\":\"plaid-tx-2\",\"name\":\"Coffee Shop\",\"amount\":-5.10}",
            isAmbiguous: false,
            reviewReason: null);

        await service.IngestAsync(firstRequest);
        var secondResult = await service.IngestAsync(secondRequest);

        var transaction = await dbContext.EnrichedTransactions.SingleAsync(x => x.PlaidTransactionId == "plaid-tx-2");

        Assert.Equal(1, secondResult.UpdatedCount);
        Assert.Equal(IngestionDisposition.Updated, secondResult.Items[0].Disposition);
        Assert.Equal("Coffee Shop", transaction.Description);
        Assert.Equal(-5.10m, transaction.Amount);
        Assert.Equal(new DateOnly(2026, 2, 22), transaction.TransactionDate);

        Assert.Equal(1, await dbContext.EnrichedTransactions.CountAsync());
        Assert.Equal(2, await dbContext.RawTransactionIngestionRecords.CountAsync());
    }

    [Fact]
    public async Task IngestAsync_UpsertPreservesExistingUserAndAgentNotes()
    {
        await using var dbContext = CreateDbContext();
        var accountId = await SeedAccountAsync(dbContext);

        dbContext.EnrichedTransactions.Add(new EnrichedTransaction
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            PlaidTransactionId = "plaid-tx-3",
            Description = "Original",
            Amount = -8.00m,
            TransactionDate = new DateOnly(2026, 2, 20),
            ReviewStatus = TransactionReviewStatus.None,
            UserNote = "keep-user-note",
            AgentNote = "keep-agent-note",
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
            LastModifiedAtUtc = DateTime.UtcNow.AddDays(-1),
        });
        await dbContext.SaveChangesAsync();

        var service = new PlaidDeltaIngestionService(dbContext);
        var request = BuildRequest(
            accountId,
            "cursor-3",
            "plaid-tx-3",
            "Original Updated",
            -9.25m,
            new DateOnly(2026, 2, 22),
            "{\"transaction_id\":\"plaid-tx-3\",\"name\":\"Original Updated\",\"amount\":-9.25}",
            isAmbiguous: false,
            reviewReason: null);

        await service.IngestAsync(request);

        var transaction = await dbContext.EnrichedTransactions.SingleAsync(x => x.PlaidTransactionId == "plaid-tx-3");
        Assert.Equal("keep-user-note", transaction.UserNote);
        Assert.Equal("keep-agent-note", transaction.AgentNote);
    }

    [Fact]
    public async Task IngestAsync_AmbiguousInputRoutesToNeedsReviewAndRemainsFailClosed()
    {
        await using var dbContext = CreateDbContext();
        var accountId = await SeedAccountAsync(dbContext);
        var service = new PlaidDeltaIngestionService(dbContext);

        var ambiguousRequest = BuildRequest(
            accountId,
            "cursor-4",
            "plaid-tx-4",
            "Pending Merchant",
            -12.00m,
            new DateOnly(2026, 2, 22),
            "{\"transaction_id\":\"plaid-tx-4\",\"name\":\"Pending Merchant\",\"amount\":-12.00}",
            isAmbiguous: true,
            reviewReason: null);

        var nonAmbiguousReprocess = BuildRequest(
            accountId,
            "cursor-5",
            "plaid-tx-4",
            "Pending Merchant Final",
            -12.00m,
            new DateOnly(2026, 2, 22),
            "{\"transaction_id\":\"plaid-tx-4\",\"name\":\"Pending Merchant Final\",\"amount\":-12.00}",
            isAmbiguous: false,
            reviewReason: null);

        var firstResult = await service.IngestAsync(ambiguousRequest);
        var secondResult = await service.IngestAsync(nonAmbiguousReprocess);

        var transaction = await dbContext.EnrichedTransactions.SingleAsync(x => x.PlaidTransactionId == "plaid-tx-4");

        Assert.Equal(TransactionReviewStatus.NeedsReview, firstResult.Items[0].ReviewStatus);
        Assert.Equal("ambiguous_source_payload", firstResult.Items[0].ReviewReason);

        Assert.Equal(TransactionReviewStatus.NeedsReview, transaction.ReviewStatus);
        Assert.Equal("ambiguous_source_payload", transaction.ReviewReason);
        Assert.Equal(TransactionReviewStatus.NeedsReview, secondResult.Items[0].ReviewStatus);
    }

    private static MosaicMoneyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MosaicMoneyDbContext>()
            .UseInMemoryDatabase($"mosaicmoney-tests-{Guid.NewGuid()}")
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
            ExternalAccountKey = "test-account-key",
            IsActive = true,
        };

        dbContext.Households.Add(household);
        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync();

        return account.Id;
    }

    private static PlaidDeltaIngestionRequest BuildRequest(
        Guid accountId,
        string cursor,
        string plaidTransactionId,
        string description,
        decimal amount,
        DateOnly transactionDate,
        string rawPayload,
        bool isAmbiguous,
        string? reviewReason)
    {
        return new PlaidDeltaIngestionRequest(
            accountId,
            cursor,
            [new PlaidDeltaIngestionItemInput(
                plaidTransactionId,
                description,
                amount,
                transactionDate,
                rawPayload,
                isAmbiguous,
                reviewReason)]);
    }
}
