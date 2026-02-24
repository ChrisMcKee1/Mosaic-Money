using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Data;

namespace MosaicMoney.Api.Domain.Ledger.Plaid;

public sealed record ProcessPlaidRecurringWebhookCommand(
    string WebhookType,
    string WebhookCode,
    string ItemId,
    string Environment,
    string? ProviderRequestId);

public sealed record ProcessPlaidRecurringWebhookResult(
    Guid CredentialId,
    string ItemId,
    string Environment,
    int StreamsProcessedCount,
    DateTime ProcessedAtUtc,
    string? LastProviderRequestId);

public sealed class PlaidRecurringIngestionService(
    MosaicMoneyDbContext dbContext,
    IPlaidTokenProvider tokenProvider,
    PlaidAccessTokenProtector tokenProtector)
{
    public async Task<ProcessPlaidRecurringWebhookResult?> ProcessRecurringUpdateWebhookAsync(
        ProcessPlaidRecurringWebhookCommand command,
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
        var recurringResult = await tokenProvider.GetTransactionsRecurringAsync(
            new PlaidTransactionsRecurringGetRequest(accessToken, normalizedEnvironment),
            cancellationToken);

        var resolvedRequestId = NormalizeOptional(command.ProviderRequestId) ?? recurringResult.RequestId;

        var streamsProcessedCount = 0;

        // For MVP, we just log or store the streams as optional enrichment on existing recurring items.
        // The spec says: "Add optional enrichment fields (nullable) on recurring templates or mappings: PlaidRecurringStreamId, PlaidRecurringConfidence, PlaidRecurringLastSeenAtUtc, RecurringSource (deterministic or plaid)."
        // We will try to match by MerchantName or just insert new ones if they don't exist?
        // "Conflicts between deterministic and Plaid-derived recurring links route to NeedsReview; never auto-overwrite approved user decisions."
        // For now, we will just insert new Plaid-sourced recurring items if they don't match.

        var existingRecurringItems = await dbContext.RecurringItems
            .Where(x => x.HouseholdId == credential.HouseholdId)
            .ToListAsync(cancellationToken);

        var allStreams = recurringResult.InflowStreams.Concat(recurringResult.OutflowStreams).ToList();

        foreach (var stream in allStreams)
        {
            if (!stream.IsActive)
            {
                continue;
            }

            var merchantName = NormalizeOptional(stream.MerchantName) ?? NormalizeOptional(stream.Description) ?? "Unknown Merchant";
            
            var existingItem = existingRecurringItems.FirstOrDefault(x => 
                x.PlaidRecurringStreamId == stream.StreamId || 
                (x.MerchantName.Equals(merchantName, StringComparison.OrdinalIgnoreCase) && x.RecurringSource == "deterministic"));

            if (existingItem != null)
            {
                if (existingItem.RecurringSource == "plaid" || string.IsNullOrEmpty(existingItem.PlaidRecurringStreamId))
                {
                    existingItem.PlaidRecurringStreamId = stream.StreamId;
                    existingItem.PlaidRecurringLastSeenAtUtc = now;
                    existingItem.ExpectedAmount = stream.LastAmount ?? existingItem.ExpectedAmount;
                    existingItem.NextDueDate = stream.LastDate ?? existingItem.NextDueDate;
                }
            }
            else
            {
                var newItem = new RecurringItem
                {
                    Id = Guid.NewGuid(),
                    HouseholdId = credential.HouseholdId ?? Guid.Empty,
                    MerchantName = merchantName,
                    ExpectedAmount = stream.LastAmount ?? 0m,
                    IsVariable = false,
                    Frequency = ResolveFrequency(stream.Frequency),
                    NextDueDate = stream.LastDate ?? DateOnly.FromDateTime(now),
                    PlaidRecurringStreamId = stream.StreamId,
                    PlaidRecurringLastSeenAtUtc = now,
                    RecurringSource = "plaid",
                    IsActive = true
                };

                if (newItem.HouseholdId != Guid.Empty)
                {
                    dbContext.RecurringItems.Add(newItem);
                    existingRecurringItems.Add(newItem);
                }
            }

            streamsProcessedCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ProcessPlaidRecurringWebhookResult(
            credential.Id,
            normalizedItemId,
            normalizedEnvironment,
            streamsProcessedCount,
            now,
            resolvedRequestId);
    }

    private static RecurringFrequency ResolveFrequency(string frequency)
    {
        return frequency.ToLowerInvariant() switch
        {
            "weekly" => RecurringFrequency.Weekly,
            "biweekly" => RecurringFrequency.BiWeekly,
            "monthly" => RecurringFrequency.Monthly,
            "annually" => RecurringFrequency.Annually,
            _ => RecurringFrequency.Monthly,
        };
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
}