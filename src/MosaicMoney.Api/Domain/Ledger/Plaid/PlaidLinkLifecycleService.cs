using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MosaicMoney.Api.Data;

namespace MosaicMoney.Api.Domain.Ledger.Plaid;

public sealed record IssuePlaidLinkTokenCommand(
    Guid? HouseholdId,
    string ClientUserId,
    string? RedirectUri,
    IReadOnlyList<string>? Products,
    string? ClientMetadataJson);

public sealed record IssuePlaidLinkTokenResult(
    Guid LinkSessionId,
    string LinkToken,
    DateTime ExpiresAtUtc,
    string Environment,
    IReadOnlyList<string> Products,
    bool OAuthEnabled,
    string? RedirectUri,
    string RequestId);

public sealed record LogPlaidLinkSessionEventCommand(
    Guid LinkSessionId,
    string EventType,
    string? Source,
    string? ClientMetadataJson);

public sealed record LogPlaidLinkSessionEventResult(
    Guid LinkSessionId,
    string EventType,
    DateTime LoggedAtUtc);

public sealed record ExchangePlaidPublicTokenCommand(
    Guid? HouseholdId,
    Guid? LinkSessionId,
    string PublicToken,
    string? InstitutionId,
    string? ClientMetadataJson);

public sealed record ExchangePlaidPublicTokenResult(
    Guid CredentialId,
    Guid? LinkSessionId,
    string ItemId,
    string Environment,
    PlaidItemCredentialStatus Status,
    string? InstitutionId,
    DateTime StoredAtUtc);

public sealed record ProcessPlaidItemRecoveryWebhookCommand(
    string WebhookType,
    string WebhookCode,
    string ItemId,
    string Environment,
    string? ProviderRequestId,
    string? ErrorCode,
    string? ErrorType,
    string? MetadataJson);

public sealed record ProcessPlaidItemRecoveryWebhookResult(
    Guid CredentialId,
    Guid? LinkSessionId,
    string ItemId,
    string Environment,
    PlaidItemCredentialStatus CredentialStatus,
    PlaidLinkSessionStatus? SessionStatus,
    string RecoveryAction,
    string RecoveryReasonCode,
    DateTime ProcessedAtUtc);

public sealed class PlaidLinkLifecycleService(
    MosaicMoneyDbContext dbContext,
    IPlaidTokenProvider tokenProvider,
    PlaidAccessTokenProtector tokenProtector,
    IOptions<PlaidOptions> options)
{
    private const string DefaultSyncBootstrapCursor = "now";
    private const string HistoricalSyncBootstrapCursorMode = "start";

    private static readonly HashSet<string> RequiresRelinkErrorCodes =
    [
        "USER_PERMISSION_REVOKED",
        "ACCESS_NOT_GRANTED",
        "ITEM_REVOKED",
    ];

    private static readonly HashSet<string> RequiresUpdateModeErrorCodes =
    [
        "ITEM_LOGIN_REQUIRED",
        "INVALID_CREDENTIALS",
        "INVALID_MFA",
        "ITEM_LOCKED",
        "OAUTH_LOGIN_REQUIRED",
        "OAUTH_INVALID_TOKEN",
        "OAUTH_STATE_ID_MISMATCH",
        "OAUTH_STATE_ID_INVALID",
    ];

    public async Task<IssuePlaidLinkTokenResult> IssueLinkTokenAsync(
        IssuePlaidLinkTokenCommand command,
        CancellationToken cancellationToken = default)
    {
        var normalizedEnvironment = ResolveEnvironment();
        var normalizedProducts = ResolveProducts(command.Products);
        var redirectUri = ResolveRedirectUri(command.RedirectUri);
        var stateId = Guid.NewGuid().ToString("N");

        var providerResult = await tokenProvider.CreateLinkTokenAsync(
            new PlaidLinkTokenCreateRequest(
                command.ClientUserId.Trim(),
                normalizedEnvironment,
                redirectUri,
                normalizedProducts,
                ResolveCountryCodes(),
                OAuthEnabled: true,
                stateId,
                command.ClientMetadataJson),
            cancellationToken);

        var now = DateTime.UtcNow;
        var linkSession = new PlaidLinkSession
        {
            Id = Guid.NewGuid(),
            HouseholdId = command.HouseholdId,
            ClientUserId = command.ClientUserId.Trim(),
            LinkTokenHash = PlaidAccessTokenProtector.ComputeFingerprint(providerResult.LinkToken),
            OAuthStateId = stateId,
            RedirectUri = providerResult.RedirectUri,
            RequestedProducts = string.Join(',', providerResult.Products),
            RequestedEnvironment = providerResult.Environment,
            Status = PlaidLinkSessionStatus.Issued,
            LinkTokenCreatedAtUtc = now,
            LinkTokenExpiresAtUtc = providerResult.ExpiresAtUtc,
            LastProviderRequestId = providerResult.RequestId,
            LastClientMetadataJson = command.ClientMetadataJson,
        };

        dbContext.PlaidLinkSessions.Add(linkSession);
        dbContext.PlaidLinkSessionEvents.Add(new PlaidLinkSessionEvent
        {
            Id = Guid.NewGuid(),
            PlaidLinkSessionId = linkSession.Id,
            EventType = "ISSUED",
            Source = "server",
            ClientMetadataJson = command.ClientMetadataJson,
            OccurredAtUtc = now,
            ProviderRequestId = providerResult.RequestId,
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return new IssuePlaidLinkTokenResult(
            linkSession.Id,
            providerResult.LinkToken,
            providerResult.ExpiresAtUtc,
            providerResult.Environment,
            providerResult.Products,
            providerResult.OAuthEnabled,
            providerResult.RedirectUri,
            providerResult.RequestId);
    }

    public async Task<LogPlaidLinkSessionEventResult?> LogLinkSessionEventAsync(
        LogPlaidLinkSessionEventCommand command,
        CancellationToken cancellationToken = default)
    {
        var session = await dbContext.PlaidLinkSessions
            .FirstOrDefaultAsync(x => x.Id == command.LinkSessionId, cancellationToken);

        if (session is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var normalizedEventType = command.EventType.Trim().ToUpperInvariant();

        dbContext.PlaidLinkSessionEvents.Add(new PlaidLinkSessionEvent
        {
            Id = Guid.NewGuid(),
            PlaidLinkSessionId = session.Id,
            EventType = normalizedEventType,
            Source = string.IsNullOrWhiteSpace(command.Source) ? "client" : command.Source.Trim().ToLowerInvariant(),
            ClientMetadataJson = command.ClientMetadataJson,
            OccurredAtUtc = now,
        });

        session.LastEventAtUtc = now;
        session.LastClientMetadataJson = command.ClientMetadataJson;
        session.Status = ResolveSessionStatusFromEvent(normalizedEventType, session.Status);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new LogPlaidLinkSessionEventResult(session.Id, normalizedEventType, now);
    }

    public async Task<ExchangePlaidPublicTokenResult> ExchangePublicTokenAsync(
        ExchangePlaidPublicTokenCommand command,
        CancellationToken cancellationToken = default)
    {
        PlaidLinkSession? session = null;
        if (command.LinkSessionId.HasValue)
        {
            session = await dbContext.PlaidLinkSessions
                .FirstOrDefaultAsync(x => x.Id == command.LinkSessionId.Value, cancellationToken);

            if (session is null)
            {
                throw new InvalidOperationException("Link session was not found.");
            }
        }

        var normalizedEnvironment = ResolveEnvironment();
        var providerResult = await tokenProvider.ExchangePublicTokenAsync(
            new PlaidPublicTokenExchangeRequest(
                command.PublicToken.Trim(),
                normalizedEnvironment,
                command.InstitutionId,
                command.ClientMetadataJson),
            cancellationToken);

        var now = DateTime.UtcNow;
        var protectedAccessToken = tokenProtector.Protect(providerResult.AccessToken);
        var tokenFingerprint = PlaidAccessTokenProtector.ComputeFingerprint(providerResult.AccessToken);

        var credential = await dbContext.PlaidItemCredentials
            .FirstOrDefaultAsync(
                x => x.PlaidEnvironment == providerResult.Environment && x.ItemId == providerResult.ItemId,
                cancellationToken);

        if (credential is null)
        {
            credential = new PlaidItemCredential
            {
                Id = Guid.NewGuid(),
                HouseholdId = command.HouseholdId ?? session?.HouseholdId,
                ItemId = providerResult.ItemId,
                PlaidEnvironment = providerResult.Environment,
                AccessTokenCiphertext = protectedAccessToken,
                AccessTokenFingerprint = tokenFingerprint,
                InstitutionId = providerResult.InstitutionId,
                Status = PlaidItemCredentialStatus.Active,
                CreatedAtUtc = now,
                LastRotatedAtUtc = now,
                LastProviderRequestId = providerResult.RequestId,
                LastLinkedSessionId = session?.Id,
                LastClientMetadataJson = command.ClientMetadataJson,
            };

            dbContext.PlaidItemCredentials.Add(credential);
        }
        else
        {
            credential.HouseholdId ??= command.HouseholdId ?? session?.HouseholdId;
            credential.AccessTokenCiphertext = protectedAccessToken;
            credential.AccessTokenFingerprint = tokenFingerprint;
            credential.InstitutionId = providerResult.InstitutionId;
            credential.Status = PlaidItemCredentialStatus.Active;
            credential.LastRotatedAtUtc = now;
            credential.LastProviderRequestId = providerResult.RequestId;
            credential.LastLinkedSessionId = session?.Id;
            credential.LastClientMetadataJson = command.ClientMetadataJson;
        }

        var bootstrapCursor = ResolveSyncBootstrapCursor();
        var bootstrapCount = ResolveSyncBootstrapCount();
        var syncBootstrapResult = await tokenProvider.BootstrapTransactionsSyncAsync(
            new PlaidTransactionsSyncBootstrapRequest(
                providerResult.AccessToken,
                providerResult.Environment,
                bootstrapCursor,
                bootstrapCount),
            cancellationToken);

        var syncState = await dbContext.PlaidItemSyncStates
            .FirstOrDefaultAsync(
                x => x.PlaidEnvironment == providerResult.Environment && x.ItemId == providerResult.ItemId,
                cancellationToken);

        var resolvedCursor = ResolveSyncCursor(syncBootstrapResult.NextCursor, bootstrapCursor);
        if (syncState is null)
        {
            syncState = new PlaidItemSyncState
            {
                Id = Guid.NewGuid(),
                ItemId = providerResult.ItemId,
                PlaidEnvironment = providerResult.Environment,
                Cursor = resolvedCursor,
                LastWebhookAtUtc = now,
                LastProviderRequestId = syncBootstrapResult.RequestId,
                SyncStatus = PlaidItemSyncStatus.Pending,
                PendingWebhookCount = 1,
            };

            dbContext.PlaidItemSyncStates.Add(syncState);
        }
        else
        {
            syncState.Cursor = resolvedCursor;
            syncState.LastWebhookAtUtc = now;
            syncState.LastProviderRequestId = syncBootstrapResult.RequestId;
            syncState.PendingWebhookCount = Math.Max(1, syncState.PendingWebhookCount + 1);

            if (syncState.SyncStatus != PlaidItemSyncStatus.Processing)
            {
                syncState.SyncStatus = PlaidItemSyncStatus.Pending;
            }
        }

        if (session is not null)
        {
            session.Status = PlaidLinkSessionStatus.Exchanged;
            session.LinkedItemId = providerResult.ItemId;
            session.LastEventAtUtc = now;
            session.LastProviderRequestId = providerResult.RequestId;
            session.LastClientMetadataJson = command.ClientMetadataJson;

            dbContext.PlaidLinkSessionEvents.Add(new PlaidLinkSessionEvent
            {
                Id = Guid.NewGuid(),
                PlaidLinkSessionId = session.Id,
                EventType = "PUBLIC_TOKEN_EXCHANGED",
                Source = "server",
                ClientMetadataJson = command.ClientMetadataJson,
                OccurredAtUtc = now,
                ProviderRequestId = providerResult.RequestId,
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ExchangePlaidPublicTokenResult(
            credential.Id,
            session?.Id,
            credential.ItemId,
            credential.PlaidEnvironment,
            credential.Status,
            credential.InstitutionId,
            credential.LastRotatedAtUtc);
    }

    public async Task<ProcessPlaidItemRecoveryWebhookResult?> ProcessItemRecoveryWebhookAsync(
        ProcessPlaidItemRecoveryWebhookCommand command,
        CancellationToken cancellationToken = default)
    {
        var normalizedEnvironment = NormalizeEnvironment(command.Environment);
        var normalizedItemId = command.ItemId.Trim();
        var normalizedWebhookType = command.WebhookType.Trim().ToUpperInvariant();
        var normalizedWebhookCode = command.WebhookCode.Trim().ToUpperInvariant();
        var normalizedErrorCode = NormalizeOptionalUpper(command.ErrorCode);

        var credential = await dbContext.PlaidItemCredentials
            .FirstOrDefaultAsync(
                x => x.PlaidEnvironment == normalizedEnvironment && x.ItemId == normalizedItemId,
                cancellationToken);

        if (credential is null)
        {
            return null;
        }

        var decision = ResolveRecoveryDecision(normalizedWebhookType, normalizedWebhookCode, normalizedErrorCode);
        var now = DateTime.UtcNow;
        var normalizedProviderRequestId = NormalizeOptional(command.ProviderRequestId);

        credential.Status = decision.CredentialStatus;
        credential.LastProviderRequestId = normalizedProviderRequestId;
        credential.LastClientMetadataJson = command.MetadataJson;
        credential.RecoveryAction = decision.RecoveryAction;
        credential.RecoveryReasonCode = decision.RecoveryReasonCode;
        credential.RecoverySignaledAtUtc = now;

        PlaidLinkSession? session = null;
        if (credential.LastLinkedSessionId.HasValue)
        {
            session = await dbContext.PlaidLinkSessions
                .FirstOrDefaultAsync(x => x.Id == credential.LastLinkedSessionId.Value, cancellationToken);
        }

        session ??= await dbContext.PlaidLinkSessions
            .Where(x => x.LinkedItemId == normalizedItemId && x.RequestedEnvironment == normalizedEnvironment)
            .OrderByDescending(x => x.LastEventAtUtc ?? x.LinkTokenCreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (session is not null)
        {
            session.Status = decision.SessionStatus;
            session.LastEventAtUtc = now;
            session.LastProviderRequestId = normalizedProviderRequestId;
            session.LastClientMetadataJson = command.MetadataJson;
            session.RecoveryAction = decision.RecoveryAction;
            session.RecoveryReasonCode = decision.RecoveryReasonCode;
            session.RecoverySignaledAtUtc = now;

            dbContext.PlaidLinkSessionEvents.Add(new PlaidLinkSessionEvent
            {
                Id = Guid.NewGuid(),
                PlaidLinkSessionId = session.Id,
                EventType = "ITEM_RECOVERY_WEBHOOK",
                Source = "provider",
                ClientMetadataJson = command.MetadataJson,
                ProviderRequestId = normalizedProviderRequestId,
                OccurredAtUtc = now,
            });

            credential.LastLinkedSessionId = session.Id;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ProcessPlaidItemRecoveryWebhookResult(
            credential.Id,
            session?.Id,
            credential.ItemId,
            credential.PlaidEnvironment,
            credential.Status,
            session?.Status,
            decision.RecoveryAction,
            decision.RecoveryReasonCode,
            now);
    }

    private string ResolveEnvironment()
    {
        var environment = options.Value.Environment;
        return string.IsNullOrWhiteSpace(environment)
            ? "sandbox"
            : environment.Trim().ToLowerInvariant();
    }

    private string NormalizeEnvironment(string environment)
    {
        return string.IsNullOrWhiteSpace(environment)
            ? ResolveEnvironment()
            : environment.Trim().ToLowerInvariant();
    }

    private IReadOnlyList<string> ResolveProducts(IReadOnlyList<string>? requestedProducts)
    {
        var products = (requestedProducts is { Count: > 0 } ? requestedProducts : options.Value.Products)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return products.Count == 0 ? ["transactions"] : products;
    }

    private IReadOnlyList<string> ResolveCountryCodes()
    {
        var countryCodes = options.Value.CountryCodes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return countryCodes.Count == 0 ? ["US"] : countryCodes;
    }

    private string? ResolveRedirectUri(string? requestRedirectUri)
    {
        var configuredUri = string.IsNullOrWhiteSpace(options.Value.RedirectUri)
            ? null
            : options.Value.RedirectUri.Trim();

        if (!string.IsNullOrWhiteSpace(requestRedirectUri))
        {
            return requestRedirectUri.Trim();
        }

        return configuredUri;
    }

    private string ResolveSyncBootstrapCursor()
    {
        var cursor = options.Value.TransactionsSyncBootstrapCursor;
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return DefaultSyncBootstrapCursor;
        }

        var normalizedCursor = cursor.Trim();
        if (string.Equals(normalizedCursor, HistoricalSyncBootstrapCursorMode, StringComparison.OrdinalIgnoreCase))
        {
            // Plaid interprets an empty cursor as "start from the beginning" for initial sync bootstrap.
            return string.Empty;
        }

        return normalizedCursor;
    }

    private int ResolveSyncBootstrapCount()
    {
        var count = options.Value.TransactionsSyncBootstrapCount;
        return count is < 1 or > 500
            ? 1
            : count;
    }

    private static string ResolveSyncCursor(string? nextCursor, string fallbackCursor)
    {
        if (!string.IsNullOrWhiteSpace(nextCursor))
        {
            return nextCursor.Trim();
        }

        return string.IsNullOrWhiteSpace(fallbackCursor)
            ? DefaultSyncBootstrapCursor
            : fallbackCursor.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeOptionalUpper(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    }

    private static PlaidRecoveryDecision ResolveRecoveryDecision(string webhookType, string webhookCode, string? errorCode)
    {
        if (!string.Equals(webhookType, "ITEM", StringComparison.Ordinal))
        {
            return PlaidRecoveryDecision.NeedsReview("webhook_type_unrecognized");
        }

        if (string.Equals(webhookCode, "USER_PERMISSION_REVOKED", StringComparison.Ordinal))
        {
            return PlaidRecoveryDecision.RequiresRelink("item_permission_revoked");
        }

        if (string.Equals(webhookCode, "PENDING_EXPIRATION", StringComparison.Ordinal))
        {
            return PlaidRecoveryDecision.RequiresUpdateMode("oauth_pending_expiration");
        }

        if (string.Equals(webhookCode, "ERROR", StringComparison.Ordinal))
        {
            if (errorCode is not null && RequiresRelinkErrorCodes.Contains(errorCode))
            {
                return PlaidRecoveryDecision.RequiresRelink("item_error_requires_relink");
            }

            if (errorCode is not null && RequiresUpdateModeErrorCodes.Contains(errorCode))
            {
                return PlaidRecoveryDecision.RequiresUpdateMode("item_error_requires_update_mode");
            }

            return PlaidRecoveryDecision.NeedsReview("item_error_ambiguous");
        }

        return PlaidRecoveryDecision.NeedsReview("webhook_signal_unrecognized");
    }

    private static PlaidLinkSessionStatus ResolveSessionStatusFromEvent(string eventType, PlaidLinkSessionStatus current)
    {
        return eventType switch
        {
            "OPEN" => PlaidLinkSessionStatus.Open,
            "EXIT" => PlaidLinkSessionStatus.Exit,
            "SUCCESS" => PlaidLinkSessionStatus.Success,
            _ => current,
        };
    }

    private sealed record PlaidRecoveryDecision(
        PlaidItemCredentialStatus CredentialStatus,
        PlaidLinkSessionStatus SessionStatus,
        string RecoveryAction,
        string RecoveryReasonCode)
    {
        public static PlaidRecoveryDecision RequiresRelink(string reasonCode)
            => new(PlaidItemCredentialStatus.RequiresRelink, PlaidLinkSessionStatus.RequiresRelink, "requires_relink", reasonCode);

        public static PlaidRecoveryDecision RequiresUpdateMode(string reasonCode)
            => new(PlaidItemCredentialStatus.RequiresUpdateMode, PlaidLinkSessionStatus.RequiresUpdateMode, "requires_update_mode", reasonCode);

        public static PlaidRecoveryDecision NeedsReview(string reasonCode)
            => new(PlaidItemCredentialStatus.NeedsReview, PlaidLinkSessionStatus.NeedsReview, "needs_review", reasonCode);
    }
}
