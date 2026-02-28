using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.Ingestion;
using MosaicMoney.Api.Domain.Ledger.Taxonomy;
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

    [Fact]
    public async Task IngestAsync_TaxonomyReadinessNotReady_RoutesTransactionToNeedsReview()
    {
        await using var dbContext = CreateDbContext();
        var accountId = await SeedAccountAsync(dbContext);
        var readinessGateStub = new StubTaxonomyReadinessGate
        {
            ResultToReturn = new TaxonomyReadinessEvaluation(
                IsReady: false,
                ReasonCode: TaxonomyReadinessReasonCodes.MissingSubcategoryCoverage,
                Rationale: "Subcategory inventory does not meet ingestion readiness threshold.",
                Snapshot: new TaxonomyReadinessSnapshot(
                    HouseholdId: Guid.NewGuid(),
                    PlatformSubcategoryCount: 0,
                    HouseholdSharedSubcategoryCount: 0,
                    UserScopedSubcategoryCount: 0,
                    TotalEligibleSubcategoryCount: 0,
                    ExpenseTransactionCount: 0,
                    CategorizedExpenseTransactionCount: 0,
                    ExpenseFillRate: 1m))
        };

        var service = new PlaidDeltaIngestionService(dbContext, readinessGateStub);

        var request = BuildRequest(
            accountId,
            "cursor-readiness-1",
            "plaid-tx-readiness-1",
            "Coffee",
            -4.25m,
            new DateOnly(2026, 2, 21),
            "{\"transaction_id\":\"plaid-tx-readiness-1\",\"name\":\"Coffee\",\"amount\":-4.25}",
            isAmbiguous: false,
            reviewReason: null);

        var result = await service.IngestAsync(request);

        Assert.Equal(1, readinessGateStub.CallCount);
        Assert.Equal(TransactionReviewStatus.NeedsReview, result.Items[0].ReviewStatus);
        Assert.Equal(TaxonomyReadinessReasonCodes.MissingSubcategoryCoverage, result.Items[0].ReviewReason);

        var transaction = await dbContext.EnrichedTransactions.SingleAsync(x => x.PlaidTransactionId == "plaid-tx-readiness-1");
        Assert.Equal(TransactionReviewStatus.NeedsReview, transaction.ReviewStatus);
        Assert.Equal(TaxonomyReadinessReasonCodes.MissingSubcategoryCoverage, transaction.ReviewReason);
    }

    [Fact]
    public async Task IngestAsync_ConfidentRecurringMatch_LinksTransactionAndAdvancesDueDateOnce()
    {
        await using var dbContext = CreateDbContext();
        var accountId = await SeedAccountAsync(dbContext);
        var householdId = await dbContext.Accounts
            .Where(x => x.Id == accountId)
            .Select(x => x.HouseholdId)
            .SingleAsync();

        var recurringItem = new RecurringItem
        {
            Id = Guid.NewGuid(),
            HouseholdId = householdId,
            MerchantName = "Coffee Club",
            ExpectedAmount = 25.00m,
            Frequency = RecurringFrequency.Monthly,
            NextDueDate = new DateOnly(2026, 2, 22),
            DueWindowDaysBefore = 3,
            DueWindowDaysAfter = 3,
            AmountVariancePercent = 5.00m,
            AmountVarianceAbsolute = 2.00m,
            DeterministicMatchThreshold = 0.7000m,
            DueDateScoreWeight = 0.5000m,
            AmountScoreWeight = 0.3500m,
            RecencyScoreWeight = 0.1500m,
            DeterministicScoreVersion = "mm-be-07a-v1",
            TieBreakPolicy = "due_date_distance_then_amount_delta_then_latest_observed",
            IsActive = true,
        };

        dbContext.RecurringItems.Add(recurringItem);
        await dbContext.SaveChangesAsync();

        var service = new PlaidDeltaIngestionService(dbContext);
        var request = BuildRequest(
            accountId,
            "cursor-6",
            "plaid-tx-6",
            "Coffee Club Membership",
            -25.00m,
            new DateOnly(2026, 2, 22),
            "{\"transaction_id\":\"plaid-tx-6\",\"name\":\"Coffee Club Membership\",\"amount\":-25.00}",
            isAmbiguous: false,
            reviewReason: null);

        await service.IngestAsync(request);
        await service.IngestAsync(request);

        var transaction = await dbContext.EnrichedTransactions.SingleAsync(x => x.PlaidTransactionId == "plaid-tx-6");
        var updatedRecurringItem = await dbContext.RecurringItems.SingleAsync(x => x.Id == recurringItem.Id);

        Assert.Equal(recurringItem.Id, transaction.RecurringItemId);
        Assert.Equal(new DateOnly(2026, 3, 22), updatedRecurringItem.NextDueDate);
    }

    [Fact]
    public async Task IngestAsync_CompetingRecurringCandidates_RoutesToNeedsReviewWithoutAutoLinkingOrOscillation()
    {
        await using var dbContext = CreateDbContext();
        var accountId = await SeedAccountAsync(dbContext);
        var householdId = await dbContext.Accounts
            .Where(x => x.Id == accountId)
            .Select(x => x.HouseholdId)
            .SingleAsync();

        var nextDueDate = new DateOnly(2026, 2, 23);
        var recurringA = new RecurringItem
        {
            Id = Guid.NewGuid(),
            HouseholdId = householdId,
            MerchantName = "Streaming Service",
            ExpectedAmount = 15.99m,
            Frequency = RecurringFrequency.Monthly,
            NextDueDate = nextDueDate,
            DueWindowDaysBefore = 2,
            DueWindowDaysAfter = 2,
            AmountVariancePercent = 5.00m,
            AmountVarianceAbsolute = 1.00m,
            DeterministicMatchThreshold = 0.7000m,
            DueDateScoreWeight = 0.5000m,
            AmountScoreWeight = 0.3500m,
            RecencyScoreWeight = 0.1500m,
            DeterministicScoreVersion = "mm-be-07a-v1",
            TieBreakPolicy = "due_date_distance_then_amount_delta_then_latest_observed",
            IsActive = true,
        };

        var recurringB = new RecurringItem
        {
            Id = Guid.NewGuid(),
            HouseholdId = householdId,
            MerchantName = "Streaming Service",
            ExpectedAmount = 15.99m,
            Frequency = RecurringFrequency.Monthly,
            NextDueDate = nextDueDate,
            DueWindowDaysBefore = 2,
            DueWindowDaysAfter = 2,
            AmountVariancePercent = 5.00m,
            AmountVarianceAbsolute = 1.00m,
            DeterministicMatchThreshold = 0.7000m,
            DueDateScoreWeight = 0.5000m,
            AmountScoreWeight = 0.3500m,
            RecencyScoreWeight = 0.1500m,
            DeterministicScoreVersion = "mm-be-07a-v1",
            TieBreakPolicy = "due_date_distance_then_amount_delta_then_latest_observed",
            IsActive = true,
        };

        dbContext.RecurringItems.AddRange(recurringA, recurringB);
        await dbContext.SaveChangesAsync();

        var service = new PlaidDeltaIngestionService(dbContext);
        var request = BuildRequest(
            accountId,
            "cursor-7",
            "plaid-tx-7",
            "Streaming Service Premium",
            -15.99m,
            nextDueDate,
            "{\"transaction_id\":\"plaid-tx-7\",\"name\":\"Streaming Service Premium\",\"amount\":-15.99}",
            isAmbiguous: false,
            reviewReason: null);

        var firstResult = await service.IngestAsync(request);
        var secondResult = await service.IngestAsync(request);

        var transaction = await dbContext.EnrichedTransactions.SingleAsync(x => x.PlaidTransactionId == "plaid-tx-7");
        var updatedRecurringA = await dbContext.RecurringItems.SingleAsync(x => x.Id == recurringA.Id);
        var updatedRecurringB = await dbContext.RecurringItems.SingleAsync(x => x.Id == recurringB.Id);

        Assert.Null(transaction.RecurringItemId);
        Assert.Equal(TransactionReviewStatus.NeedsReview, transaction.ReviewStatus);
        Assert.Equal("recurring_competing_candidates", transaction.ReviewReason);

        Assert.Equal(TransactionReviewStatus.NeedsReview, firstResult.Items[0].ReviewStatus);
        Assert.Equal("recurring_competing_candidates", firstResult.Items[0].ReviewReason);

        Assert.Equal(TransactionReviewStatus.NeedsReview, secondResult.Items[0].ReviewStatus);
        Assert.Equal("recurring_competing_candidates", secondResult.Items[0].ReviewReason);
        Assert.Equal(1, secondResult.UnchangedCount);

        Assert.Equal(nextDueDate, updatedRecurringA.NextDueDate);
        Assert.Equal(nextDueDate, updatedRecurringB.NextDueDate);
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

        private sealed class StubTaxonomyReadinessGate : ITaxonomyReadinessGate
        {
            public int CallCount { get; private set; }

            public TaxonomyReadinessEvaluation ResultToReturn { get; set; } = new(
                IsReady: true,
                ReasonCode: TaxonomyReadinessReasonCodes.Ready,
                Rationale: "Ready for test execution.",
                Snapshot: new TaxonomyReadinessSnapshot(
                    HouseholdId: Guid.Empty,
                    PlatformSubcategoryCount: 3,
                    HouseholdSharedSubcategoryCount: 0,
                    UserScopedSubcategoryCount: 0,
                    TotalEligibleSubcategoryCount: 3,
                    ExpenseTransactionCount: 10,
                    CategorizedExpenseTransactionCount: 8,
                    ExpenseFillRate: 0.8000m));

            public Task<TaxonomyReadinessEvaluation> EvaluateAsync(
                Guid householdId,
                TaxonomyReadinessLane lane,
                Guid? ownerUserId = null,
                CancellationToken cancellationToken = default)
            {
                CallCount++;
                return Task.FromResult(ResultToReturn);
            }
        }
}
