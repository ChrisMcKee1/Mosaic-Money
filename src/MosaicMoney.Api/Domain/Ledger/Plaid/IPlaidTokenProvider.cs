namespace MosaicMoney.Api.Domain.Ledger.Plaid;

public sealed record PlaidLinkTokenCreateRequest(
    string ClientUserId,
    string Environment,
    string? RedirectUri,
    IReadOnlyList<string> Products,
    IReadOnlyList<string> CountryCodes,
    bool OAuthEnabled,
    string OAuthStateId,
    string? ClientMetadataJson);

public sealed record PlaidLinkTokenCreateResult(
    string LinkToken,
    DateTime ExpiresAtUtc,
    string Environment,
    IReadOnlyList<string> Products,
    bool OAuthEnabled,
    string? RedirectUri,
    string RequestId);

public sealed record PlaidPublicTokenExchangeRequest(
    string PublicToken,
    string Environment,
    string? InstitutionId,
    string? ClientMetadataJson);

public sealed record PlaidPublicTokenExchangeResult(
    string ItemId,
    string AccessToken,
    string Environment,
    string? InstitutionId,
    string RequestId);

public interface IPlaidTokenProvider
{
    Task<PlaidLinkTokenCreateResult> CreateLinkTokenAsync(
        PlaidLinkTokenCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<PlaidPublicTokenExchangeResult> ExchangePublicTokenAsync(
        PlaidPublicTokenExchangeRequest request,
        CancellationToken cancellationToken = default);
}
