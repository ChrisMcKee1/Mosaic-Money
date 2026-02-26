using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using MosaicMoney.Api.Contracts.V1;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;

namespace MosaicMoney.Api.Apis;

public static class HouseholdEndpoints
{
    private const string HouseholdUserIdHeaderName = "X-Mosaic-Household-User-Id";
    private const string MosaicHouseholdUserIdClaimType = "mosaic_household_user_id";
    private const string HouseholdUserIdClaimType = "household_user_id";

    private static readonly HashSet<string> AllowedInviteRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Member",
        "Admin",
        "Owner",
    };

    private static readonly HashSet<string> AllowedSharingPresets = new(StringComparer.OrdinalIgnoreCase)
    {
        "Mine",
        "Joint",
        "Shared",
    };

    public static RouteGroupBuilder MapHouseholdEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/households", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            CreateHouseholdRequest request,
            CancellationToken cancellationToken) =>
        {
            var errors = ApiValidation.ValidateDataAnnotations(request);
            if (errors.Count > 0)
            {
                return ApiValidation.ToValidationResult(httpContext, errors);
            }

            var household = new Household
            {
                Id = Guid.CreateVersion7(),
                Name = request.Name,
                CreatedAtUtc = DateTime.UtcNow,
            };

            dbContext.Households.Add(household);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Created(
                $"/api/v1/households/{household.Id}",
                new HouseholdDto(household.Id, household.Name, household.CreatedAtUtc));
        });

        group.MapGet("/households", async (
            MosaicMoneyDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var households = await dbContext.Households
                .AsNoTracking()
                .OrderByDescending(h => h.CreatedAtUtc)
                .Select(h => new HouseholdDto(h.Id, h.Name, h.CreatedAtUtc))
                .ToListAsync(cancellationToken);

            return Results.Ok(households);
        });

        group.MapGet("/households/{id:guid}", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            Guid id,
            CancellationToken cancellationToken) =>
        {
            var household = await dbContext.Households
                .AsNoTracking()
                .Where(h => h.Id == id)
                .Select(h => new HouseholdDto(h.Id, h.Name, h.CreatedAtUtc))
                .FirstOrDefaultAsync(cancellationToken);

            if (household is null)
            {
                return ApiValidation.ToNotFoundResult(
                    httpContext,
                    "household_not_found",
                    "The requested household was not found.");
            }

            return Results.Ok(household);
        });

        group.MapGet("/households/{id:guid}/members", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            Guid id,
            CancellationToken cancellationToken) =>
        {
            if (!await dbContext.Households.AsNoTracking().AnyAsync(x => x.Id == id, cancellationToken))
            {
                return ApiValidation.ToNotFoundResult(
                    httpContext,
                    "household_not_found",
                    "The requested household was not found.");
            }

            var members = await dbContext.HouseholdUsers
                .AsNoTracking()
                .Where(x => x.HouseholdId == id && x.MembershipStatus == HouseholdMembershipStatus.Active)
                .OrderBy(x => x.DisplayName)
                .Select(x => new HouseholdMemberDto(
                    x.Id,
                    x.HouseholdId,
                    x.DisplayName,
                    x.ExternalUserKey,
                    x.MembershipStatus.ToString(),
                    dbContext.AccountMemberAccessEntries.Any(access =>
                        access.HouseholdUserId == x.Id
                        && access.AccessRole == AccountAccessRole.Owner
                        && access.Visibility == AccountAccessVisibility.Visible)
                        ? "Owner"
                        : "Member",
                    x.InvitedAtUtc,
                    x.ActivatedAtUtc,
                    x.RemovedAtUtc))
                .ToListAsync(cancellationToken);

            return Results.Ok(members);
        });

        group.MapGet("/households/{id:guid}/invites", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            Guid id,
            CancellationToken cancellationToken) =>
        {
            if (!await dbContext.Households.AsNoTracking().AnyAsync(x => x.Id == id, cancellationToken))
            {
                return ApiValidation.ToNotFoundResult(
                    httpContext,
                    "household_not_found",
                    "The requested household was not found.");
            }

            var invites = await dbContext.HouseholdUsers
                .AsNoTracking()
                .Where(x => x.HouseholdId == id && x.MembershipStatus == HouseholdMembershipStatus.Invited)
                .OrderByDescending(x => x.InvitedAtUtc)
                .Select(x => new HouseholdInviteDto(
                    x.Id,
                    x.HouseholdId,
                    x.ExternalUserKey ?? x.DisplayName,
                    "Member",
                    x.MembershipStatus.ToString(),
                    x.InvitedAtUtc,
                    x.RemovedAtUtc))
                .ToListAsync(cancellationToken);

            return Results.Ok(invites);
        });

        group.MapPost("/households/{id:guid}/invites", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            Guid id,
            CreateHouseholdInviteRequest request,
            CancellationToken cancellationToken) =>
        {
            var errors = ValidateCreateHouseholdInviteRequest(request);
            if (errors.Count > 0)
            {
                return ApiValidation.ToValidationResult(httpContext, errors);
            }

            if (!await dbContext.Households.AsNoTracking().AnyAsync(x => x.Id == id, cancellationToken))
            {
                return ApiValidation.ToNotFoundResult(
                    httpContext,
                    "household_not_found",
                    "The requested household was not found.");
            }

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var existingMember = await dbContext.HouseholdUsers
                .FirstOrDefaultAsync(
                    x => x.HouseholdId == id && x.ExternalUserKey == normalizedEmail,
                    cancellationToken);

            if (existingMember is not null && existingMember.MembershipStatus == HouseholdMembershipStatus.Active)
            {
                return ApiValidation.ToConflictResult(
                    httpContext,
                    "household_member_already_active",
                    "A household member with this email is already active.");
            }

            if (existingMember is not null && existingMember.MembershipStatus == HouseholdMembershipStatus.Invited)
            {
                return ApiValidation.ToConflictResult(
                    httpContext,
                    "household_invite_already_pending",
                    "An invite is already pending for this email.");
            }

            var invitedAtUtc = DateTime.UtcNow;
            var displayName = BuildDefaultDisplayName(normalizedEmail);

            if (existingMember is null)
            {
                existingMember = new HouseholdUser
                {
                    Id = Guid.CreateVersion7(),
                    HouseholdId = id,
                    DisplayName = displayName,
                    ExternalUserKey = normalizedEmail,
                    MembershipStatus = HouseholdMembershipStatus.Invited,
                    InvitedAtUtc = invitedAtUtc,
                    ActivatedAtUtc = null,
                    RemovedAtUtc = null,
                };

                dbContext.HouseholdUsers.Add(existingMember);
            }
            else
            {
                existingMember.DisplayName = string.IsNullOrWhiteSpace(existingMember.DisplayName)
                    ? displayName
                    : existingMember.DisplayName;
                existingMember.ExternalUserKey = normalizedEmail;
                existingMember.MembershipStatus = HouseholdMembershipStatus.Invited;
                existingMember.InvitedAtUtc = invitedAtUtc;
                existingMember.ActivatedAtUtc = null;
                existingMember.RemovedAtUtc = null;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Created(
                $"/api/v1/households/{id}/invites/{existingMember.Id}",
                new HouseholdInviteDto(
                    existingMember.Id,
                    existingMember.HouseholdId,
                    existingMember.ExternalUserKey ?? normalizedEmail,
                    request.Role,
                    existingMember.MembershipStatus.ToString(),
                    existingMember.InvitedAtUtc,
                    existingMember.RemovedAtUtc));
        });

        group.MapPost("/households/{id:guid}/invites/{inviteId:guid}/accept", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            Guid id,
            Guid inviteId,
            AcceptHouseholdInviteRequest request,
            CancellationToken cancellationToken) =>
        {
            var errors = ApiValidation.ValidateDataAnnotations(request);
            if (errors.Count > 0)
            {
                return ApiValidation.ToValidationResult(httpContext, errors);
            }

            var invite = await dbContext.HouseholdUsers
                .FirstOrDefaultAsync(x => x.Id == inviteId && x.HouseholdId == id, cancellationToken);

            if (invite is null)
            {
                return ApiValidation.ToNotFoundResult(
                    httpContext,
                    "household_invite_not_found",
                    "The requested household invite was not found.");
            }

            if (invite.MembershipStatus != HouseholdMembershipStatus.Invited)
            {
                return ApiValidation.ToConflictResult(
                    httpContext,
                    "household_invite_not_pending",
                    "Only pending invites can be accepted.");
            }

            var now = DateTime.UtcNow;
            invite.MembershipStatus = HouseholdMembershipStatus.Active;
            invite.ActivatedAtUtc = now;
            invite.RemovedAtUtc = null;

            if (!string.IsNullOrWhiteSpace(request.DisplayName))
            {
                invite.DisplayName = request.DisplayName.Trim();
            }
            else if (string.IsNullOrWhiteSpace(invite.DisplayName))
            {
                invite.DisplayName = BuildDefaultDisplayName(invite.ExternalUserKey ?? "Household Member");
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(new HouseholdMemberDto(
                invite.Id,
                invite.HouseholdId,
                invite.DisplayName,
                invite.ExternalUserKey,
                invite.MembershipStatus.ToString(),
                "Member",
                invite.InvitedAtUtc,
                invite.ActivatedAtUtc,
                invite.RemovedAtUtc));
        });

        group.MapDelete("/households/{id:guid}/members/{memberId:guid}", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            Guid id,
            Guid memberId,
            CancellationToken cancellationToken) =>
        {
            var member = await dbContext.HouseholdUsers
                .FirstOrDefaultAsync(x => x.Id == memberId && x.HouseholdId == id, cancellationToken);

            if (member is null)
            {
                return ApiValidation.ToNotFoundResult(
                    httpContext,
                    "household_member_not_found",
                    "The requested household member was not found.");
            }

            if (member.MembershipStatus != HouseholdMembershipStatus.Active)
            {
                return ApiValidation.ToConflictResult(
                    httpContext,
                    "household_member_not_active",
                    "Only active members can be removed.");
            }

            var activeMemberCount = await dbContext.HouseholdUsers
                .CountAsync(
                    x => x.HouseholdId == id && x.MembershipStatus == HouseholdMembershipStatus.Active,
                    cancellationToken);

            if (activeMemberCount <= 1)
            {
                return ApiValidation.ToConflictResult(
                    httpContext,
                    "household_last_member_removal_denied",
                    "At least one active household member is required.");
            }

            var now = DateTime.UtcNow;
            member.MembershipStatus = HouseholdMembershipStatus.Removed;
            member.RemovedAtUtc = now;
            member.ExternalUserKey = null;

            var accessEntries = await dbContext.AccountMemberAccessEntries
                .Where(x => x.HouseholdUserId == memberId)
                .ToListAsync(cancellationToken);

            foreach (var accessEntry in accessEntries)
            {
                accessEntry.AccessRole = AccountAccessRole.None;
                accessEntry.Visibility = AccountAccessVisibility.Hidden;
                accessEntry.LastModifiedAtUtc = now;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });

        group.MapDelete("/households/{id:guid}/invites/{inviteId:guid}", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            Guid id,
            Guid inviteId,
            CancellationToken cancellationToken) =>
        {
            var invite = await dbContext.HouseholdUsers
                .FirstOrDefaultAsync(x => x.Id == inviteId && x.HouseholdId == id, cancellationToken);

            if (invite is null)
            {
                return ApiValidation.ToNotFoundResult(
                    httpContext,
                    "household_invite_not_found",
                    "The requested household invite was not found.");
            }

            if (invite.MembershipStatus != HouseholdMembershipStatus.Invited)
            {
                return ApiValidation.ToConflictResult(
                    httpContext,
                    "household_invite_not_pending",
                    "Only pending invites can be cancelled.");
            }

            invite.MembershipStatus = HouseholdMembershipStatus.Removed;
            invite.RemovedAtUtc = DateTime.UtcNow;
            invite.ExternalUserKey = null;

            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });

        group.MapGet("/households/{id:guid}/account-access", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            Guid id,
            CancellationToken cancellationToken) =>
        {
            var memberScope = await ResolveActiveHouseholdMemberScopeAsync(httpContext, dbContext, id, cancellationToken);
            if (memberScope.ErrorResult is not null)
            {
                return memberScope.ErrorResult;
            }

            var activeMemberIds = await dbContext.HouseholdUsers
                .AsNoTracking()
                .Where(x => x.HouseholdId == id && x.MembershipStatus == HouseholdMembershipStatus.Active)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            if (activeMemberIds.Count == 0)
            {
                return Results.Ok(Array.Empty<HouseholdAccountAccessSummaryDto>());
            }

            var accessEntries = await dbContext.AccountMemberAccessEntries
                .AsNoTracking()
                .Where(x =>
                    x.Account.HouseholdId == id
                    && x.Account.IsActive
                    && activeMemberIds.Contains(x.HouseholdUserId))
                .Select(x => new AccountAccessProjection(
                    x.AccountId,
                    x.HouseholdUserId,
                    x.AccessRole,
                    x.Visibility,
                    x.LastModifiedAtUtc))
                .ToListAsync(cancellationToken);

            var visibleAccountIds = accessEntries
                .Where(x =>
                    x.HouseholdUserId == memberScope.HouseholdUserId
                    && x.Visibility == AccountAccessVisibility.Visible
                    && x.AccessRole != AccountAccessRole.None)
                .Select(x => x.AccountId)
                .Distinct()
                .ToList();

            if (visibleAccountIds.Count == 0)
            {
                return Results.Ok(Array.Empty<HouseholdAccountAccessSummaryDto>());
            }

            var accounts = await dbContext.Accounts
                .AsNoTracking()
                .Where(x => x.HouseholdId == id && x.IsActive && visibleAccountIds.Contains(x.Id))
                .OrderBy(x => x.Name)
                .Select(x => new
                {
                    x.Id,
                    x.HouseholdId,
                    x.Name,
                    x.InstitutionName,
                    x.IsActive,
                })
                .ToListAsync(cancellationToken);

            var accessByAccountId = accessEntries
                .GroupBy(x => x.AccountId)
                .ToDictionary(x => x.Key, x => x.ToList());

            var response = new List<HouseholdAccountAccessSummaryDto>(accounts.Count);
            foreach (var account in accounts)
            {
                var grants = accessByAccountId.GetValueOrDefault(account.Id) ?? [];
                var currentMemberGrant = grants.FirstOrDefault(x => x.HouseholdUserId == memberScope.HouseholdUserId);

                if (currentMemberGrant is null)
                {
                    continue;
                }

                response.Add(new HouseholdAccountAccessSummaryDto(
                    account.Id,
                    account.HouseholdId,
                    account.Name,
                    account.InstitutionName,
                    account.IsActive,
                    currentMemberGrant.AccessRole.ToString(),
                    currentMemberGrant.Visibility.ToString(),
                    DetermineSharingPreset(grants, memberScope.HouseholdUserId),
                    grants.Max(x => x.LastModifiedAtUtc)));
            }

            return Results.Ok(response);
        });

        group.MapPut("/households/{id:guid}/accounts/{accountId:guid}/sharing-preset", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            Guid id,
            Guid accountId,
            UpdateAccountSharingPresetRequest request,
            CancellationToken cancellationToken) =>
        {
            var errors = ValidateUpdateAccountSharingPresetRequest(request);
            if (errors.Count > 0)
            {
                return ApiValidation.ToValidationResult(httpContext, errors);
            }

            var memberScope = await ResolveActiveHouseholdMemberScopeAsync(httpContext, dbContext, id, cancellationToken);
            if (memberScope.ErrorResult is not null)
            {
                return memberScope.ErrorResult;
            }

            var account = await dbContext.Accounts
                .FirstOrDefaultAsync(x => x.Id == accountId && x.HouseholdId == id && x.IsActive, cancellationToken);

            if (account is null)
            {
                return ApiValidation.ToNotFoundResult(
                    httpContext,
                    "account_not_found",
                    "The requested account was not found.");
            }

            var activeMembers = await dbContext.HouseholdUsers
                .AsNoTracking()
                .Where(x => x.HouseholdId == id && x.MembershipStatus == HouseholdMembershipStatus.Active)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            if (activeMembers.Count == 0)
            {
                return ApiValidation.ToConflictResult(
                    httpContext,
                    "no_active_household_members",
                    "No active household members are available for sharing updates.");
            }

            var accessEntries = await dbContext.AccountMemberAccessEntries
                .Where(x => x.AccountId == accountId && activeMembers.Contains(x.HouseholdUserId))
                .ToListAsync(cancellationToken);

            var requesterEntry = accessEntries.FirstOrDefault(x => x.HouseholdUserId == memberScope.HouseholdUserId);
            var requesterCanManage = requesterEntry is { AccessRole: AccountAccessRole.Owner, Visibility: AccountAccessVisibility.Visible }
                || accessEntries.Count == 0;

            if (!requesterCanManage)
            {
                return ApiValidation.ToForbiddenResult(
                    httpContext,
                    "account_sharing_requires_owner",
                    "Only an owner with visible account access can change sharing settings.");
            }

            var normalizedPreset = NormalizeSharingPreset(request.Preset);
            var now = DateTime.UtcNow;
            var entryByMemberId = accessEntries.ToDictionary(x => x.HouseholdUserId);

            foreach (var householdMemberId in activeMembers)
            {
                if (!entryByMemberId.TryGetValue(householdMemberId, out var entry))
                {
                    entry = new AccountMemberAccess
                    {
                        AccountId = accountId,
                        HouseholdUserId = householdMemberId,
                        AccessRole = AccountAccessRole.None,
                        Visibility = AccountAccessVisibility.Hidden,
                        GrantedAtUtc = now,
                        LastModifiedAtUtc = now,
                    };

                    dbContext.AccountMemberAccessEntries.Add(entry);
                    accessEntries.Add(entry);
                    entryByMemberId[householdMemberId] = entry;
                }

                ApplySharingPreset(normalizedPreset, householdMemberId == memberScope.HouseholdUserId, entry, now);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            var currentMemberAccess = entryByMemberId[memberScope.HouseholdUserId];
            var projectionEntries = accessEntries
                .Select(x => new AccountAccessProjection(
                    x.AccountId,
                    x.HouseholdUserId,
                    x.AccessRole,
                    x.Visibility,
                    x.LastModifiedAtUtc))
                .ToList();

            return Results.Ok(new HouseholdAccountAccessSummaryDto(
                account.Id,
                account.HouseholdId,
                account.Name,
                account.InstitutionName,
                account.IsActive,
                currentMemberAccess.AccessRole.ToString(),
                currentMemberAccess.Visibility.ToString(),
                DetermineSharingPreset(projectionEntries, memberScope.HouseholdUserId),
                projectionEntries.Max(x => x.LastModifiedAtUtc)));
        });

        return group;
    }

    public static IReadOnlyList<ApiValidationError> ValidateCreateHouseholdInviteRequest(CreateHouseholdInviteRequest request)
    {
        var errors = ApiValidation.ValidateDataAnnotations(request).ToList();

        if (!string.IsNullOrWhiteSpace(request.Role) && !AllowedInviteRoles.Contains(request.Role.Trim()))
        {
            errors.Add(new ApiValidationError(
                nameof(CreateHouseholdInviteRequest.Role),
                "Role must be one of: Member, Admin, Owner."));
        }

        return errors;
    }

    public static IReadOnlyList<ApiValidationError> ValidateUpdateAccountSharingPresetRequest(UpdateAccountSharingPresetRequest request)
    {
        var errors = ApiValidation.ValidateDataAnnotations(request).ToList();

        if (!string.IsNullOrWhiteSpace(request.Preset) && !AllowedSharingPresets.Contains(request.Preset.Trim()))
        {
            errors.Add(new ApiValidationError(
                nameof(UpdateAccountSharingPresetRequest.Preset),
                "Preset must be one of: Mine, Joint, Shared."));
        }

        return errors;
    }

    private static string DetermineSharingPreset(IReadOnlyCollection<AccountAccessProjection> grants, Guid householdUserId)
    {
        var currentMemberGrant = grants.FirstOrDefault(x => x.HouseholdUserId == householdUserId);
        if (currentMemberGrant is null
            || currentMemberGrant.AccessRole == AccountAccessRole.None
            || currentMemberGrant.Visibility == AccountAccessVisibility.Hidden)
        {
            return "Hidden";
        }

        var visibleOtherMemberGrants = grants
            .Where(x =>
                x.HouseholdUserId != householdUserId
                && x.Visibility == AccountAccessVisibility.Visible
                && x.AccessRole != AccountAccessRole.None)
            .ToList();

        if (visibleOtherMemberGrants.Count == 0)
        {
            return "Mine";
        }

        if (visibleOtherMemberGrants.All(x => x.AccessRole == AccountAccessRole.Owner))
        {
            return "Joint";
        }

        if (visibleOtherMemberGrants.All(x => x.AccessRole == AccountAccessRole.ReadOnly))
        {
            return "Shared";
        }

        return "Joint";
    }

    private static string NormalizeSharingPreset(string preset)
    {
        if (preset.Equals("Mine", StringComparison.OrdinalIgnoreCase))
        {
            return "Mine";
        }

        if (preset.Equals("Shared", StringComparison.OrdinalIgnoreCase))
        {
            return "Shared";
        }

        return "Joint";
    }

    private static void ApplySharingPreset(string preset, bool isRequester, AccountMemberAccess entry, DateTime now)
    {
        var targetRole = AccountAccessRole.None;
        var targetVisibility = AccountAccessVisibility.Hidden;

        switch (preset)
        {
            case "Mine":
                if (isRequester)
                {
                    targetRole = AccountAccessRole.Owner;
                    targetVisibility = AccountAccessVisibility.Visible;
                }

                break;
            case "Shared":
                targetRole = isRequester ? AccountAccessRole.Owner : AccountAccessRole.ReadOnly;
                targetVisibility = AccountAccessVisibility.Visible;
                break;
            default:
                targetRole = AccountAccessRole.Owner;
                targetVisibility = AccountAccessVisibility.Visible;
                break;
        }

        if (entry.AccessRole != targetRole || entry.Visibility != targetVisibility)
        {
            entry.AccessRole = targetRole;
            entry.Visibility = targetVisibility;
            entry.LastModifiedAtUtc = now;
        }
    }

    private static async Task<HouseholdMemberScope> ResolveActiveHouseholdMemberScopeAsync(
        HttpContext httpContext,
        MosaicMoneyDbContext dbContext,
        Guid householdId,
        CancellationToken cancellationToken)
    {
        var principalValue = httpContext.User.FindFirstValue(MosaicHouseholdUserIdClaimType)
            ?? httpContext.User.FindFirstValue(HouseholdUserIdClaimType);

        if (string.IsNullOrWhiteSpace(principalValue)
            && httpContext.Request.Headers.TryGetValue(HouseholdUserIdHeaderName, out var headerValues))
        {
            principalValue = headerValues.FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(principalValue))
        {
            return new HouseholdMemberScope(
                Guid.Empty,
                ApiValidation.ToUnauthorizedResult(
                    httpContext,
                    "member_context_required",
                    "A household member context claim or X-Mosaic-Household-User-Id header is required."));
        }

        if (!Guid.TryParse(principalValue, out var householdUserId))
        {
            return new HouseholdMemberScope(
                Guid.Empty,
                ApiValidation.ToUnauthorizedResult(
                    httpContext,
                    "member_context_invalid",
                    "The household member context value must be a valid GUID."));
        }

        var activeMembershipExists = await dbContext.HouseholdUsers
            .AsNoTracking()
            .AnyAsync(
                x =>
                    x.Id == householdUserId
                    && x.HouseholdId == householdId
                    && x.MembershipStatus == HouseholdMembershipStatus.Active,
                cancellationToken);

        if (!activeMembershipExists)
        {
            return new HouseholdMemberScope(
                Guid.Empty,
                ApiValidation.ToForbiddenResult(
                    httpContext,
                    "membership_access_denied",
                    "The household member is not active and cannot access this household."));
        }

        return new HouseholdMemberScope(householdUserId, null);
    }

    private static string BuildDefaultDisplayName(string emailOrName)
    {
        var trimmed = emailOrName.Trim();
        if (trimmed.Length == 0)
        {
            return "Household Member";
        }

        var separatorIndex = trimmed.IndexOf('@');
        return separatorIndex > 0 ? trimmed[..separatorIndex] : trimmed;
    }

    private sealed record AccountAccessProjection(
        Guid AccountId,
        Guid HouseholdUserId,
        AccountAccessRole AccessRole,
        AccountAccessVisibility Visibility,
        DateTime LastModifiedAtUtc);

    private sealed record HouseholdMemberScope(Guid HouseholdUserId, IResult? ErrorResult);
}
