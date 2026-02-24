using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Data;

namespace MosaicMoney.Api.Domain.Ledger.Plaid;

public sealed record ProcessPlaidLiabilitiesWebhookCommand(
    string WebhookType,
    string WebhookCode,
    string ItemId,
    string Environment,
    string? ProviderRequestId);

public sealed record ProcessPlaidLiabilitiesWebhookResult(
    Guid CredentialId,
    string ItemId,
    string Environment,
    int AccountsUpsertedCount,
    int SnapshotsInsertedCount,
    DateTime ProcessedAtUtc,
    string? LastProviderRequestId);

public sealed class PlaidLiabilitiesIngestionService(
    MosaicMoneyDbContext dbContext,
    IPlaidTokenProvider tokenProvider,
    PlaidAccessTokenProtector tokenProtector)
{
    public async Task<ProcessPlaidLiabilitiesWebhookResult?> ProcessDefaultUpdateWebhookAsync(
        ProcessPlaidLiabilitiesWebhookCommand command,
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
        var liabilitiesResult = await tokenProvider.GetLiabilitiesAsync(
            new PlaidLiabilitiesGetRequest(accessToken, normalizedEnvironment),
            cancellationToken);

        var resolvedRequestId = NormalizeOptional(command.ProviderRequestId) ?? liabilitiesResult.RequestId;

        var existingAccounts = await dbContext.LiabilityAccounts
            .Where(x => x.ItemId == normalizedItemId && x.PlaidEnvironment == normalizedEnvironment)
            .ToListAsync(cancellationToken);

        var accountsByPlaidAccountId = existingAccounts.ToDictionary(
            x => x.PlaidAccountId,
            StringComparer.Ordinal);

        var seenPlaidAccountIds = new HashSet<string>(StringComparer.Ordinal);
        var accountsUpsertedCount = 0;

        foreach (var accountPayload in liabilitiesResult.Accounts)
        {
            var plaidAccountId = accountPayload.PlaidAccountId.Trim();
            if (string.IsNullOrWhiteSpace(plaidAccountId))
            {
                continue;
            }

            seenPlaidAccountIds.Add(plaidAccountId);

            if (!accountsByPlaidAccountId.TryGetValue(plaidAccountId, out var account))
            {
                account = new LiabilityAccount
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

                dbContext.LiabilityAccounts.Add(account);
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
            var persistedSnapshotKeys = await dbContext.LiabilitySnapshots
                .Where(x => targetAccountIds.Contains(x.LiabilityAccountId))
                .Select(x => new { x.LiabilityAccountId, x.SnapshotHash })
                .ToListAsync(cancellationToken);

            foreach (var key in persistedSnapshotKeys)
            {
                existingSnapshotKeys.Add(BuildSnapshotKey(key.LiabilityAccountId, key.SnapshotHash));
            }
        }

        var snapshotsInsertedCount = 0;
        foreach (var snapshotPayload in liabilitiesResult.Snapshots)
        {
            var plaidAccountId = snapshotPayload.PlaidAccountId.Trim();
            if (!accountsByPlaidAccountId.TryGetValue(plaidAccountId, out var liabilityAccount))
            {
                continue;
            }

            var snapshotHash = ComputeSha256(snapshotPayload.RawLiabilityJson);
            var snapshotKey = BuildSnapshotKey(liabilityAccount.Id, snapshotHash);

            if (existingSnapshotKeys.Contains(snapshotKey))
            {
                continue;
            }

            dbContext.LiabilitySnapshots.Add(new LiabilitySnapshot
            {
                Id = Guid.NewGuid(),
                LiabilityAccountId = liabilityAccount.Id,
                LiabilityType = ResolveLiabilityType(snapshotPayload.LiabilityType),
                AsOfDate = snapshotPayload.AsOfDate,
                CurrentBalance = RoundMoney(snapshotPayload.CurrentBalance),
                LastStatementBalance = RoundMoney(snapshotPayload.LastStatementBalance),
                MinimumPayment = RoundMoney(snapshotPayload.MinimumPayment),
                LastPaymentAmount = RoundMoney(snapshotPayload.LastPaymentAmount),
                LastPaymentDate = snapshotPayload.LastPaymentDate,
                NextPaymentDueDate = snapshotPayload.NextPaymentDueDate,
                Apr = RoundApr(snapshotPayload.Apr),
                SnapshotHash = snapshotHash,
                RawLiabilityJson = snapshotPayload.RawLiabilityJson,
                CapturedAtUtc = now,
                ProviderRequestId = resolvedRequestId,
            });

            existingSnapshotKeys.Add(snapshotKey);
            snapshotsInsertedCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ProcessPlaidLiabilitiesWebhookResult(
            credential.Id,
            normalizedItemId,
            normalizedEnvironment,
            accountsUpsertedCount,
            snapshotsInsertedCount,
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

        return $"Liability Account {suffix}";
    }

    private static string ResolveLiabilityType(string? liabilityType)
    {
        if (!string.IsNullOrWhiteSpace(liabilityType))
        {
            return liabilityType.Trim().ToLowerInvariant();
        }

        return "unknown";
    }

    private static decimal? RoundMoney(decimal? value)
    {
        return value.HasValue
            ? decimal.Round(value.Value, 2, MidpointRounding.AwayFromZero)
            : null;
    }

    private static decimal? RoundApr(decimal? value)
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
