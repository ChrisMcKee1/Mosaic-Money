using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class PlaidAccountLinkModelContractTests
{
    [Fact]
    public void PlaidAccountLink_DefaultsToActive()
    {
        var link = new PlaidAccountLink();

        Assert.True(link.IsActive);
    }

    [Fact]
    public void DbModel_DeclaresPlaidAccountLinkConstraintsAndFilteredUniqueness()
    {
        using var dbContext = CreateDbContext();

        var model = dbContext.GetService<IDesignTimeModel>().Model;
        var linkEntity = model.FindEntityType(typeof(PlaidAccountLink));

        Assert.NotNull(linkEntity);

        var constraintNames = linkEntity!
            .GetCheckConstraints()
            .Select(x => x.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("CK_PlaidAccountLink_ItemIdRequired", constraintNames);
        Assert.Contains("CK_PlaidAccountLink_PlaidAccountIdRequired", constraintNames);
        Assert.Contains("CK_PlaidAccountLink_UnlinkAudit", constraintNames);
        Assert.Contains("CK_PlaidAccountLink_LastSeenAfterLinked", constraintNames);

        var activePlaidKeyIndex = linkEntity
            .GetIndexes()
            .SingleOrDefault(x =>
                x.IsUnique
                && x.Properties.Count == 3
                && x.Properties[0].Name == nameof(PlaidAccountLink.PlaidEnvironment)
                && x.Properties[1].Name == nameof(PlaidAccountLink.ItemId)
                && x.Properties[2].Name == nameof(PlaidAccountLink.PlaidAccountId));

        Assert.NotNull(activePlaidKeyIndex);
        Assert.Equal("\"IsActive\" = TRUE", activePlaidKeyIndex!.GetFilter());

        var activeAccountIndex = linkEntity
            .GetIndexes()
            .SingleOrDefault(x =>
                x.IsUnique
                && x.Properties.Count == 1
                && x.Properties[0].Name == nameof(PlaidAccountLink.AccountId));

        Assert.NotNull(activeAccountIndex);
        Assert.Equal("\"IsActive\" = TRUE", activeAccountIndex!.GetFilter());
    }

    [Fact]
    public void DbModel_DeclaresPlaidAccountLinkRelationships()
    {
        using var dbContext = CreateDbContext();

        var model = dbContext.GetService<IDesignTimeModel>().Model;
        var linkEntity = model.FindEntityType(typeof(PlaidAccountLink));

        Assert.NotNull(linkEntity);

        var accountFk = linkEntity!
            .GetForeignKeys()
            .SingleOrDefault(x => x.Properties.Any(p => p.Name == nameof(PlaidAccountLink.AccountId)));

        Assert.NotNull(accountFk);
        Assert.Equal(typeof(Account), accountFk!.PrincipalEntityType.ClrType);

        var credentialFk = linkEntity
            .GetForeignKeys()
            .SingleOrDefault(x => x.Properties.Any(p => p.Name == nameof(PlaidAccountLink.PlaidItemCredentialId)));

        Assert.NotNull(credentialFk);
        Assert.Equal(typeof(PlaidItemCredential), credentialFk!.PrincipalEntityType.ClrType);
    }

    private static MosaicMoneyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MosaicMoneyDbContext>()
            .UseInMemoryDatabase($"mosaicmoney-plaid-account-link-tests-{Guid.NewGuid()}")
            .Options;

        return new MosaicMoneyDbContext(options);
    }
}
