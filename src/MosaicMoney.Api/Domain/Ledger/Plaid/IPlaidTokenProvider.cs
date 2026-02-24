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

public sealed record PlaidTransactionsSyncBootstrapRequest(
    string AccessToken,
    string Environment,
    string Cursor,
    int Count);

public sealed record PlaidTransactionsSyncBootstrapResult(
    string NextCursor,
    bool HasMore,
    string RequestId);

public sealed record PlaidTransactionsSyncPullRequest(
    string AccessToken,
    string Environment,
    string Cursor,
    int Count);

public sealed record PlaidTransactionsSyncAccount(
    string PlaidAccountId,
    string Name,
    string? OfficialName,
    string? Mask,
    string? Type,
    string? Subtype);

public sealed record PlaidTransactionsSyncDeltaTransaction(
    string PlaidTransactionId,
    string PlaidAccountId,
    string Description,
    string? MerchantName,
    decimal Amount,
    DateOnly? TransactionDate,
    string RawPayloadJson,
    bool Pending);

public sealed record PlaidTransactionsSyncPullResult(
    string NextCursor,
    bool HasMore,
    string RequestId,
    IReadOnlyList<PlaidTransactionsSyncAccount> Accounts,
    IReadOnlyList<PlaidTransactionsSyncDeltaTransaction> Added,
    IReadOnlyList<PlaidTransactionsSyncDeltaTransaction> Modified,
    IReadOnlyList<string> RemovedTransactionIds);

public sealed record PlaidLiabilitiesGetRequest(
    string AccessToken,
    string Environment);

public sealed record PlaidLiabilityAccount(
    string PlaidAccountId,
    string Name,
    string? OfficialName,
    string? Mask,
    string? Type,
    string? Subtype,
    decimal? CurrentBalance);

public sealed record PlaidLiabilitySnapshot(
    string PlaidAccountId,
    string LiabilityType,
    DateOnly? AsOfDate,
    decimal? CurrentBalance,
    decimal? LastStatementBalance,
    decimal? MinimumPayment,
    decimal? LastPaymentAmount,
    DateOnly? LastPaymentDate,
    DateOnly? NextPaymentDueDate,
    decimal? Apr,
    string RawLiabilityJson);

public sealed record PlaidLiabilitiesGetResult(
    string RequestId,
    IReadOnlyList<PlaidLiabilityAccount> Accounts,
    IReadOnlyList<PlaidLiabilitySnapshot> Snapshots);

public sealed record PlaidInvestmentsHoldingsGetRequest(
    string AccessToken,
    string Environment);

public sealed record PlaidInvestmentAccount(
    string PlaidAccountId,
    string Name,
    string? OfficialName,
    string? Mask,
    string? Type,
    string? Subtype);

public sealed record PlaidInvestmentHolding(
    string PlaidAccountId,
    string PlaidSecurityId,
    decimal Quantity,
    decimal InstitutionPrice,
    DateOnly? InstitutionPriceAsOf,
    decimal InstitutionValue,
    decimal? CostBasis,
    string RawHoldingJson);

public sealed record PlaidInvestmentSecurity(
    string PlaidSecurityId,
    string? TickerSymbol,
    string? Name);

public sealed record PlaidInvestmentsHoldingsGetResult(
    string RequestId,
    IReadOnlyList<PlaidInvestmentAccount> Accounts,
    IReadOnlyList<PlaidInvestmentHolding> Holdings,
    IReadOnlyList<PlaidInvestmentSecurity> Securities);

public sealed record PlaidTransactionsRecurringGetRequest(
    string AccessToken,
    string Environment);

public sealed record PlaidRecurringStream(
    string StreamId,
    string PlaidAccountId,
    string Description,
    string MerchantName,
    decimal? LastAmount,
    DateOnly? LastDate,
    string Frequency,
    bool IsActive,
    string RawStreamJson);

public sealed record PlaidTransactionsRecurringGetResult(
    string RequestId,
    IReadOnlyList<PlaidRecurringStream> InflowStreams,
    IReadOnlyList<PlaidRecurringStream> OutflowStreams);

public interface IPlaidTokenProvider
{
    Task<PlaidLinkTokenCreateResult> CreateLinkTokenAsync(
        PlaidLinkTokenCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<PlaidPublicTokenExchangeResult> ExchangePublicTokenAsync(
        PlaidPublicTokenExchangeRequest request,
        CancellationToken cancellationToken = default);

    Task<PlaidTransactionsSyncBootstrapResult> BootstrapTransactionsSyncAsync(
        PlaidTransactionsSyncBootstrapRequest request,
        CancellationToken cancellationToken = default);

    Task<PlaidTransactionsSyncPullResult> PullTransactionsSyncAsync(
        PlaidTransactionsSyncPullRequest request,
        CancellationToken cancellationToken = default);

    Task<PlaidLiabilitiesGetResult> GetLiabilitiesAsync(
        PlaidLiabilitiesGetRequest request,
        CancellationToken cancellationToken = default);

    Task<PlaidInvestmentsHoldingsGetResult> GetInvestmentsHoldingsAsync(
        PlaidInvestmentsHoldingsGetRequest request,
        CancellationToken cancellationToken = default);

    Task<PlaidTransactionsRecurringGetResult> GetTransactionsRecurringAsync(
        PlaidTransactionsRecurringGetRequest request,
        CancellationToken cancellationToken = default);
}
