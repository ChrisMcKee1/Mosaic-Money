using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Contracts.V1;
using MosaicMoney.Api.Data;

namespace MosaicMoney.Api.Apis;

public static class PlaidInvestmentsEndpoints
{
    public static RouteGroupBuilder MapPlaidInvestmentsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/investments/accounts", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            Guid? householdId,
            string? environment,
            CancellationToken cancellationToken) =>
        {
            var accessScope = await AuthenticatedHouseholdScopeResolver.ResolveAsync(
                httpContext,
                dbContext,
                householdId,
                "The authenticated household member is not active and cannot access investment accounts for this household.",
                cancellationToken);
            if (accessScope.ErrorResult is not null)
            {
                return accessScope.ErrorResult;
            }

            var query = dbContext.InvestmentAccounts.AsNoTracking().AsQueryable();
            query = query.Where(x => x.HouseholdId == accessScope.HouseholdId);

            if (!string.IsNullOrWhiteSpace(environment))
            {
                query = query.Where(x => x.PlaidEnvironment == environment.Trim().ToLowerInvariant());
            }

            var accounts = await query
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);

            return Results.Ok(accounts.Select(ApiEndpointHelpers.MapInvestmentAccount).ToList());
        });

        group.MapGet("/investments/accounts/{accountId:guid}/holdings", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            Guid accountId,
            CancellationToken cancellationToken) =>
        {
            var accessScope = await AuthenticatedHouseholdScopeResolver.ResolveAsync(
                httpContext,
                dbContext,
                householdId: null,
                "The authenticated household member is not active and cannot access investment holdings.",
                cancellationToken);
            if (accessScope.ErrorResult is not null)
            {
                return accessScope.ErrorResult;
            }

            var accountExists = await dbContext.InvestmentAccounts
                .AsNoTracking()
                .AnyAsync(x => x.Id == accountId && x.HouseholdId == accessScope.HouseholdId, cancellationToken);
            if (!accountExists)
            {
                return ApiValidation.ToNotFoundResult(httpContext, "account_not_found", "The requested investment account was not found.");
            }

            var holdings = await dbContext.InvestmentHoldingSnapshots
                .AsNoTracking()
                .Where(x => x.InvestmentAccountId == accountId)
                .OrderByDescending(x => x.CapturedAtUtc)
                .ToListAsync(cancellationToken);

            // Group by security ID to get the latest snapshot per holding
            var latestHoldings = holdings
                .GroupBy(x => x.PlaidSecurityId)
                .Select(g => g.First())
                .OrderBy(x => x.Name ?? x.TickerSymbol)
                .ToList();

            return Results.Ok(latestHoldings.Select(ApiEndpointHelpers.MapInvestmentHoldingSnapshot).ToList());
        });

        return group;
    }
}