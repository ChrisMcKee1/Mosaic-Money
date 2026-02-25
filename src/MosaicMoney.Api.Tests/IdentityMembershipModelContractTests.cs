using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class IdentityMembershipModelContractTests
{
    [Fact]
    public void HouseholdUser_DefaultMembershipStatus_IsActive()
    {
        var householdUser = new HouseholdUser();

        Assert.Equal(HouseholdMembershipStatus.Active, householdUser.MembershipStatus);
    }

    [Fact]
    public void DbModel_DeclaresIdentityAndMembershipConstraintsAndIndexes()
    {
        using var dbContext = CreateDbContext();

        var model = dbContext.GetService<IDesignTimeModel>().Model;

        var mosaicUserEntity = model.FindEntityType(typeof(MosaicUser));
        Assert.NotNull(mosaicUserEntity);

        var mosaicConstraintNames = mosaicUserEntity!
            .GetCheckConstraints()
            .Select(x => x.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("CK_MosaicUser_AuthProviderRequired", mosaicConstraintNames);
        Assert.Contains("CK_MosaicUser_AuthSubjectRequired", mosaicConstraintNames);

        var authSubjectUniqueIndex = mosaicUserEntity
            .GetIndexes()
            .SingleOrDefault(x =>
                x.IsUnique
                && x.Properties.Count == 2
                && x.Properties[0].Name == nameof(MosaicUser.AuthProvider)
                && x.Properties[1].Name == nameof(MosaicUser.AuthSubject));

        Assert.NotNull(authSubjectUniqueIndex);

        var householdUserEntity = model.FindEntityType(typeof(HouseholdUser));
        Assert.NotNull(householdUserEntity);

        var householdConstraintNames = householdUserEntity!
            .GetCheckConstraints()
            .Select(x => x.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("CK_HouseholdUser_MembershipStatusRange", householdConstraintNames);
        Assert.Contains("CK_HouseholdUser_RemovedAudit", householdConstraintNames);
        Assert.Contains("CK_HouseholdUser_ActivationAfterInvite", householdConstraintNames);

        var activeMembershipUniqueIndex = householdUserEntity
            .GetIndexes()
            .SingleOrDefault(x =>
                x.IsUnique
                && x.Properties.Count == 2
                && x.Properties[0].Name == nameof(HouseholdUser.HouseholdId)
                && x.Properties[1].Name == nameof(HouseholdUser.MosaicUserId));

        Assert.NotNull(activeMembershipUniqueIndex);
        Assert.Equal("\"MembershipStatus\" = 1 AND \"MosaicUserId\" IS NOT NULL", activeMembershipUniqueIndex!.GetFilter());
    }

    [Fact]
    public void DbModel_KeepsNeedsReviewByUserReference_StableOnHouseholdUser()
    {
        using var dbContext = CreateDbContext();

        var model = dbContext.GetService<IDesignTimeModel>().Model;

        var enrichedTransactionEntity = model.FindEntityType(typeof(EnrichedTransaction));
        Assert.NotNull(enrichedTransactionEntity);

        var needsReviewForeignKey = enrichedTransactionEntity!
            .GetForeignKeys()
            .SingleOrDefault(x => x.Properties.Any(p => p.Name == nameof(EnrichedTransaction.NeedsReviewByUserId)));

        Assert.NotNull(needsReviewForeignKey);
        Assert.Equal(typeof(HouseholdUser), needsReviewForeignKey!.PrincipalEntityType.ClrType);
    }

    private static MosaicMoneyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MosaicMoneyDbContext>()
            .UseInMemoryDatabase($"mosaicmoney-identity-membership-tests-{Guid.NewGuid()}")
            .Options;

        return new MosaicMoneyDbContext(options);
    }
}
