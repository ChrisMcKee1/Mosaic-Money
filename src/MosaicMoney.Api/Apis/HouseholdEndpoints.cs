using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Contracts.V1;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;

namespace MosaicMoney.Api.Apis;

public static class HouseholdEndpoints
{
    private static readonly HashSet<string> AllowedInviteRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Member",
        "Admin",
        "Owner",
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
}
