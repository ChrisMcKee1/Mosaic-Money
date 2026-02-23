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

public sealed class PlaidLinkLifecycleService(
    MosaicMoneyDbContext dbContext,
    IPlaidTokenProvider tokenProvider,
    PlaidAccessTokenProtector tokenProtector,
    IOptions<PlaidOptions> options)
{
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

    private string ResolveEnvironment()
    {
        var environment = options.Value.Environment;
        return string.IsNullOrWhiteSpace(environment)
            ? "sandbox"
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
}
