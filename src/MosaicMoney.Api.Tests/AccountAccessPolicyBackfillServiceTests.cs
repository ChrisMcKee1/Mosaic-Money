using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.AccessPolicy;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class AccountAccessPolicyBackfillServiceTests
{
    [Fact]
    public async Task ExecuteAsync_SingleActiveMember_BackfillsVisibleOwnerGrant()
    {
        using var dbContext = CreateDbContext();
        var now = new DateTime(2026, 2, 25, 12, 0, 0, DateTimeKind.Utc);

        var household = new Household { Id = Guid.NewGuid(), Name = "Test Household" };
        var member = new HouseholdUser
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            DisplayName = "Primary Member",
            MembershipStatus = HouseholdMembershipStatus.Active,
        };

        var account = new Account
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            Name = "Checking",
            IsActive = true,
        };

        dbContext.AddRange(household, member, account);
        await dbContext.SaveChangesAsync();

        var service = new AccountAccessPolicyBackfillService(
            dbContext,
            new FixedTimeProvider(now),
            NullLogger<AccountAccessPolicyBackfillService>.Instance);

        var result = await service.ExecuteAsync();

        Assert.Equal(1, result.OwnerBackfillsApplied);

        var accessEntry = await dbContext.AccountMemberAccessEntries
            .SingleAsync(x => x.AccountId == account.Id && x.HouseholdUserId == member.Id);

        Assert.Equal(AccountAccessRole.Owner, accessEntry.AccessRole);
        Assert.Equal(AccountAccessVisibility.Visible, accessEntry.Visibility);
        Assert.False((await dbContext.Accounts.SingleAsync(x => x.Id == account.Id)).AccessPolicyNeedsReview);
        Assert.Empty(await dbContext.AccountAccessPolicyReviewQueueEntries.ToListAsync());
    }

    [Fact]
    public async Task ExecuteAsync_AmbiguousMultiMember_KeepsFailClosedAndQueuesReview()
    {
        using var dbContext = CreateDbContext();
        var now = new DateTime(2026, 2, 25, 13, 0, 0, DateTimeKind.Utc);

        var household = new Household { Id = Guid.NewGuid(), Name = "Ambiguous Household" };
        var memberA = new HouseholdUser
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            DisplayName = "Member A",
            MembershipStatus = HouseholdMembershipStatus.Active,
        };

        var memberB = new HouseholdUser
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            DisplayName = "Member B",
            MembershipStatus = HouseholdMembershipStatus.Active,
        };

        var account = new Account
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            Name = "Joint Credit",
            IsActive = true,
        };

        dbContext.AddRange(household, memberA, memberB, account);
        await dbContext.SaveChangesAsync();

        var service = new AccountAccessPolicyBackfillService(
            dbContext,
            new FixedTimeProvider(now),
            NullLogger<AccountAccessPolicyBackfillService>.Instance);

        var result = await service.ExecuteAsync();

        Assert.Equal(1, result.AmbiguityQueueEntriesUpserted);
        Assert.Empty(await dbContext.AccountMemberAccessEntries
            .Where(x => x.AccountId == account.Id)
            .ToListAsync());

        var reloadedAccount = await dbContext.Accounts.SingleAsync(x => x.Id == account.Id);
        Assert.True(reloadedAccount.AccessPolicyNeedsReview);

        var queueEntry = await dbContext.AccountAccessPolicyReviewQueueEntries.SingleAsync(x => x.AccountId == account.Id);
        Assert.Equal(AccountAccessPolicyBackfillReasonCodes.AmbiguousMultiMemberDefault, queueEntry.ReasonCode);
        Assert.Null(queueEntry.ResolvedAtUtc);
    }

    [Fact]
    public async Task ExecuteAsync_Rerun_IsIdempotentForAclAndQueueRows()
    {
        using var dbContext = CreateDbContext();
        var now = new DateTime(2026, 2, 25, 14, 0, 0, DateTimeKind.Utc);

        var householdSingle = new Household { Id = Guid.NewGuid(), Name = "Single" };
        var singleMember = new HouseholdUser
        {
            Id = Guid.NewGuid(),
            HouseholdId = householdSingle.Id,
            DisplayName = "Only Member",
            MembershipStatus = HouseholdMembershipStatus.Active,
        };

        var singleAccount = new Account
        {
            Id = Guid.NewGuid(),
            HouseholdId = householdSingle.Id,
            Name = "Single Account",
            IsActive = true,
        };

        var householdAmbiguous = new Household { Id = Guid.NewGuid(), Name = "Ambiguous" };
        var ambiguousMemberA = new HouseholdUser
        {
            Id = Guid.NewGuid(),
            HouseholdId = householdAmbiguous.Id,
            DisplayName = "Ambiguous A",
            MembershipStatus = HouseholdMembershipStatus.Active,
        };

        var ambiguousMemberB = new HouseholdUser
        {
            Id = Guid.NewGuid(),
            HouseholdId = householdAmbiguous.Id,
            DisplayName = "Ambiguous B",
            MembershipStatus = HouseholdMembershipStatus.Active,
        };

        var ambiguousAccount = new Account
        {
            Id = Guid.NewGuid(),
            HouseholdId = householdAmbiguous.Id,
            Name = "Ambiguous Account",
            IsActive = true,
        };

        dbContext.AddRange(
            householdSingle,
            singleMember,
            singleAccount,
            householdAmbiguous,
            ambiguousMemberA,
            ambiguousMemberB,
            ambiguousAccount);

        await dbContext.SaveChangesAsync();

        var service = new AccountAccessPolicyBackfillService(
            dbContext,
            new FixedTimeProvider(now),
            NullLogger<AccountAccessPolicyBackfillService>.Instance);

        var firstRun = await service.ExecuteAsync();
        var secondRun = await service.ExecuteAsync();

        Assert.Equal(1, firstRun.OwnerBackfillsApplied);
        Assert.Equal(1, firstRun.AmbiguityQueueEntriesUpserted);
        Assert.Equal(0, secondRun.OwnerBackfillsApplied);
        Assert.Equal(0, secondRun.AmbiguityQueueEntriesUpserted);

        Assert.Single(await dbContext.AccountMemberAccessEntries
            .Where(x => x.AccountId == singleAccount.Id)
            .ToListAsync());

        Assert.Single(await dbContext.AccountAccessPolicyReviewQueueEntries
            .Where(x => x.AccountId == ambiguousAccount.Id)
            .ToListAsync());

        Assert.Empty(await dbContext.AccountMemberAccessEntries
            .Where(x => x.AccountId == ambiguousAccount.Id)
            .ToListAsync());
    }

    private static MosaicMoneyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MosaicMoneyDbContext>()
            .UseInMemoryDatabase($"mosaicmoney-account-access-backfill-tests-{Guid.NewGuid()}")
            .Options;

        return new MosaicMoneyDbContext(options);
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(utcNow);
    }
}
