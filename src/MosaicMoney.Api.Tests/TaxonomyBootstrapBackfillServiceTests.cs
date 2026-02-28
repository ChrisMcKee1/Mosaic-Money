using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.Classification;
using MosaicMoney.Api.Domain.Ledger.Taxonomy;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class TaxonomyBootstrapBackfillServiceTests
{
    [Fact]
    public async Task ExecuteAsync_SeedsTaxonomy_BackfillsDeterministicMatches_AndRoutesAmbiguousToNeedsReview()
    {
        using var dbContext = CreateDbContext();
        var accountId = await SeedActiveAccountAsync(dbContext);

        var energyTransactionId = Guid.NewGuid();
        var unknownTransactionId = Guid.NewGuid();
        var payrollTransactionId = Guid.NewGuid();

        dbContext.EnrichedTransactions.AddRange(
            new EnrichedTransaction
            {
                Id = energyTransactionId,
                AccountId = accountId,
                Description = "Austin Energy monthly bill",
                Amount = -120.44m,
                TransactionDate = new DateOnly(2026, 2, 27),
                ReviewStatus = TransactionReviewStatus.None,
                CreatedAtUtc = DateTime.UtcNow,
                LastModifiedAtUtc = DateTime.UtcNow,
            },
            new EnrichedTransaction
            {
                Id = unknownTransactionId,
                AccountId = accountId,
                Description = "Unknown merchant charge",
                Amount = -38.90m,
                TransactionDate = new DateOnly(2026, 2, 27),
                ReviewStatus = TransactionReviewStatus.None,
                CreatedAtUtc = DateTime.UtcNow,
                LastModifiedAtUtc = DateTime.UtcNow,
            },
            new EnrichedTransaction
            {
                Id = payrollTransactionId,
                AccountId = accountId,
                Description = "Payroll direct deposit",
                Amount = 2200.00m,
                TransactionDate = new DateOnly(2026, 2, 27),
                ReviewStatus = TransactionReviewStatus.None,
                CreatedAtUtc = DateTime.UtcNow,
                LastModifiedAtUtc = DateTime.UtcNow,
            });

        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, new DateTime(2026, 2, 27, 15, 0, 0, DateTimeKind.Utc));

        var firstRun = await service.ExecuteAsync();

        Assert.True(firstRun.Enabled);
        Assert.True(firstRun.CategoriesInserted > 0);
        Assert.True(firstRun.SubcategoriesInserted > 0);
        Assert.Equal(2, firstRun.EligibleTransactionsProcessed);
        Assert.Equal(1, firstRun.BackfilledTransactions);
        Assert.Equal(1, firstRun.NeedsReviewRouted);
        Assert.Equal(3, firstRun.BeforeSnapshot.TransactionSubcategory.NullCount);
        Assert.Equal(2, firstRun.AfterSnapshot.TransactionSubcategory.NullCount);

        var energyTransaction = await dbContext.EnrichedTransactions.SingleAsync(x => x.Id == energyTransactionId);
        Assert.NotNull(energyTransaction.SubcategoryId);
        Assert.Equal(TransactionReviewStatus.None, energyTransaction.ReviewStatus);

        var unknownTransaction = await dbContext.EnrichedTransactions.SingleAsync(x => x.Id == unknownTransactionId);
        Assert.Null(unknownTransaction.SubcategoryId);
        Assert.Equal(TransactionReviewStatus.NeedsReview, unknownTransaction.ReviewStatus);
        Assert.Equal(TaxonomyBackfillReasonCodes.NoRuleMatch, unknownTransaction.ReviewReason);

        var payrollTransaction = await dbContext.EnrichedTransactions.SingleAsync(x => x.Id == payrollTransactionId);
        Assert.Null(payrollTransaction.SubcategoryId);
        Assert.Equal(TransactionReviewStatus.None, payrollTransaction.ReviewStatus);

        var secondRun = await service.ExecuteAsync();

        Assert.Equal(0, secondRun.CategoriesInserted);
        Assert.Equal(0, secondRun.SubcategoriesInserted);
        Assert.Equal(0, secondRun.EligibleTransactionsProcessed);
        Assert.Equal(0, secondRun.BackfilledTransactions);
        Assert.Equal(0, secondRun.NeedsReviewRouted);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotOverrideExistingAssignments_OrReviewedTransactions()
    {
        using var dbContext = CreateDbContext();
        var accountId = await SeedActiveAccountAsync(dbContext);

        var manualCategory = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Manual",
            DisplayOrder = 99,
            IsSystem = false,
        };

        var manualSubcategory = new Subcategory
        {
            Id = Guid.NewGuid(),
            CategoryId = manualCategory.Id,
            Name = "Manual Subcategory",
            IsBusinessExpense = false,
        };

        var assignedTransactionId = Guid.NewGuid();
        var reviewedTransactionId = Guid.NewGuid();

        dbContext.Categories.Add(manualCategory);
        dbContext.Subcategories.Add(manualSubcategory);
        dbContext.EnrichedTransactions.AddRange(
            new EnrichedTransaction
            {
                Id = assignedTransactionId,
                AccountId = accountId,
                Description = "Austin Energy monthly bill",
                Amount = -88.00m,
                TransactionDate = new DateOnly(2026, 2, 28),
                SubcategoryId = manualSubcategory.Id,
                ReviewStatus = TransactionReviewStatus.Reviewed,
                CreatedAtUtc = DateTime.UtcNow,
                LastModifiedAtUtc = DateTime.UtcNow,
            },
            new EnrichedTransaction
            {
                Id = reviewedTransactionId,
                AccountId = accountId,
                Description = "Unknown merchant charge",
                Amount = -23.01m,
                TransactionDate = new DateOnly(2026, 2, 28),
                ReviewStatus = TransactionReviewStatus.Reviewed,
                ReviewReason = "manual_review_decision",
                CreatedAtUtc = DateTime.UtcNow,
                LastModifiedAtUtc = DateTime.UtcNow,
            });

        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, new DateTime(2026, 2, 28, 10, 30, 0, DateTimeKind.Utc));

        var result = await service.ExecuteAsync();

        Assert.Equal(0, result.EligibleTransactionsProcessed);
        Assert.Equal(0, result.BackfilledTransactions);
        Assert.Equal(0, result.NeedsReviewRouted);

        var assignedTransaction = await dbContext.EnrichedTransactions.SingleAsync(x => x.Id == assignedTransactionId);
        Assert.Equal(manualSubcategory.Id, assignedTransaction.SubcategoryId);
        Assert.Equal(TransactionReviewStatus.Reviewed, assignedTransaction.ReviewStatus);

        var reviewedTransaction = await dbContext.EnrichedTransactions.SingleAsync(x => x.Id == reviewedTransactionId);
        Assert.Null(reviewedTransaction.SubcategoryId);
        Assert.Equal(TransactionReviewStatus.Reviewed, reviewedTransaction.ReviewStatus);
        Assert.Equal("manual_review_decision", reviewedTransaction.ReviewReason);
    }

    private static TaxonomyBootstrapBackfillService CreateService(MosaicMoneyDbContext dbContext, DateTime utcNow)
    {
        return new TaxonomyBootstrapBackfillService(
            dbContext,
            new DeterministicClassificationEngine(),
            new FixedTimeProvider(utcNow),
            Options.Create(new TaxonomyBackfillOptions
            {
                Enabled = true,
                DeterministicConfidenceThreshold = 0.7000m,
                MaxTransactionsPerRun = 10000,
            }),
            NullLogger<TaxonomyBootstrapBackfillService>.Instance);
    }

    private static MosaicMoneyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MosaicMoneyDbContext>()
            .UseInMemoryDatabase($"mosaicmoney-taxonomy-backfill-tests-{Guid.NewGuid()}")
            .Options;

        return new MosaicMoneyDbContext(options);
    }

    private static async Task<Guid> SeedActiveAccountAsync(MosaicMoneyDbContext dbContext)
    {
        var householdId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        dbContext.Households.Add(new Household
        {
            Id = householdId,
            Name = "Taxonomy Household",
        });

        dbContext.Accounts.Add(new Account
        {
            Id = accountId,
            HouseholdId = householdId,
            Name = "Checking",
            IsActive = true,
        });

        await dbContext.SaveChangesAsync();
        return accountId;
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(utcNow);
    }
}
