using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MosaicMoney.Api.Contracts.V1;
using MosaicMoney.Api.Authentication;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;

namespace MosaicMoney.Api.Apis;

internal static class HouseholdMemberContextResolver
{
    private const string HouseholdUserIdHeaderName = "X-Mosaic-Household-User-Id";
    private const string MosaicHouseholdUserIdClaimType = "mosaic_household_user_id";
    private const string HouseholdUserIdClaimType = "household_user_id";
    private const string AuthSubjectClaimType = "sub";
    internal static async Task<HouseholdMemberContextScope> ResolveAsync(
        HttpContext httpContext,
        MosaicMoneyDbContext dbContext,
        Guid? householdId,
        string membershipDeniedMessage,
        CancellationToken cancellationToken)
    {
        var authProvider = ResolveConfiguredAuthProvider(httpContext);
        var principalValue = httpContext.User.FindFirstValue(MosaicHouseholdUserIdClaimType)
            ?? httpContext.User.FindFirstValue(HouseholdUserIdClaimType);

        if (string.IsNullOrWhiteSpace(principalValue)
            && httpContext.Request.Headers.TryGetValue(HouseholdUserIdHeaderName, out var headerValues))
        {
            principalValue = headerValues.FirstOrDefault();
        }

        var authSubject = httpContext.User.FindFirstValue(AuthSubjectClaimType)
            ?? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(authSubject))
        {
            authSubject = TryResolveAuthSubjectFromBearerHeader(httpContext);
        }

        if (!string.IsNullOrWhiteSpace(principalValue))
        {
            return await ResolveByExplicitMemberContextAsync(
                httpContext,
                dbContext,
                principalValue,
                authSubject,
                authProvider,
                householdId,
                membershipDeniedMessage,
                cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(authSubject))
        {
            return await ResolveByAuthSubjectAsync(
                httpContext,
                dbContext,
                authSubject,
                authProvider,
                householdId,
                membershipDeniedMessage,
                cancellationToken);
        }

        return new HouseholdMemberContextScope(
            Guid.Empty,
            ApiValidation.ToUnauthorizedResult(
                httpContext,
                "member_context_required",
                "A household member context claim, X-Mosaic-Household-User-Id header, or authenticated subject mapping is required."));
    }

    private static string ResolveConfiguredAuthProvider(HttpContext httpContext)
    {
        var options = httpContext.RequestServices
            .GetService<IOptions<ClerkAuthenticationOptions>>()
            ?.Value;

        return string.IsNullOrWhiteSpace(options?.AuthProvider)
            ? "clerk"
            : options.AuthProvider.Trim();
    }

    private static string? TryResolveAuthSubjectFromBearerHeader(HttpContext httpContext)
    {
        if (!httpContext.Request.Headers.TryGetValue("Authorization", out var authorizationHeaderValues))
        {
            return null;
        }

        var bearerValue = authorizationHeaderValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(bearerValue)
            || !bearerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var rawToken = bearerValue["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(rawToken);
            return jwt.Claims.FirstOrDefault(x => x.Type == AuthSubjectClaimType)?.Value;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<HouseholdMemberContextScope> ResolveByExplicitMemberContextAsync(
        HttpContext httpContext,
        MosaicMoneyDbContext dbContext,
        string principalValue,
        string? authSubject,
        string authProvider,
        Guid? householdId,
        string membershipDeniedMessage,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principalValue, out var householdUserId))
        {
            return new HouseholdMemberContextScope(
                Guid.Empty,
                ApiValidation.ToUnauthorizedResult(
                    httpContext,
                    "member_context_invalid",
                    "The household member context value must be a valid GUID."));
        }

        var membership = await dbContext.HouseholdUsers
            .AsNoTracking()
            .Where(x =>
                x.Id == householdUserId
                && x.MembershipStatus == HouseholdMembershipStatus.Active
                && (!householdId.HasValue || x.HouseholdId == householdId.Value))
            .Select(x => new
            {
                x.Id,
                x.MosaicUserId,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (membership is null)
        {
            return new HouseholdMemberContextScope(
                Guid.Empty,
                ApiValidation.ToForbiddenResult(
                    httpContext,
                    "membership_access_denied",
                    membershipDeniedMessage));
        }

        if (!string.IsNullOrWhiteSpace(authSubject))
        {
            if (membership.MosaicUserId is null)
            {
                return new HouseholdMemberContextScope(
                    Guid.Empty,
                    ApiValidation.ToForbiddenResult(
                        httpContext,
                        "member_context_subject_mismatch",
                        "The authenticated subject does not map to the provided household member context."));
            }

            var subjectMatchesMember = await dbContext.MosaicUsers
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.Id == membership.MosaicUserId
                        && x.IsActive
                        && x.AuthProvider == authProvider
                        && x.AuthSubject == authSubject,
                    cancellationToken);

            if (!subjectMatchesMember)
            {
                return new HouseholdMemberContextScope(
                    Guid.Empty,
                    ApiValidation.ToForbiddenResult(
                        httpContext,
                        "member_context_subject_mismatch",
                        "The authenticated subject does not map to the provided household member context."));
            }
        }

        return new HouseholdMemberContextScope(householdUserId, null);
    }

    private static async Task<HouseholdMemberContextScope> ResolveByAuthSubjectAsync(
        HttpContext httpContext,
        MosaicMoneyDbContext dbContext,
        string authSubject,
        string authProvider,
        Guid? householdId,
        string membershipDeniedMessage,
        CancellationToken cancellationToken)
    {
        var activeMappedUserIds = await dbContext.MosaicUsers
            .AsNoTracking()
            .Where(x =>
                x.IsActive
                && x.AuthProvider == authProvider
                && x.AuthSubject == authSubject)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (activeMappedUserIds.Count == 0)
        {
            return new HouseholdMemberContextScope(
                Guid.Empty,
                ApiValidation.ToForbiddenResult(
                    httpContext,
                    "membership_access_denied",
                    membershipDeniedMessage));
        }

        var activeMemberships = await dbContext.HouseholdUsers
            .AsNoTracking()
            .Where(x =>
                x.MembershipStatus == HouseholdMembershipStatus.Active
                && x.MosaicUserId.HasValue
                && activeMappedUserIds.Contains(x.MosaicUserId.Value)
                && (!householdId.HasValue || x.HouseholdId == householdId.Value))
            .Select(x => x.Id)
            .Take(2)
            .ToListAsync(cancellationToken);

        if (activeMemberships.Count == 1)
        {
            return new HouseholdMemberContextScope(activeMemberships[0], null);
        }

        if (activeMemberships.Count > 1)
        {
            return new HouseholdMemberContextScope(
                Guid.Empty,
                ApiValidation.ToUnauthorizedResult(
                    httpContext,
                    "member_context_ambiguous",
                    "Multiple active household memberships were found for this identity. Provide an explicit household member context claim or X-Mosaic-Household-User-Id header."));
        }

        return new HouseholdMemberContextScope(
            Guid.Empty,
            ApiValidation.ToForbiddenResult(
                httpContext,
                "membership_access_denied",
                membershipDeniedMessage));
    }
}

internal sealed record HouseholdMemberContextScope(Guid HouseholdUserId, IResult? ErrorResult);