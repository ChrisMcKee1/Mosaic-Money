using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class AccountMemberAccessModelContractTests
{
    [Fact]
    public void AccountMemberAccess_DefaultsToHiddenNoAccess()
    {
        var access = new AccountMemberAccess();

        Assert.Equal(AccountAccessRole.None, access.AccessRole);
        Assert.Equal(AccountAccessVisibility.Hidden, access.Visibility);
    }

    [Fact]
    public void DbModel_DeclaresAclKeyConstraintsAndIndex()
    {
        using var dbContext = CreateDbContext();

        var model = dbContext.GetService<IDesignTimeModel>().Model;
        var aclEntity = model.FindEntityType(typeof(AccountMemberAccess));

        Assert.NotNull(aclEntity);

        var key = aclEntity!.FindPrimaryKey();
        Assert.NotNull(key);
        Assert.Equal(2, key!.Properties.Count);
        Assert.Equal(nameof(AccountMemberAccess.AccountId), key.Properties[0].Name);
        Assert.Equal(nameof(AccountMemberAccess.HouseholdUserId), key.Properties[1].Name);

        var constraintNames = aclEntity
            .GetCheckConstraints()
            .Select(x => x.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("CK_AccountMemberAccess_AccessRoleRange", constraintNames);
        Assert.Contains("CK_AccountMemberAccess_VisibilityRange", constraintNames);
        Assert.Contains("CK_AccountMemberAccess_AccessVisibilityConsistency", constraintNames);

        var lookupIndex = aclEntity
            .GetIndexes()
            .SingleOrDefault(x =>
                x.Properties.Count == 3
                && x.Properties[0].Name == nameof(AccountMemberAccess.HouseholdUserId)
                && x.Properties[1].Name == nameof(AccountMemberAccess.AccessRole)
                && x.Properties[2].Name == nameof(AccountMemberAccess.Visibility));

        Assert.NotNull(lookupIndex);
    }

    [Fact]
    public void DbModel_DeclaresAclRelationshipsToAccountAndHouseholdUser()
    {
        using var dbContext = CreateDbContext();

        var model = dbContext.GetService<IDesignTimeModel>().Model;
        var aclEntity = model.FindEntityType(typeof(AccountMemberAccess));

        Assert.NotNull(aclEntity);

        var accountFk = aclEntity!
            .GetForeignKeys()
            .SingleOrDefault(x =>
                x.Properties.Count == 1
                && x.Properties[0].Name == nameof(AccountMemberAccess.AccountId));

        Assert.NotNull(accountFk);
        Assert.Equal(typeof(Account), accountFk!.PrincipalEntityType.ClrType);

        var memberFk = aclEntity
            .GetForeignKeys()
            .SingleOrDefault(x =>
                x.Properties.Count == 1
                && x.Properties[0].Name == nameof(AccountMemberAccess.HouseholdUserId));

        Assert.NotNull(memberFk);
        Assert.Equal(typeof(HouseholdUser), memberFk!.PrincipalEntityType.ClrType);
    }

    private static MosaicMoneyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MosaicMoneyDbContext>()
            .UseInMemoryDatabase($"mosaicmoney-account-acl-tests-{Guid.NewGuid()}")
            .Options;

        return new MosaicMoneyDbContext(options);
    }
}
