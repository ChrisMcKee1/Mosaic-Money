using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.Taxonomy;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class TaxonomyReadinessGateServiceTests
{
    [Fact]
    public async Task EvaluateAsync_SubcategoryCoverageBelowThreshold_ReturnsNotReady()
    {
        await using var dbContext = CreateDbContext();
        var householdId = await SeedHouseholdWithAccountAsync(dbContext);
        await SeedPlatformSubcategoriesAsync(dbContext, count: 1);

        var service = CreateService(dbContext, new TaxonomyReadinessOptions
        {
            EnableClassificationGate = true,
            EnableIngestionGate = true,
            MinimumPlatformSubcategoryCount = 2,
            MinimumTotalSubcategoryCount = 3,
            MinimumExpenseSampleCount = 20,
            MinimumExpenseFillRate = 0.7m,
        });

        var result = await service.EvaluateAsync(householdId, TaxonomyReadinessLane.Classification);

        Assert.False(result.IsReady);
        Assert.Equal(TaxonomyReadinessReasonCodes.MissingSubcategoryCoverage, result.ReasonCode);
        Assert.Equal(1, result.Snapshot.PlatformSubcategoryCount);
    }

    [Fact]
    public async Task EvaluateAsync_FillRateBelowThreshold_ReturnsNotReady()
    {
        await using var dbContext = CreateDbContext();
        var householdId = await SeedHouseholdWithAccountAsync(dbContext);
        var subcategoryId = await SeedPlatformSubcategoriesAsync(dbContext, count: 1);
        await SeedExpenseTransactionsAsync(dbContext, householdId, subcategoryId, totalExpenseCount: 3, categorizedExpenseCount: 1);

        var service = CreateService(dbContext, new TaxonomyReadinessOptions
        {
            EnableClassificationGate = true,
            EnableIngestionGate = true,
            MinimumPlatformSubcategoryCount = 1,
            MinimumTotalSubcategoryCount = 1,
            MinimumExpenseSampleCount = 3,
            MinimumExpenseFillRate = 0.8m,
        });

        var result = await service.EvaluateAsync(householdId, TaxonomyReadinessLane.Ingestion);

        Assert.False(result.IsReady);
        Assert.Equal(TaxonomyReadinessReasonCodes.FillRateBelowThreshold, result.ReasonCode);
        Assert.Equal(0.3333m, result.Snapshot.ExpenseFillRate);
    }

    [Fact]
    public async Task EvaluateAsync_ThresholdsMet_ReturnsReady()
    {
        await using var dbContext = CreateDbContext();
        var householdId = await SeedHouseholdWithAccountAsync(dbContext);
        var subcategoryId = await SeedPlatformSubcategoriesAsync(dbContext, count: 3);
        await SeedExpenseTransactionsAsync(dbContext, householdId, subcategoryId, totalExpenseCount: 3, categorizedExpenseCount: 2);

        var service = CreateService(dbContext, new TaxonomyReadinessOptions
        {
            EnableClassificationGate = true,
            EnableIngestionGate = true,
            MinimumPlatformSubcategoryCount = 1,
            MinimumTotalSubcategoryCount = 3,
            MinimumExpenseSampleCount = 3,
            MinimumExpenseFillRate = 0.6m,
        });

        var result = await service.EvaluateAsync(householdId, TaxonomyReadinessLane.Classification);

        Assert.True(result.IsReady);
        Assert.Equal(TaxonomyReadinessReasonCodes.Ready, result.ReasonCode);
        Assert.Equal(0.6667m, result.Snapshot.ExpenseFillRate);
    }

    private static TaxonomyReadinessGateService CreateService(MosaicMoneyDbContext dbContext, TaxonomyReadinessOptions options)
    {
        return new TaxonomyReadinessGateService(
            dbContext,
            Options.Create(options),
            NullLogger<TaxonomyReadinessGateService>.Instance);
    }

    private static MosaicMoneyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MosaicMoneyDbContext>()
            .UseInMemoryDatabase($"mosaicmoney-taxonomy-readiness-tests-{Guid.NewGuid()}")
            .Options;

        return new MosaicMoneyDbContext(options);
    }

    private static async Task<Guid> SeedHouseholdWithAccountAsync(MosaicMoneyDbContext dbContext)
    {
        var household = new Household
        {
            Id = Guid.NewGuid(),
            Name = "Readiness Test Household",
            CreatedAtUtc = DateTime.UtcNow,
        };

        var account = new Account
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            Name = "Primary Checking",
            InstitutionName = "Test Bank",
            ExternalAccountKey = "readiness-account-1",
            IsActive = true,
        };

        dbContext.Households.Add(household);
        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync();

        return household.Id;
    }

    private static async Task<Guid> SeedPlatformSubcategoriesAsync(MosaicMoneyDbContext dbContext, int count)
    {
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Expenses",
            DisplayOrder = 1,
            IsSystem = true,
            IsArchived = false,
            OwnerType = CategoryOwnerType.Platform,
            CreatedAtUtc = DateTime.UtcNow,
            LastModifiedAtUtc = DateTime.UtcNow,
        };

        dbContext.Categories.Add(category);

        Guid firstSubcategoryId = Guid.Empty;
        for (var index = 0; index < count; index++)
        {
            var subcategoryId = Guid.NewGuid();
            if (firstSubcategoryId == Guid.Empty)
            {
                firstSubcategoryId = subcategoryId;
            }

            dbContext.Subcategories.Add(new Subcategory
            {
                Id = subcategoryId,
                CategoryId = category.Id,
                Name = $"Expense Category {index + 1}",
                DisplayOrder = index,
                IsBusinessExpense = false,
                IsArchived = false,
                CreatedAtUtc = DateTime.UtcNow,
                LastModifiedAtUtc = DateTime.UtcNow,
            });
        }

        await dbContext.SaveChangesAsync();
        return firstSubcategoryId;
    }

    private static async Task SeedExpenseTransactionsAsync(
        MosaicMoneyDbContext dbContext,
        Guid householdId,
        Guid categorizedSubcategoryId,
        int totalExpenseCount,
        int categorizedExpenseCount)
    {
        var accountId = await dbContext.Accounts
            .Where(x => x.HouseholdId == householdId)
            .Select(x => x.Id)
            .SingleAsync();

        for (var index = 0; index < totalExpenseCount; index++)
        {
            dbContext.EnrichedTransactions.Add(new EnrichedTransaction
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                Description = $"Expense transaction {index + 1}",
                Amount = -10.00m,
                TransactionDate = new DateOnly(2026, 2, 20).AddDays(index),
                SubcategoryId = index < categorizedExpenseCount ? categorizedSubcategoryId : null,
                ReviewStatus = TransactionReviewStatus.None,
                CreatedAtUtc = DateTime.UtcNow,
                LastModifiedAtUtc = DateTime.UtcNow,
            });
        }

        await dbContext.SaveChangesAsync();
    }
}
