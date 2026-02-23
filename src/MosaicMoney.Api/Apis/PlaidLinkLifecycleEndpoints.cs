using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Contracts.V1;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.Plaid;

namespace MosaicMoney.Api.Apis;

public static class PlaidLinkLifecycleEndpoints
{
    private static readonly HashSet<string> AllowedSessionEvents =
    [
        "OPEN",
        "EXIT",
        "SUCCESS",
        "HANDOFF",
        "ERROR",
    ];

    private static readonly HashSet<string> AllowedItemRecoveryWebhookCodes =
    [
        "ERROR",
        "PENDING_EXPIRATION",
        "USER_PERMISSION_REVOKED",
    ];

    public static RouteGroupBuilder MapPlaidLinkLifecycleEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/plaid/link-tokens", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            PlaidLinkLifecycleService lifecycleService,
            CreatePlaidLinkTokenRequest request,
            CancellationToken cancellationToken) =>
        {
            var errors = ValidateCreateLinkTokenRequest(request).ToList();

            if (request.HouseholdId.HasValue)
            {
                var householdExists = await dbContext.Households
                    .AsNoTracking()
                    .AnyAsync(x => x.Id == request.HouseholdId.Value, cancellationToken);

                if (!householdExists)
                {
                    errors.Add(new ApiValidationError(nameof(request.HouseholdId), "HouseholdId does not exist."));
                }
            }

            if (errors.Count > 0)
            {
                return ApiValidation.ToValidationResult(httpContext, errors);
            }

            try
            {
                var result = await lifecycleService.IssueLinkTokenAsync(
                    new IssuePlaidLinkTokenCommand(
                        request.HouseholdId,
                        request.ClientUserId,
                        request.RedirectUri,
                        request.Products,
                        request.ClientMetadataJson),
                    cancellationToken);

                return Results.Created(
                    $"/api/v1/plaid/link-sessions/{result.LinkSessionId}",
                    new PlaidLinkTokenIssuedDto(
                        result.LinkSessionId,
                        result.LinkToken,
                        result.ExpiresAtUtc,
                        result.Environment,
                        result.Products,
                        result.OAuthEnabled,
                        result.RedirectUri));
            }
            catch (InvalidOperationException)
            {
                return ApiValidation.ToServiceUnavailableResult(
                    httpContext,
                    "plaid_configuration_missing",
                    "Plaid integration is not configured for link token issuance.");
            }
        });

        group.MapPost("/plaid/link-sessions/{sessionId:guid}/events", async (
            HttpContext httpContext,
            PlaidLinkLifecycleService lifecycleService,
            Guid sessionId,
            LogPlaidLinkSessionEventRequest request,
            CancellationToken cancellationToken) =>
        {
            var errors = ValidateLogLinkSessionEventRequest(request).ToList();

            if (errors.Count > 0)
            {
                return ApiValidation.ToValidationResult(httpContext, errors);
            }

            var result = await lifecycleService.LogLinkSessionEventAsync(
                new LogPlaidLinkSessionEventCommand(
                    sessionId,
                    request.EventType,
                    request.Source,
                    request.ClientMetadataJson),
                cancellationToken);

            if (result is null)
            {
                return ApiValidation.ToNotFoundResult(
                    httpContext,
                    "plaid_link_session_not_found",
                    "The requested Plaid link session was not found.");
            }

            return Results.Accepted(
                $"/api/v1/plaid/link-sessions/{result.LinkSessionId}",
                new PlaidLinkSessionEventLoggedDto(result.LinkSessionId, result.EventType, result.LoggedAtUtc));
        });

        group.MapPost("/plaid/public-token-exchange", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            PlaidLinkLifecycleService lifecycleService,
            ExchangePlaidPublicTokenRequest request,
            CancellationToken cancellationToken) =>
        {
            var errors = ValidateExchangePublicTokenRequest(request).ToList();

            if (request.HouseholdId.HasValue)
            {
                var householdExists = await dbContext.Households
                    .AsNoTracking()
                    .AnyAsync(x => x.Id == request.HouseholdId.Value, cancellationToken);

                if (!householdExists)
                {
                    errors.Add(new ApiValidationError(nameof(request.HouseholdId), "HouseholdId does not exist."));
                }
            }

            PlaidLinkSession? linkSession = null;
            if (request.LinkSessionId.HasValue)
            {
                linkSession = await dbContext.PlaidLinkSessions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == request.LinkSessionId.Value, cancellationToken);

                if (linkSession is null)
                {
                    return ApiValidation.ToNotFoundResult(
                        httpContext,
                        "plaid_link_session_not_found",
                        "The requested Plaid link session was not found.");
                }

                if (request.HouseholdId.HasValue
                    && linkSession.HouseholdId is Guid linkHouseholdId
                    && linkHouseholdId != request.HouseholdId.Value)
                {
                    errors.Add(new ApiValidationError(nameof(request.HouseholdId), "HouseholdId does not match the link session household."));
                }
            }

            if (errors.Count > 0)
            {
                return ApiValidation.ToValidationResult(httpContext, errors);
            }

            try
            {
                var result = await lifecycleService.ExchangePublicTokenAsync(
                    new ExchangePlaidPublicTokenCommand(
                        request.HouseholdId,
                        request.LinkSessionId,
                        request.PublicToken,
                        request.InstitutionId,
                        request.ClientMetadataJson),
                    cancellationToken);

                return Results.Ok(new PlaidPublicTokenExchangeResultDto(
                    result.CredentialId,
                    result.LinkSessionId,
                    result.ItemId,
                    result.Environment,
                    result.Status.ToString(),
                    result.InstitutionId,
                    result.StoredAtUtc));
            }
            catch (InvalidOperationException)
            {
                return ApiValidation.ToServiceUnavailableResult(
                    httpContext,
                    "plaid_configuration_missing",
                    "Plaid integration is not configured for public token exchange.");
            }
        });

        group.MapPost("/plaid/webhooks/item-recovery", async (
            HttpContext httpContext,
            PlaidLinkLifecycleService lifecycleService,
            PlaidItemRecoveryWebhookRequest request,
            CancellationToken cancellationToken) =>
        {
            var errors = ValidatePlaidItemRecoveryWebhookRequest(request).ToList();
            if (errors.Count > 0)
            {
                return ApiValidation.ToValidationResult(httpContext, errors);
            }

            var result = await lifecycleService.ProcessItemRecoveryWebhookAsync(
                new ProcessPlaidItemRecoveryWebhookCommand(
                    request.WebhookType,
                    request.WebhookCode,
                    request.ItemId,
                    request.Environment,
                    request.ProviderRequestId,
                    request.ErrorCode,
                    request.ErrorType,
                    request.MetadataJson),
                cancellationToken);

            if (result is null)
            {
                return ApiValidation.ToNotFoundResult(
                    httpContext,
                    "plaid_item_credential_not_found",
                    "No Plaid item credential exists for the supplied item and environment.");
            }

            return Results.Accepted(
                $"/api/v1/plaid/items/{result.ItemId}/recovery",
                new PlaidItemRecoveryWebhookProcessedDto(
                    result.CredentialId,
                    result.LinkSessionId,
                    result.ItemId,
                    result.Environment,
                    result.CredentialStatus.ToString(),
                    result.SessionStatus?.ToString(),
                    result.RecoveryAction,
                    result.RecoveryReasonCode,
                    result.ProcessedAtUtc));
        });

        return group;
    }

    private static void AddJsonValidationError(ICollection<ApiValidationError> errors, string fieldName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        try
        {
            using var _ = JsonDocument.Parse(value);
        }
        catch (JsonException)
        {
            errors.Add(new ApiValidationError(fieldName, "Value must be valid JSON when provided."));
        }
    }

    internal static IReadOnlyList<ApiValidationError> ValidateCreateLinkTokenRequest(CreatePlaidLinkTokenRequest request)
    {
        var errors = ApiValidation.ValidateDataAnnotations(request).ToList();

        if (string.IsNullOrWhiteSpace(request.ClientUserId))
        {
            errors.Add(new ApiValidationError(nameof(request.ClientUserId), "ClientUserId is required."));
        }

        if (!string.IsNullOrWhiteSpace(request.RedirectUri)
            && (!Uri.TryCreate(request.RedirectUri, UriKind.Absolute, out var redirectUri)
                || !string.Equals(redirectUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add(new ApiValidationError(nameof(request.RedirectUri), "RedirectUri must be a valid absolute HTTPS URI when provided."));
        }

        if (request.Products is { Count: > 0 })
        {
            for (var i = 0; i < request.Products.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(request.Products[i]))
                {
                    errors.Add(new ApiValidationError($"Products[{i}]", "Product values cannot be empty."));
                }
            }
        }

        AddJsonValidationError(errors, nameof(request.ClientMetadataJson), request.ClientMetadataJson);
        return errors;
    }

    internal static IReadOnlyList<ApiValidationError> ValidateLogLinkSessionEventRequest(LogPlaidLinkSessionEventRequest request)
    {
        var errors = ApiValidation.ValidateDataAnnotations(request).ToList();

        if (string.IsNullOrWhiteSpace(request.EventType))
        {
            errors.Add(new ApiValidationError(nameof(request.EventType), "EventType is required."));
        }
        else if (!AllowedSessionEvents.Contains(request.EventType.Trim().ToUpperInvariant()))
        {
            errors.Add(new ApiValidationError(nameof(request.EventType), "EventType must be one of: OPEN, EXIT, SUCCESS, HANDOFF, ERROR."));
        }

        AddJsonValidationError(errors, nameof(request.ClientMetadataJson), request.ClientMetadataJson);
        return errors;
    }

    internal static IReadOnlyList<ApiValidationError> ValidateExchangePublicTokenRequest(ExchangePlaidPublicTokenRequest request)
    {
        var errors = ApiValidation.ValidateDataAnnotations(request).ToList();

        if (string.IsNullOrWhiteSpace(request.PublicToken))
        {
            errors.Add(new ApiValidationError(nameof(request.PublicToken), "PublicToken is required."));
        }

        if (!request.LinkSessionId.HasValue && !request.HouseholdId.HasValue)
        {
            errors.Add(new ApiValidationError(nameof(request.LinkSessionId), "Either LinkSessionId or HouseholdId is required."));
        }

        AddJsonValidationError(errors, nameof(request.ClientMetadataJson), request.ClientMetadataJson);
        return errors;
    }

    internal static IReadOnlyList<ApiValidationError> ValidatePlaidItemRecoveryWebhookRequest(PlaidItemRecoveryWebhookRequest request)
    {
        var errors = ApiValidation.ValidateDataAnnotations(request).ToList();

        if (string.IsNullOrWhiteSpace(request.WebhookType))
        {
            errors.Add(new ApiValidationError(nameof(request.WebhookType), "WebhookType is required."));
        }
        else if (!string.Equals(request.WebhookType.Trim(), "ITEM", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new ApiValidationError(nameof(request.WebhookType), "WebhookType must be ITEM for item recovery processing."));
        }

        if (string.IsNullOrWhiteSpace(request.WebhookCode))
        {
            errors.Add(new ApiValidationError(nameof(request.WebhookCode), "WebhookCode is required."));
        }
        else if (!AllowedItemRecoveryWebhookCodes.Contains(request.WebhookCode.Trim().ToUpperInvariant()))
        {
            errors.Add(new ApiValidationError(nameof(request.WebhookCode), "WebhookCode must be one of: ERROR, PENDING_EXPIRATION, USER_PERMISSION_REVOKED."));
        }

        if (string.IsNullOrWhiteSpace(request.ItemId))
        {
            errors.Add(new ApiValidationError(nameof(request.ItemId), "ItemId is required."));
        }

        if (string.IsNullOrWhiteSpace(request.Environment))
        {
            errors.Add(new ApiValidationError(nameof(request.Environment), "Environment is required."));
        }

        AddJsonValidationError(errors, nameof(request.MetadataJson), request.MetadataJson);
        return errors;
    }
}
