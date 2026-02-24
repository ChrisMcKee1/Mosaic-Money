using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace MosaicMoney.Api.Domain.Ledger.Plaid;

// Deterministic fallback for local-only/test workflows when explicitly enabled.
public sealed class DeterministicPlaidTokenProvider(IOptions<PlaidOptions> plaidOptions) : IPlaidTokenProvider
{
    public Task<PlaidLinkTokenCreateResult> CreateLinkTokenAsync(
        PlaidLinkTokenCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfiguration();

        var tokenSeed = $"{request.Environment}|{request.ClientUserId}|{request.OAuthStateId}|{DateTime.UtcNow:O}";
        var hash = ComputeSha256(tokenSeed);
        var linkToken = $"link-sim-{hash[..24]}";
        var requestId = $"req-sim-{Guid.NewGuid():N}";

        return Task.FromResult(new PlaidLinkTokenCreateResult(
            linkToken,
            DateTime.UtcNow.AddHours(4),
            request.Environment,
            request.Products,
            request.OAuthEnabled,
            request.RedirectUri,
            requestId));
    }

    public Task<PlaidPublicTokenExchangeResult> ExchangePublicTokenAsync(
        PlaidPublicTokenExchangeRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfiguration();

        var normalizedPublicToken = request.PublicToken.Trim();
        var hash = ComputeSha256($"{request.Environment}|{normalizedPublicToken}");

        var itemId = $"item-sim-{hash[..16]}";
        var accessToken = $"access-sim-{hash[..32]}";
        var requestId = $"req-sim-{Guid.NewGuid():N}";

        return Task.FromResult(new PlaidPublicTokenExchangeResult(
            itemId,
            accessToken,
            request.Environment,
            request.InstitutionId,
            requestId));
    }

    public Task<PlaidTransactionsSyncBootstrapResult> BootstrapTransactionsSyncAsync(
        PlaidTransactionsSyncBootstrapRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfiguration();

        var normalizedCursor = string.IsNullOrWhiteSpace(request.Cursor)
            ? "now"
            : request.Cursor.Trim();
        var hash = ComputeSha256($"{request.Environment}|{request.AccessToken}|{normalizedCursor}|{request.Count}");

        return Task.FromResult(new PlaidTransactionsSyncBootstrapResult(
            $"cursor-sim-{hash[..24]}",
            HasMore: false,
            RequestId: $"req-sim-{Guid.NewGuid():N}"));
    }

    public Task<PlaidTransactionsSyncPullResult> PullTransactionsSyncAsync(
        PlaidTransactionsSyncPullRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfiguration();

        var normalizedCursor = string.IsNullOrWhiteSpace(request.Cursor)
            ? "now"
            : request.Cursor.Trim();

        return Task.FromResult(new PlaidTransactionsSyncPullResult(
            normalizedCursor,
            HasMore: false,
            RequestId: $"req-sim-{Guid.NewGuid():N}",
            Accounts: [],
            Added: [],
            Modified: [],
            RemovedTransactionIds: []));
    }

    private void EnsureConfiguration()
    {
        var options = plaidOptions.Value;

        if (string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.Secret))
        {
            throw new InvalidOperationException(
                "Plaid ClientId/Secret are not configured. Provide values through AppHost parameters and user-secrets.");
        }
    }

    private static string ComputeSha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
