using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class AccountAccessPolicyReviewQueueModelContractTests
{
    [Fact]
    public void Account_DefaultsToNoAccessPolicyReview()
    {
        var account = new Account();

        Assert.False(account.AccessPolicyNeedsReview);
    }

    [Fact]
    public void DbModel_DeclaresReviewQueueKeyConstraintsAndRelationships()
    {
        using var dbContext = CreateDbContext();

        var model = dbContext.GetService<IDesignTimeModel>().Model;
        var queueEntity = model.FindEntityType(typeof(AccountAccessPolicyReviewQueueEntry));

        Assert.NotNull(queueEntity);

        var key = queueEntity!.FindPrimaryKey();
        Assert.NotNull(key);
        Assert.Single(key!.Properties);
        Assert.Equal(nameof(AccountAccessPolicyReviewQueueEntry.AccountId), key.Properties[0].Name);

        var constraintNames = queueEntity
            .GetCheckConstraints()
            .Select(x => x.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("CK_AccountAccessPolicyReviewQueueEntry_ReasonCodeRequired", constraintNames);
        Assert.Contains("CK_AccountAccessPolicyReviewQueueEntry_RationaleRequired", constraintNames);
        Assert.Contains("CK_AccountAccessPolicyReviewQueueEntry_EvaluationAfterEnqueue", constraintNames);
        Assert.Contains("CK_AccountAccessPolicyReviewQueueEntry_ResolutionAfterEnqueue", constraintNames);

        var accountForeignKey = queueEntity
            .GetForeignKeys()
            .SingleOrDefault(x => x.Properties.Any(p => p.Name == nameof(AccountAccessPolicyReviewQueueEntry.AccountId)));

        Assert.NotNull(accountForeignKey);
        Assert.Equal(typeof(Account), accountForeignKey!.PrincipalEntityType.ClrType);

        var householdForeignKey = queueEntity
            .GetForeignKeys()
            .SingleOrDefault(x => x.Properties.Any(p => p.Name == nameof(AccountAccessPolicyReviewQueueEntry.HouseholdId)));

        Assert.NotNull(householdForeignKey);
        Assert.Equal(typeof(Household), householdForeignKey!.PrincipalEntityType.ClrType);
    }

    private static MosaicMoneyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MosaicMoneyDbContext>()
            .UseInMemoryDatabase($"mosaicmoney-account-access-policy-review-model-tests-{Guid.NewGuid()}")
            .Options;

        return new MosaicMoneyDbContext(options);
    }
}
