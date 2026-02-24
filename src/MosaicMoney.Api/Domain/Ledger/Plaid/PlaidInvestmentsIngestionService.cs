using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Data;

namespace MosaicMoney.Api.Domain.Ledger.Plaid;

public sealed record ProcessPlaidInvestmentsWebhookCommand(
    string WebhookType,
    string WebhookCode,
    string ItemId,
    string Environment,
    string? ProviderRequestId);

public sealed record ProcessPlaidInvestmentsWebhookResult(
    Guid CredentialId,
    string ItemId,
    string Environment,
    int AccountsUpsertedCount,
    int HoldingsInsertedCount,
    DateTime ProcessedAtUtc,
    string? LastProviderRequestId);

public sealed class PlaidInvestmentsIngestionService(
    MosaicMoneyDbContext dbContext,
    IPlaidTokenProvider tokenProvider,
    PlaidAccessTokenProtector tokenProtector)
{
    public async Task<ProcessPlaidInvestmentsWebhookResult?> ProcessDefaultUpdateWebhookAsync(
        ProcessPlaidInvestmentsWebhookCommand command,
        CancellationToken cancellationToken = default)
    {
        var normalizedEnvironment = NormalizeEnvironment(command.Environment);
        var normalizedItemId = command.ItemId.Trim();

        var credential = await dbContext.PlaidItemCredentials
            .FirstOrDefaultAsync(
                x => x.PlaidEnvironment == normalizedEnvironment && x.ItemId == normalizedItemId,
                cancellationToken);

        if (credential is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var accessToken = tokenProtector.Unprotect(credential.AccessTokenCiphertext);
        var investmentsResult = await tokenProvider.GetInvestmentsHoldingsAsync(
            new PlaidInvestmentsHoldingsGetRequest(accessToken, normalizedEnvironment),
            cancellationToken);

        var resolvedRequestId = NormalizeOptional(command.ProviderRequestId) ?? investmentsResult.RequestId;

        var existingAccounts = await dbContext.InvestmentAccounts
            .Where(x => x.ItemId == normalizedItemId && x.PlaidEnvironment == normalizedEnvironment)
            .ToListAsync(cancellationToken);

        var accountsByPlaidAccountId = existingAccounts.ToDictionary(
            x => x.PlaidAccountId,
            StringComparer.Ordinal);

        var seenPlaidAccountIds = new HashSet<string>(StringComparer.Ordinal);
        var accountsUpsertedCount = 0;

        foreach (var accountPayload in investmentsResult.Accounts)
        {
            var plaidAccountId = accountPayload.PlaidAccountId.Trim();
            if (string.IsNullOrWhiteSpace(plaidAccountId))
            {
                continue;
            }

            seenPlaidAccountIds.Add(plaidAccountId);

            if (!accountsByPlaidAccountId.TryGetValue(plaidAccountId, out var account))
            {
                account = new InvestmentAccount
                {
                    Id = Guid.NewGuid(),
                    HouseholdId = credential.HouseholdId,
                    ItemId = normalizedItemId,
                    PlaidEnvironment = normalizedEnvironment,
                    PlaidAccountId = plaidAccountId,
                    Name = ResolveName(accountPayload.Name, accountPayload.OfficialName, plaidAccountId),
                    OfficialName = NormalizeOptional(accountPayload.OfficialName),
                    Mask = NormalizeOptional(accountPayload.Mask),
                    AccountType = NormalizeOptional(accountPayload.Type),
                    AccountSubtype = NormalizeOptional(accountPayload.Subtype),
                    IsActive = true,
                    CreatedAtUtc = now,
                    LastSeenAtUtc = now,
                    LastProviderRequestId = resolvedRequestId,
                };

                dbContext.InvestmentAccounts.Add(account);
                accountsByPlaidAccountId[plaidAccountId] = account;
            }
            else
            {
                account.HouseholdId ??= credential.HouseholdId;
                account.Name = ResolveName(accountPayload.Name, accountPayload.OfficialName, plaidAccountId);
                account.OfficialName = NormalizeOptional(accountPayload.OfficialName);
                account.Mask = NormalizeOptional(accountPayload.Mask);
                account.AccountType = NormalizeOptional(accountPayload.Type);
                account.AccountSubtype = NormalizeOptional(accountPayload.Subtype);
                account.IsActive = true;
                account.LastSeenAtUtc = now;
                account.LastProviderRequestId = resolvedRequestId;
            }

            accountsUpsertedCount++;
        }

        foreach (var account in existingAccounts)
        {
            if (!seenPlaidAccountIds.Contains(account.PlaidAccountId) && account.IsActive)
            {
                account.IsActive = false;
                account.LastSeenAtUtc = now;
                account.LastProviderRequestId = resolvedRequestId;
            }
        }

        var targetAccountIds = accountsByPlaidAccountId.Values
            .Select(x => x.Id)
            .Distinct()
            .ToList();

        var existingSnapshotKeys = new HashSet<string>(StringComparer.Ordinal);
        if (targetAccountIds.Count > 0)
        {
            var persistedSnapshotKeys = await dbContext.InvestmentHoldingSnapshots
                .Where(x => targetAccountIds.Contains(x.InvestmentAccountId))
                .Select(x => new { x.InvestmentAccountId, x.SnapshotHash })
                .ToListAsync(cancellationToken);

            foreach (var key in persistedSnapshotKeys)
            {
                existingSnapshotKeys.Add(BuildSnapshotKey(key.InvestmentAccountId, key.SnapshotHash));
            }
        }

        var securitiesByPlaidSecurityId = investmentsResult.Securities.ToDictionary(
            x => x.PlaidSecurityId,
            StringComparer.Ordinal);

        var holdingsInsertedCount = 0;
        foreach (var holdingPayload in investmentsResult.Holdings)
        {
            var plaidAccountId = holdingPayload.PlaidAccountId.Trim();
            if (!accountsByPlaidAccountId.TryGetValue(plaidAccountId, out var investmentAccount))
            {
                continue;
            }

            var snapshotHash = ComputeSha256(holdingPayload.RawHoldingJson);
            var snapshotKey = BuildSnapshotKey(investmentAccount.Id, snapshotHash);

            if (existingSnapshotKeys.Contains(snapshotKey))
            {
                continue;
            }

            securitiesByPlaidSecurityId.TryGetValue(holdingPayload.PlaidSecurityId, out var security);

            dbContext.InvestmentHoldingSnapshots.Add(new InvestmentHoldingSnapshot
            {
                Id = Guid.NewGuid(),
                InvestmentAccountId = investmentAccount.Id,
                PlaidSecurityId = holdingPayload.PlaidSecurityId,
                TickerSymbol = NormalizeOptional(security?.TickerSymbol),
                Name = NormalizeOptional(security?.Name),
                Quantity = RoundQuantity(holdingPayload.Quantity),
                InstitutionPrice = RoundPrice(holdingPayload.InstitutionPrice),
                InstitutionPriceAsOf = holdingPayload.InstitutionPriceAsOf,
                InstitutionValue = RoundMoney(holdingPayload.InstitutionValue),
                CostBasis = RoundPrice(holdingPayload.CostBasis),
                SnapshotHash = snapshotHash,
                RawHoldingJson = holdingPayload.RawHoldingJson,
                CapturedAtUtc = now,
                ProviderRequestId = resolvedRequestId,
            });

            existingSnapshotKeys.Add(snapshotKey);
            holdingsInsertedCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ProcessPlaidInvestmentsWebhookResult(
            credential.Id,
            normalizedItemId,
            normalizedEnvironment,
            accountsUpsertedCount,
            holdingsInsertedCount,
            now,
            resolvedRequestId);
    }

    private static string ResolveName(string name, string? officialName, string plaidAccountId)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(officialName))
        {
            return officialName.Trim();
        }

        var suffix = plaidAccountId.Length <= 6
            ? plaidAccountId
            : plaidAccountId[^6..];

        return $"Investment Account {suffix}";
    }

    private static decimal RoundMoney(decimal value)
    {
        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal RoundQuantity(decimal value)
    {
        return decimal.Round(value, 4, MidpointRounding.AwayFromZero);
    }

    private static decimal RoundPrice(decimal value)
    {
        return decimal.Round(value, 4, MidpointRounding.AwayFromZero);
    }

    private static decimal? RoundPrice(decimal? value)
    {
        return value.HasValue
            ? decimal.Round(value.Value, 4, MidpointRounding.AwayFromZero)
            : null;
    }

    private static string NormalizeEnvironment(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "sandbox"
            : value.Trim().ToLowerInvariant();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string BuildSnapshotKey(Guid accountId, string snapshotHash)
    {
        return string.Concat(accountId, "|", snapshotHash);
    }

    private static string ComputeSha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}