using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Contracts.V1;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;

namespace MosaicMoney.Api.Apis;

public static class RecurringEndpoints
{
    public static RouteGroupBuilder MapRecurringEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/recurring", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            Guid? householdId,
            bool? isActive,
            CancellationToken cancellationToken) =>
        {
            var accessScope = await AuthenticatedHouseholdScopeResolver.ResolveAsync(
                httpContext,
                dbContext,
                householdId,
                "The authenticated household member is not active and cannot access recurring items for this household.",
                cancellationToken);
            if (accessScope.ErrorResult is not null)
            {
                return accessScope.ErrorResult;
            }

            var query = dbContext.RecurringItems.AsNoTracking().AsQueryable();
            query = query.Where(x => x.HouseholdId == accessScope.HouseholdId);

            if (isActive.HasValue)
            {
                query = query.Where(x => x.IsActive == isActive.Value);
            }

            var items = await query
                .OrderBy(x => x.NextDueDate)
                .ThenBy(x => x.MerchantName)
                .ToListAsync(cancellationToken);

            return Results.Ok(items.Select(ApiEndpointHelpers.MapRecurringItem).ToList());
        });

        group.MapPost("/recurring", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            CreateRecurringItemRequest request,
            CancellationToken cancellationToken) =>
        {
            var accessScope = await AuthenticatedHouseholdScopeResolver.ResolveAsync(
                httpContext,
                dbContext,
                request.HouseholdId,
                "The authenticated household member is not active and cannot create recurring items for this household.",
                cancellationToken);
            if (accessScope.ErrorResult is not null)
            {
                return accessScope.ErrorResult;
            }

            var errors = ApiValidation.ValidateDataAnnotations(request).ToList();

            if (!ApiEndpointHelpers.TryParseEnum<RecurringFrequency>(request.Frequency, out var frequency))
            {
                errors.Add(new ApiValidationError(nameof(request.Frequency), "Frequency must be one of: Weekly, BiWeekly, Monthly, Quarterly, Annually."));
            }

            if (request.ExpectedAmount <= 0)
            {
                errors.Add(new ApiValidationError(nameof(request.ExpectedAmount), "ExpectedAmount must be greater than zero."));
            }

            if (request.NextDueDate == default)
            {
                errors.Add(new ApiValidationError(nameof(request.NextDueDate), "NextDueDate is required."));
            }

            if (!ApiEndpointHelpers.AreScoreWeightsValid(request.DueDateScoreWeight, request.AmountScoreWeight, request.RecencyScoreWeight))
            {
                errors.Add(new ApiValidationError(nameof(request.DueDateScoreWeight), "DueDateScoreWeight, AmountScoreWeight, and RecencyScoreWeight must sum to 1.0000."));
            }

            if (string.IsNullOrWhiteSpace(request.DeterministicScoreVersion))
            {
                errors.Add(new ApiValidationError(nameof(request.DeterministicScoreVersion), "DeterministicScoreVersion is required."));
            }

            if (string.IsNullOrWhiteSpace(request.TieBreakPolicy))
            {
                errors.Add(new ApiValidationError(nameof(request.TieBreakPolicy), "TieBreakPolicy is required."));
            }

            if (errors.Count > 0)
            {
                return ApiValidation.ToValidationResult(httpContext, errors);
            }

            var recurringItem = new RecurringItem
            {
                Id = Guid.NewGuid(),
                HouseholdId = accessScope.HouseholdId,
                MerchantName = request.MerchantName.Trim(),
                ExpectedAmount = decimal.Round(request.ExpectedAmount, 2),
                IsVariable = request.IsVariable,
                Frequency = frequency,
                NextDueDate = request.NextDueDate,
                DueWindowDaysBefore = request.DueWindowDaysBefore,
                DueWindowDaysAfter = request.DueWindowDaysAfter,
                AmountVariancePercent = decimal.Round(request.AmountVariancePercent, 2),
                AmountVarianceAbsolute = decimal.Round(request.AmountVarianceAbsolute, 2),
                DeterministicMatchThreshold = decimal.Round(request.DeterministicMatchThreshold, 4),
                DueDateScoreWeight = decimal.Round(request.DueDateScoreWeight, 4),
                AmountScoreWeight = decimal.Round(request.AmountScoreWeight, 4),
                RecencyScoreWeight = decimal.Round(request.RecencyScoreWeight, 4),
                DeterministicScoreVersion = request.DeterministicScoreVersion.Trim(),
                TieBreakPolicy = request.TieBreakPolicy.Trim(),
                IsActive = request.IsActive,
                UserNote = request.UserNote,
                AgentNote = request.AgentNote,
            };

            dbContext.RecurringItems.Add(recurringItem);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/v1/recurring/{recurringItem.Id}", ApiEndpointHelpers.MapRecurringItem(recurringItem));
        });

        group.MapPatch("/recurring/{id:guid}", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            Guid id,
            UpdateRecurringItemRequest request,
            CancellationToken cancellationToken) =>
        {
            var accessScope = await AuthenticatedHouseholdScopeResolver.ResolveAsync(
                httpContext,
                dbContext,
                householdId: null,
                "The authenticated household member is not active and cannot update recurring items.",
                cancellationToken);
            if (accessScope.ErrorResult is not null)
            {
                return accessScope.ErrorResult;
            }

            var errors = ApiValidation.ValidateDataAnnotations(request).ToList();

            RecurringFrequency? frequency = null;
            if (!string.IsNullOrWhiteSpace(request.Frequency))
            {
                if (!ApiEndpointHelpers.TryParseEnum<RecurringFrequency>(request.Frequency, out var parsedFrequency))
                {
                    errors.Add(new ApiValidationError(nameof(request.Frequency), "Frequency must be one of: Weekly, BiWeekly, Monthly, Quarterly, Annually."));
                }
                else
                {
                    frequency = parsedFrequency;
                }
            }

            if (request.ExpectedAmount.HasValue && request.ExpectedAmount <= 0)
            {
                errors.Add(new ApiValidationError(nameof(request.ExpectedAmount), "ExpectedAmount must be greater than zero when provided."));
            }

            var dueDateWeight = request.DueDateScoreWeight;
            var amountWeight = request.AmountScoreWeight;
            var recencyWeight = request.RecencyScoreWeight;
            if (dueDateWeight.HasValue || amountWeight.HasValue || recencyWeight.HasValue)
            {
                var resolvedDueDateWeight = dueDateWeight ?? 0m;
                var resolvedAmountWeight = amountWeight ?? 0m;
                var resolvedRecencyWeight = recencyWeight ?? 0m;

                if (!(dueDateWeight.HasValue && amountWeight.HasValue && recencyWeight.HasValue))
                {
                    errors.Add(new ApiValidationError(nameof(request.DueDateScoreWeight), "DueDateScoreWeight, AmountScoreWeight, and RecencyScoreWeight must be patched together."));
                }
                else if (!ApiEndpointHelpers.AreScoreWeightsValid(resolvedDueDateWeight, resolvedAmountWeight, resolvedRecencyWeight))
                {
                    errors.Add(new ApiValidationError(nameof(request.DueDateScoreWeight), "DueDateScoreWeight, AmountScoreWeight, and RecencyScoreWeight must sum to 1.0000."));
                }
            }

            if (request.DeterministicScoreVersion is not null && string.IsNullOrWhiteSpace(request.DeterministicScoreVersion))
            {
                errors.Add(new ApiValidationError(nameof(request.DeterministicScoreVersion), "DeterministicScoreVersion cannot be empty when provided."));
            }

            if (request.TieBreakPolicy is not null && string.IsNullOrWhiteSpace(request.TieBreakPolicy))
            {
                errors.Add(new ApiValidationError(nameof(request.TieBreakPolicy), "TieBreakPolicy cannot be empty when provided."));
            }

            if (errors.Count > 0)
            {
                return ApiValidation.ToValidationResult(httpContext, errors);
            }

            var recurringItem = await dbContext.RecurringItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (recurringItem is null)
            {
                return ApiValidation.ToNotFoundResult(httpContext, "recurring_not_found", "The requested recurring item was not found.");
            }

            if (recurringItem.HouseholdId != accessScope.HouseholdId)
            {
                return ApiValidation.ToNotFoundResult(httpContext, "recurring_not_found", "The requested recurring item was not found.");
            }

            if (!string.IsNullOrWhiteSpace(request.MerchantName))
            {
                recurringItem.MerchantName = request.MerchantName.Trim();
            }

            if (request.ExpectedAmount.HasValue)
            {
                recurringItem.ExpectedAmount = decimal.Round(request.ExpectedAmount.Value, 2);
            }

            if (request.IsVariable.HasValue)
            {
                recurringItem.IsVariable = request.IsVariable.Value;
            }

            if (frequency.HasValue)
            {
                recurringItem.Frequency = frequency.Value;
            }

            if (request.NextDueDate.HasValue)
            {
                recurringItem.NextDueDate = request.NextDueDate.Value;
            }

            if (request.DueWindowDaysBefore.HasValue)
            {
                recurringItem.DueWindowDaysBefore = request.DueWindowDaysBefore.Value;
            }

            if (request.DueWindowDaysAfter.HasValue)
            {
                recurringItem.DueWindowDaysAfter = request.DueWindowDaysAfter.Value;
            }

            if (request.AmountVariancePercent.HasValue)
            {
                recurringItem.AmountVariancePercent = decimal.Round(request.AmountVariancePercent.Value, 2);
            }

            if (request.AmountVarianceAbsolute.HasValue)
            {
                recurringItem.AmountVarianceAbsolute = decimal.Round(request.AmountVarianceAbsolute.Value, 2);
            }

            if (request.DeterministicMatchThreshold.HasValue)
            {
                recurringItem.DeterministicMatchThreshold = decimal.Round(request.DeterministicMatchThreshold.Value, 4);
            }

            if (request.DueDateScoreWeight.HasValue)
            {
                recurringItem.DueDateScoreWeight = decimal.Round(request.DueDateScoreWeight.Value, 4);
            }

            if (request.AmountScoreWeight.HasValue)
            {
                recurringItem.AmountScoreWeight = decimal.Round(request.AmountScoreWeight.Value, 4);
            }

            if (request.RecencyScoreWeight.HasValue)
            {
                recurringItem.RecencyScoreWeight = decimal.Round(request.RecencyScoreWeight.Value, 4);
            }

            if (!string.IsNullOrWhiteSpace(request.DeterministicScoreVersion))
            {
                recurringItem.DeterministicScoreVersion = request.DeterministicScoreVersion.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.TieBreakPolicy))
            {
                recurringItem.TieBreakPolicy = request.TieBreakPolicy.Trim();
            }

            if (request.IsActive.HasValue)
            {
                recurringItem.IsActive = request.IsActive.Value;
            }

            recurringItem.UserNote = request.UserNote ?? recurringItem.UserNote;
            recurringItem.AgentNote = request.AgentNote ?? recurringItem.AgentNote;

            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(ApiEndpointHelpers.MapRecurringItem(recurringItem));
        });

        return group;
    }
}
