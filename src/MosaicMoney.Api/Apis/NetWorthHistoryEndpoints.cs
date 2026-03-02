using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Contracts.V1;
using MosaicMoney.Api.Data;

namespace MosaicMoney.Api.Apis;

public static class NetWorthHistoryEndpoints
{
    public static RouteGroupBuilder MapNetWorthHistoryEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/net-worth/history", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            Guid householdId,
            int? months,
            CancellationToken cancellationToken) =>
        {
            var accessScope = await AuthenticatedHouseholdScopeResolver.ResolveAsync(
                httpContext,
                dbContext,
                householdId,
                "The authenticated household member is not active and cannot access net worth history for this household.",
                cancellationToken);
            if (accessScope.ErrorResult is not null)
            {
                return accessScope.ErrorResult;
            }

            var monthsToFetch = months ?? 12;
            var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-monthsToFetch));

            // Fetch depository accounts (checking/savings)
            // For MVP, we might not have historical snapshots of depository accounts, 
            // so we might just use current balance or calculate backwards from transactions.
            // Since we don't have depository snapshots, we'll just return the current balance for them.
            // For liabilities and investments, we have snapshots.

            var liabilitySnapshots = await dbContext.LiabilitySnapshots
                .AsNoTracking()
                .Include(x => x.LiabilityAccount)
                .Where(x => x.LiabilityAccount.HouseholdId == accessScope.HouseholdId && x.CapturedAtUtc >= startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
                .ToListAsync(cancellationToken);

            var investmentSnapshots = await dbContext.InvestmentHoldingSnapshots
                .AsNoTracking()
                .Include(x => x.InvestmentAccount)
                .Where(x => x.InvestmentAccount.HouseholdId == accessScope.HouseholdId && x.CapturedAtUtc >= startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
                .ToListAsync(cancellationToken);

            // Group by month
            var history = new List<NetWorthHistoryPointDto>();

            for (int i = 0; i <= monthsToFetch; i++)
            {
                var targetMonth = startDate.AddMonths(i);
                var targetMonthStart = new DateTime(targetMonth.Year, targetMonth.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var targetMonthEnd = targetMonthStart.AddMonths(1).AddTicks(-1);

                // Get latest snapshot for each liability account in this month
                var monthLiabilities = liabilitySnapshots
                    .Where(x => x.CapturedAtUtc <= targetMonthEnd)
                    .GroupBy(x => x.LiabilityAccountId)
                    .Select(g => g.OrderByDescending(x => x.CapturedAtUtc).First())
                    .Sum(x => x.CurrentBalance ?? 0m);

                // Get latest snapshot for each investment holding in this month
                var monthInvestments = investmentSnapshots
                    .Where(x => x.CapturedAtUtc <= targetMonthEnd)
                    .GroupBy(x => new { x.InvestmentAccountId, x.PlaidSecurityId })
                    .Select(g => g.OrderByDescending(x => x.CapturedAtUtc).First())
                    .Sum(x => x.InstitutionValue);

                // Depository balances (mocked as 0 for historical since we don't have snapshots yet)
                // In a real implementation, we'd need a DepositorySnapshot table or calculate from transactions.
                var monthDepository = 0m;

                history.Add(new NetWorthHistoryPointDto(
                    targetMonthStart,
                    monthDepository,
                    monthInvestments,
                    monthLiabilities,
                    monthDepository + monthInvestments - monthLiabilities
                ));
            }

            return Results.Ok(history);
        });

        return group;
    }
}