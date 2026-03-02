using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Contracts.V1;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;

namespace MosaicMoney.Api.Apis;

internal sealed record AuthenticatedHouseholdScope(Guid HouseholdUserId, Guid HouseholdId, IResult? ErrorResult);

internal static class AuthenticatedHouseholdScopeResolver
{
    internal static async Task<AuthenticatedHouseholdScope> ResolveAsync(
        HttpContext httpContext,
        MosaicMoneyDbContext dbContext,
        Guid? householdId,
        string membershipDeniedMessage,
        CancellationToken cancellationToken)
    {
        var memberScope = await HouseholdMemberContextResolver.ResolveAsync(
            httpContext,
            dbContext,
            householdId,
            membershipDeniedMessage,
            cancellationToken);

        if (memberScope.ErrorResult is not null)
        {
            return new AuthenticatedHouseholdScope(Guid.Empty, Guid.Empty, memberScope.ErrorResult);
        }

        var membership = await dbContext.HouseholdUsers
            .AsNoTracking()
            .Where(x =>
                x.Id == memberScope.HouseholdUserId
                && x.MembershipStatus == HouseholdMembershipStatus.Active)
            .Select(x => new
            {
                x.Id,
                x.HouseholdId,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (membership is null)
        {
            return new AuthenticatedHouseholdScope(
                Guid.Empty,
                Guid.Empty,
                ApiValidation.ToForbiddenResult(
                    httpContext,
                    "membership_access_denied",
                    membershipDeniedMessage));
        }

        return new AuthenticatedHouseholdScope(
            membership.Id,
            membership.HouseholdId,
            null);
    }
}
