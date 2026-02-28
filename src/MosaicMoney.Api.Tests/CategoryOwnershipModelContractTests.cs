using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class CategoryOwnershipModelContractTests
{
    [Fact]
    public void Category_DefaultsToPlatformOwnership()
    {
        var category = new Category();

        Assert.Equal(CategoryOwnerType.Platform, category.OwnerType);
        Assert.Null(category.HouseholdId);
        Assert.Null(category.OwnerUserId);
    }

    [Fact]
    public void DbModel_DeclaresCategoryOwnershipConstraintsAndScopedUniqueIndexes()
    {
        using var dbContext = CreateDbContext();

        var model = dbContext.GetService<IDesignTimeModel>().Model;
        var categoryEntity = model.FindEntityType(typeof(Category));

        Assert.NotNull(categoryEntity);

        var constraintNames = categoryEntity!
            .GetCheckConstraints()
            .Select(x => x.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("CK_Category_OwnerTypeRange", constraintNames);
        Assert.Contains("CK_Category_OwnerScopeConsistency", constraintNames);

        var platformUniqueIndex = categoryEntity
            .GetIndexes()
            .SingleOrDefault(x =>
                x.IsUnique
                && x.Properties.Count == 1
                && x.Properties[0].Name == nameof(Category.Name));

        Assert.NotNull(platformUniqueIndex);
        Assert.Equal("\"OwnerType\" = 0 AND \"HouseholdId\" IS NULL AND \"OwnerUserId\" IS NULL", platformUniqueIndex!.GetFilter());

        var householdSharedUniqueIndex = categoryEntity
            .GetIndexes()
            .SingleOrDefault(x =>
                x.IsUnique
                && x.Properties.Count == 2
                && x.Properties[0].Name == nameof(Category.HouseholdId)
                && x.Properties[1].Name == nameof(Category.Name));

        Assert.NotNull(householdSharedUniqueIndex);
        Assert.Equal("\"OwnerType\" = 1 AND \"HouseholdId\" IS NOT NULL AND \"OwnerUserId\" IS NULL", householdSharedUniqueIndex!.GetFilter());

        var userOwnedUniqueIndex = categoryEntity
            .GetIndexes()
            .SingleOrDefault(x =>
                x.IsUnique
                && x.Properties.Count == 3
                && x.Properties[0].Name == nameof(Category.HouseholdId)
                && x.Properties[1].Name == nameof(Category.OwnerUserId)
                && x.Properties[2].Name == nameof(Category.Name));

        Assert.NotNull(userOwnedUniqueIndex);
        Assert.Equal("\"OwnerType\" = 2 AND \"HouseholdId\" IS NOT NULL AND \"OwnerUserId\" IS NOT NULL", userOwnedUniqueIndex!.GetFilter());
    }

    [Fact]
    public void DbModel_DeclaresCategoryOwnershipRelationships()
    {
        using var dbContext = CreateDbContext();

        var model = dbContext.GetService<IDesignTimeModel>().Model;
        var categoryEntity = model.FindEntityType(typeof(Category));

        Assert.NotNull(categoryEntity);

        var householdForeignKey = categoryEntity!
            .GetForeignKeys()
            .SingleOrDefault(x =>
                x.Properties.Count == 1
                && x.Properties[0].Name == nameof(Category.HouseholdId));

        Assert.NotNull(householdForeignKey);
        Assert.Equal(typeof(Household), householdForeignKey!.PrincipalEntityType.ClrType);

        var ownerUserForeignKey = categoryEntity
            .GetForeignKeys()
            .SingleOrDefault(x =>
                x.Properties.Count == 1
                && x.Properties[0].Name == nameof(Category.OwnerUserId));

        Assert.NotNull(ownerUserForeignKey);
        Assert.Equal(typeof(HouseholdUser), ownerUserForeignKey!.PrincipalEntityType.ClrType);
    }

    private static MosaicMoneyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MosaicMoneyDbContext>()
            .UseInMemoryDatabase($"mosaicmoney-category-ownership-tests-{Guid.NewGuid()}")
            .Options;

        return new MosaicMoneyDbContext(options);
    }
}
