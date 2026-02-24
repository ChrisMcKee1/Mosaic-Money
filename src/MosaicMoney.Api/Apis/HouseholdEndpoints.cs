using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Contracts.V1;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;

namespace MosaicMoney.Api.Apis;

public static class HouseholdEndpoints
{
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

        return group;
    }
}
