using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.Classification;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class DeterministicClassificationOrchestratorTests
{
    [Fact]
    public async Task ClassifyAndPersistAsync_HighConfidenceRule_PersistsCategorizedOutcomeAndUpdatesTransaction()
    {
        await using var dbContext = CreateDbContext();
        var seeded = await SeedLedgerDataAsync(dbContext, "Austin Energy bill payment", -120.00m);

        var orchestrator = CreateOrchestrator(dbContext);
        var result = await orchestrator.ClassifyAndPersistAsync(seeded.TransactionId);

        Assert.NotNull(result);
        Assert.Equal(ClassificationDecision.Categorized, result!.Outcome.Decision);
        Assert.Equal(ClassificationAmbiguityReasonCodes.DeterministicAccepted, result.Outcome.DecisionReasonCode);
        Assert.Equal(TransactionReviewStatus.None, result.TransactionReviewStatus);
        Assert.Equal(seeded.EnergySubcategoryId, result.TransactionSubcategoryId);

        var persistedOutcome = await dbContext.TransactionClassificationOutcomes
            .Include(x => x.StageOutputs)
            .SingleAsync(x => x.Id == result.Outcome.Id);

        var stageOutput = Assert.Single(persistedOutcome.StageOutputs);
        Assert.Equal(ClassificationStage.Deterministic, stageOutput.Stage);
        Assert.Equal(1, stageOutput.StageOrder);
        Assert.False(stageOutput.EscalatedToNextStage);
    }

    [Fact]
    public async Task ClassifyAndPersistAsync_LowConfidenceRule_RoutesToNeedsReviewWithReasonCode()
    {
        await using var dbContext = CreateDbContext();
        var seeded = await SeedLedgerDataAsync(dbContext, "energy", -75.00m);

        var orchestrator = CreateOrchestrator(dbContext);
        var result = await orchestrator.ClassifyAndPersistAsync(seeded.TransactionId, seeded.ReviewerId);

        Assert.NotNull(result);
        Assert.Equal(ClassificationDecision.NeedsReview, result!.Outcome.Decision);
        Assert.Equal(ClassificationAmbiguityReasonCodes.LowConfidence, result.Outcome.DecisionReasonCode);
        Assert.Equal(TransactionReviewStatus.NeedsReview, result.TransactionReviewStatus);
        Assert.Equal(ClassificationAmbiguityReasonCodes.LowConfidence, result.TransactionReviewReason);

        var transaction = await dbContext.EnrichedTransactions.SingleAsync(x => x.Id == seeded.TransactionId);
        Assert.Equal(seeded.ReviewerId, transaction.NeedsReviewByUserId);
        Assert.Null(transaction.SubcategoryId);
    }

    [Fact]
    public async Task ClassifyAndPersistAsync_ConflictingRules_RoutesToNeedsReviewFailClosed()
    {
        await using var dbContext = CreateDbContext();
        var seeded = await SeedLedgerDataAsync(dbContext, "HEB purchase", -35.00m);

        var orchestrator = CreateOrchestrator(dbContext);
        var result = await orchestrator.ClassifyAndPersistAsync(seeded.TransactionId);

        Assert.NotNull(result);
        Assert.Equal(ClassificationDecision.NeedsReview, result!.Outcome.Decision);
        Assert.Equal(ClassificationAmbiguityReasonCodes.ConflictingDeterministicRules, result.Outcome.DecisionReasonCode);
        Assert.Equal(TransactionReviewStatus.NeedsReview, result.TransactionReviewStatus);
        Assert.Equal(ClassificationAmbiguityReasonCodes.ConflictingDeterministicRules, result.TransactionReviewReason);

        var transaction = await dbContext.EnrichedTransactions.SingleAsync(x => x.Id == seeded.TransactionId);
        Assert.Equal(TransactionReviewStatus.NeedsReview, transaction.ReviewStatus);
    }

    private static DeterministicClassificationOrchestrator CreateOrchestrator(MosaicMoneyDbContext dbContext)
    {
        return new DeterministicClassificationOrchestrator(
            dbContext,
            new DeterministicClassificationEngine(),
            new ClassificationAmbiguityPolicyGate());
    }

    private static MosaicMoneyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MosaicMoneyDbContext>()
            .UseInMemoryDatabase($"mosaicmoney-classification-tests-{Guid.NewGuid()}")
            .Options;

        return new MosaicMoneyDbContext(options);
    }

    private static async Task<(Guid TransactionId, Guid EnergySubcategoryId, Guid ReviewerId)> SeedLedgerDataAsync(
        MosaicMoneyDbContext dbContext,
        string transactionDescription,
        decimal amount)
    {
        var householdId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();

        var household = new Household
        {
            Id = householdId,
            Name = "Classification Test Household",
            CreatedAtUtc = DateTime.UtcNow,
        };

        var reviewer = new HouseholdUser
        {
            Id = reviewerId,
            HouseholdId = householdId,
            DisplayName = "Reviewer",
            ExternalUserKey = "reviewer-1",
        };

        var account = new Account
        {
            Id = accountId,
            HouseholdId = householdId,
            Name = "Primary Account",
            InstitutionName = "Test Institution",
            ExternalAccountKey = "acct-1",
            IsActive = true,
        };

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Expenses",
            DisplayOrder = 1,
            IsSystem = false,
        };

        var energySubcategory = new Subcategory
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            Name = "Austin Energy",
            IsBusinessExpense = false,
        };

        var groceriesSubcategory = new Subcategory
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            Name = "HEB Grocery",
            IsBusinessExpense = false,
        };

        var fuelSubcategory = new Subcategory
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            Name = "HEB Fuel",
            IsBusinessExpense = false,
        };

        var transaction = new EnrichedTransaction
        {
            Id = transactionId,
            AccountId = accountId,
            Description = transactionDescription,
            Amount = amount,
            TransactionDate = new DateOnly(2026, 2, 23),
            ReviewStatus = TransactionReviewStatus.None,
            CreatedAtUtc = DateTime.UtcNow,
            LastModifiedAtUtc = DateTime.UtcNow,
        };

        dbContext.Households.Add(household);
        dbContext.HouseholdUsers.Add(reviewer);
        dbContext.Accounts.Add(account);
        dbContext.Categories.Add(category);
        dbContext.Subcategories.AddRange(energySubcategory, groceriesSubcategory, fuelSubcategory);
        dbContext.EnrichedTransactions.Add(transaction);

        await dbContext.SaveChangesAsync();

        return (transactionId, energySubcategory.Id, reviewerId);
    }
}
