using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Data;

namespace MosaicMoney.Api.Domain.Ledger.AccessPolicy;

public static class AccountAccessPolicyBackfillReasonCodes
{
    public const string AmbiguousMultiMemberDefault = "access_policy_ambiguous_multi_member_default";
    public const string NoActiveHouseholdMember = "access_policy_no_active_household_member";
}

public sealed record AccountAccessPolicyBackfillResult(
    int ProcessedAccounts,
    int OwnerBackfillsApplied,
    int AmbiguityQueueEntriesUpserted,
    int ReviewFlagsEnabled,
    int ReviewFlagsCleared,
    int QueueEntriesResolved);

public sealed class AccountAccessPolicyBackfillService(
    MosaicMoneyDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<AccountAccessPolicyBackfillService> logger)
{
    public async Task<AccountAccessPolicyBackfillResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var activeAccounts = await dbContext.Accounts
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);

        if (activeAccounts.Count == 0)
        {
            return new AccountAccessPolicyBackfillResult(0, 0, 0, 0, 0, 0);
        }

        var activeMemberships = await dbContext.HouseholdUsers
            .Where(x => x.MembershipStatus == HouseholdMembershipStatus.Active)
            .Select(x => new { x.Id, x.HouseholdId })
            .ToListAsync(cancellationToken);

        var activeMemberIds = activeMemberships
            .Select(x => x.Id)
            .ToHashSet();

        var activeMembersByHousehold = activeMemberships
            .GroupBy(x => x.HouseholdId)
            .ToDictionary(
                x => x.Key,
                x => x.Select(y => y.Id).ToList());

        var existingAccessEntries = await dbContext.AccountMemberAccessEntries
            .ToListAsync(cancellationToken);

        var existingQueueEntries = await dbContext.AccountAccessPolicyReviewQueueEntries
            .ToDictionaryAsync(x => x.AccountId, cancellationToken);

        var ownerBackfillsApplied = 0;
        var ambiguityQueueEntriesUpserted = 0;
        var reviewFlagsEnabled = 0;
        var reviewFlagsCleared = 0;
        var queueEntriesResolved = 0;

        foreach (var account in activeAccounts)
        {
            var memberIds = activeMembersByHousehold.TryGetValue(account.HouseholdId, out var members)
                ? members
                : new List<Guid>();

            var hasActiveOwnerGrant = existingAccessEntries.Any(x =>
                x.AccountId == account.Id
                && x.AccessRole == AccountAccessRole.Owner
                && x.Visibility == AccountAccessVisibility.Visible
                && activeMemberIds.Contains(x.HouseholdUserId));

            if (hasActiveOwnerGrant)
            {
                if (account.AccessPolicyNeedsReview)
                {
                    account.AccessPolicyNeedsReview = false;
                    reviewFlagsCleared++;
                }

                if (TryResolveQueueEntry(existingQueueEntries, account.Id, now))
                {
                    queueEntriesResolved++;
                }

                continue;
            }

            if (memberIds.Count == 1)
            {
                var singleMemberId = memberIds[0];
                var accessEntry = existingAccessEntries.SingleOrDefault(x =>
                    x.AccountId == account.Id
                    && x.HouseholdUserId == singleMemberId);

                if (accessEntry is null)
                {
                    accessEntry = new AccountMemberAccess
                    {
                        AccountId = account.Id,
                        HouseholdUserId = singleMemberId,
                        AccessRole = AccountAccessRole.Owner,
                        Visibility = AccountAccessVisibility.Visible,
                        GrantedAtUtc = now,
                        LastModifiedAtUtc = now,
                    };

                    dbContext.AccountMemberAccessEntries.Add(accessEntry);
                    existingAccessEntries.Add(accessEntry);
                    ownerBackfillsApplied++;
                }
                else if (accessEntry.AccessRole != AccountAccessRole.Owner || accessEntry.Visibility != AccountAccessVisibility.Visible)
                {
                    accessEntry.AccessRole = AccountAccessRole.Owner;
                    accessEntry.Visibility = AccountAccessVisibility.Visible;
                    accessEntry.LastModifiedAtUtc = now;
                    ownerBackfillsApplied++;
                }

                if (account.AccessPolicyNeedsReview)
                {
                    account.AccessPolicyNeedsReview = false;
                    reviewFlagsCleared++;
                }

                if (TryResolveQueueEntry(existingQueueEntries, account.Id, now))
                {
                    queueEntriesResolved++;
                }

                continue;
            }

            var reasonCode = memberIds.Count == 0
                ? AccountAccessPolicyBackfillReasonCodes.NoActiveHouseholdMember
                : AccountAccessPolicyBackfillReasonCodes.AmbiguousMultiMemberDefault;

            var rationale = memberIds.Count == 0
                ? "No active household member could be deterministically identified as account owner during ACL backfill."
                : "Multiple active household members exist and ownership defaults are ambiguous; account remains fail-closed pending human review.";

            if (!account.AccessPolicyNeedsReview)
            {
                account.AccessPolicyNeedsReview = true;
                reviewFlagsEnabled++;
            }

            if (UpsertQueueEntry(dbContext, existingQueueEntries, account, reasonCode, rationale, now))
            {
                ambiguityQueueEntriesUpserted++;
            }
        }

        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var result = new AccountAccessPolicyBackfillResult(
            activeAccounts.Count,
            ownerBackfillsApplied,
            ambiguityQueueEntriesUpserted,
            reviewFlagsEnabled,
            reviewFlagsCleared,
            queueEntriesResolved);

        logger.LogInformation(
            "Account access policy backfill completed. Processed={ProcessedAccounts}, OwnerBackfills={OwnerBackfills}, AmbiguitiesQueued={AmbiguitiesQueued}, ReviewFlagsEnabled={ReviewFlagsEnabled}, ReviewFlagsCleared={ReviewFlagsCleared}, QueueEntriesResolved={QueueEntriesResolved}.",
            result.ProcessedAccounts,
            result.OwnerBackfillsApplied,
            result.AmbiguityQueueEntriesUpserted,
            result.ReviewFlagsEnabled,
            result.ReviewFlagsCleared,
            result.QueueEntriesResolved);

        return result;
    }

    private static bool TryResolveQueueEntry(
        IReadOnlyDictionary<Guid, AccountAccessPolicyReviewQueueEntry> queueEntries,
        Guid accountId,
        DateTime now)
    {
        if (!queueEntries.TryGetValue(accountId, out var queueEntry))
        {
            return false;
        }

        var changed = false;

        if (!queueEntry.ResolvedAtUtc.HasValue)
        {
            queueEntry.ResolvedAtUtc = now;
            changed = true;
        }

        if (queueEntry.LastEvaluatedAtUtc != now)
        {
            queueEntry.LastEvaluatedAtUtc = now;
            changed = true;
        }

        return changed;
    }

    private static bool UpsertQueueEntry(
        MosaicMoneyDbContext dbContext,
        IDictionary<Guid, AccountAccessPolicyReviewQueueEntry> queueEntries,
        Account account,
        string reasonCode,
        string rationale,
        DateTime now)
    {
        if (!queueEntries.TryGetValue(account.Id, out var queueEntry))
        {
            queueEntry = new AccountAccessPolicyReviewQueueEntry
            {
                AccountId = account.Id,
                HouseholdId = account.HouseholdId,
                ReasonCode = reasonCode,
                Rationale = rationale,
                EnqueuedAtUtc = now,
                LastEvaluatedAtUtc = now,
            };

            dbContext.AccountAccessPolicyReviewQueueEntries.Add(queueEntry);
            queueEntries[account.Id] = queueEntry;
            return true;
        }

        var changed = false;

        if (queueEntry.HouseholdId != account.HouseholdId)
        {
            queueEntry.HouseholdId = account.HouseholdId;
            changed = true;
        }

        if (!string.Equals(queueEntry.ReasonCode, reasonCode, StringComparison.Ordinal))
        {
            queueEntry.ReasonCode = reasonCode;
            changed = true;
        }

        if (!string.Equals(queueEntry.Rationale, rationale, StringComparison.Ordinal))
        {
            queueEntry.Rationale = rationale;
            changed = true;
        }

        if (queueEntry.ResolvedAtUtc.HasValue)
        {
            queueEntry.ResolvedAtUtc = null;
            changed = true;
        }

        if (queueEntry.LastEvaluatedAtUtc != now)
        {
            queueEntry.LastEvaluatedAtUtc = now;
            changed = true;
        }

        return changed;
    }
}
