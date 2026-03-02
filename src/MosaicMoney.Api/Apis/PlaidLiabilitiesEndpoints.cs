using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Contracts.V1;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger.Plaid;

namespace MosaicMoney.Api.Apis;

public static class PlaidLiabilitiesEndpoints
{
    private static readonly HashSet<string> AllowedLiabilitiesWebhookCodes =
    [
        "DEFAULT_UPDATE",
    ];

    public static RouteGroupBuilder MapPlaidLiabilitiesEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/plaid/webhooks/liabilities", async (
            HttpContext httpContext,
            PlaidLiabilitiesIngestionService liabilitiesIngestionService,
            PlaidLiabilitiesWebhookRequest request,
            CancellationToken cancellationToken) =>
        {
            var errors = ValidatePlaidLiabilitiesWebhookRequest(request).ToList();
            if (errors.Count > 0)
            {
                return ApiValidation.ToValidationResult(httpContext, errors);
            }

            var result = await liabilitiesIngestionService.ProcessDefaultUpdateWebhookAsync(
                new ProcessPlaidLiabilitiesWebhookCommand(
                    request.WebhookType,
                    request.WebhookCode,
                    request.ItemId,
                    request.Environment,
                    request.ProviderRequestId),
                cancellationToken);

            if (result is null)
            {
                return ApiValidation.ToNotFoundResult(
                    httpContext,
                    "plaid_item_credential_not_found",
                    "No Plaid item credential exists for the supplied item and environment.");
            }

            return Results.Accepted(
                $"/api/v1/plaid/items/{result.ItemId}/liabilities",
                new PlaidLiabilitiesWebhookProcessedDto(
                    result.CredentialId,
                    result.ItemId,
                    result.Environment,
                    result.AccountsUpsertedCount,
                    result.SnapshotsInsertedCount,
                    result.ProcessedAtUtc,
                    result.LastProviderRequestId));
        });

        group.MapGet("/liabilities/accounts", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            Guid? householdId,
            bool includeInactive = false,
            int snapshotLimit = 5,
            int page = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default) =>
        {
            var accessScope = await AuthenticatedHouseholdScopeResolver.ResolveAsync(
                httpContext,
                dbContext,
                householdId,
                "The authenticated household member is not active and cannot access liability accounts for this household.",
                cancellationToken);
            if (accessScope.ErrorResult is not null)
            {
                return accessScope.ErrorResult;
            }

            var errors = ValidateLiabilityQuery(page, pageSize, snapshotLimit);
            if (errors.Count > 0)
            {
                return ApiValidation.ToValidationResult(httpContext, errors);
            }

            var query = dbContext.LiabilityAccounts
                .AsNoTracking()
                .Include(x => x.Snapshots)
                .AsQueryable();

            query = query.Where(x => x.HouseholdId == accessScope.HouseholdId);

            if (!includeInactive)
            {
                query = query.Where(x => x.IsActive);
            }

            var accounts = await query
                .OrderByDescending(x => x.LastSeenAtUtc)
                .ThenBy(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return Results.Ok(accounts.Select(x => ApiEndpointHelpers.MapLiabilityAccount(x, snapshotLimit)).ToList());
        });

        group.MapGet("/liabilities/accounts/{liabilityAccountId:guid}/snapshots", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            Guid liabilityAccountId,
            int page = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default) =>
        {
            var accessScope = await AuthenticatedHouseholdScopeResolver.ResolveAsync(
                httpContext,
                dbContext,
                householdId: null,
                "The authenticated household member is not active and cannot access liability snapshots.",
                cancellationToken);
            if (accessScope.ErrorResult is not null)
            {
                return accessScope.ErrorResult;
            }

            var errors = ValidateLiabilitySnapshotQuery(page, pageSize);
            if (errors.Count > 0)
            {
                return ApiValidation.ToValidationResult(httpContext, errors);
            }

            var accountExists = await dbContext.LiabilityAccounts
                .AsNoTracking()
                .AnyAsync(x => x.Id == liabilityAccountId && x.HouseholdId == accessScope.HouseholdId, cancellationToken);

            if (!accountExists)
            {
                return ApiValidation.ToNotFoundResult(
                    httpContext,
                    "liability_account_not_found",
                    "The requested liability account was not found.");
            }

            var snapshots = await dbContext.LiabilitySnapshots
                .AsNoTracking()
                .Where(x => x.LiabilityAccountId == liabilityAccountId)
                .OrderByDescending(x => x.CapturedAtUtc)
                .ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return Results.Ok(snapshots.Select(ApiEndpointHelpers.MapLiabilitySnapshot).ToList());
        });

        return group;
    }

    internal static IReadOnlyList<ApiValidationError> ValidatePlaidLiabilitiesWebhookRequest(PlaidLiabilitiesWebhookRequest request)
    {
        var errors = ApiValidation.ValidateDataAnnotations(request).ToList();

        if (string.IsNullOrWhiteSpace(request.WebhookType))
        {
            errors.Add(new ApiValidationError(nameof(request.WebhookType), "WebhookType is required."));
        }
        else if (!string.Equals(request.WebhookType.Trim(), "LIABILITIES", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new ApiValidationError(nameof(request.WebhookType), "WebhookType must be LIABILITIES for liabilities webhook processing."));
        }

        if (string.IsNullOrWhiteSpace(request.WebhookCode))
        {
            errors.Add(new ApiValidationError(nameof(request.WebhookCode), "WebhookCode is required."));
        }
        else if (!AllowedLiabilitiesWebhookCodes.Contains(request.WebhookCode.Trim().ToUpperInvariant()))
        {
            errors.Add(new ApiValidationError(nameof(request.WebhookCode), "WebhookCode must be DEFAULT_UPDATE for liabilities webhook processing."));
        }

        if (string.IsNullOrWhiteSpace(request.ItemId))
        {
            errors.Add(new ApiValidationError(nameof(request.ItemId), "ItemId is required."));
        }

        if (string.IsNullOrWhiteSpace(request.Environment))
        {
            errors.Add(new ApiValidationError(nameof(request.Environment), "Environment is required."));
        }

        return errors;
    }

    private static List<ApiValidationError> ValidateLiabilityQuery(int page, int pageSize, int snapshotLimit)
    {
        var errors = new List<ApiValidationError>();

        if (page < 1)
        {
            errors.Add(new ApiValidationError(nameof(page), "Page must be greater than or equal to 1."));
        }

        if (pageSize is < 1 or > 200)
        {
            errors.Add(new ApiValidationError(nameof(pageSize), "PageSize must be between 1 and 200."));
        }

        if (snapshotLimit is < 1 or > 25)
        {
            errors.Add(new ApiValidationError(nameof(snapshotLimit), "SnapshotLimit must be between 1 and 25."));
        }

        return errors;
    }

    private static List<ApiValidationError> ValidateLiabilitySnapshotQuery(int page, int pageSize)
    {
        var errors = new List<ApiValidationError>();

        if (page < 1)
        {
            errors.Add(new ApiValidationError(nameof(page), "Page must be greater than or equal to 1."));
        }

        if (pageSize is < 1 or > 200)
        {
            errors.Add(new ApiValidationError(nameof(pageSize), "PageSize must be between 1 and 200."));
        }

        return errors;
    }
}
