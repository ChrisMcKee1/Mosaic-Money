using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Contracts.V1;
using MosaicMoney.Api.Data;

namespace MosaicMoney.Api.Apis;

public static class PlaidInvestmentsEndpoints
{
    public static RouteGroupBuilder MapPlaidInvestmentsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/investments/accounts", async (
            MosaicMoneyDbContext dbContext,
            Guid? householdId,
            string? environment) =>
        {
            var query = dbContext.InvestmentAccounts.AsNoTracking().AsQueryable();

            if (householdId.HasValue)
            {
                query = query.Where(x => x.HouseholdId == householdId.Value);
            }

            if (!string.IsNullOrWhiteSpace(environment))
            {
                query = query.Where(x => x.PlaidEnvironment == environment.Trim().ToLowerInvariant());
            }

            var accounts = await query
                .OrderBy(x => x.Name)
                .ToListAsync();

            return Results.Ok(accounts.Select(ApiEndpointHelpers.MapInvestmentAccount).ToList());
        });

        group.MapGet("/investments/accounts/{accountId:guid}/holdings", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            Guid accountId) =>
        {
            var accountExists = await dbContext.InvestmentAccounts.AnyAsync(x => x.Id == accountId);
            if (!accountExists)
            {
                return ApiValidation.ToNotFoundResult(httpContext, "account_not_found", "The requested investment account was not found.");
            }

            var holdings = await dbContext.InvestmentHoldingSnapshots
                .AsNoTracking()
                .Where(x => x.InvestmentAccountId == accountId)
                .OrderByDescending(x => x.CapturedAtUtc)
                .ToListAsync();

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