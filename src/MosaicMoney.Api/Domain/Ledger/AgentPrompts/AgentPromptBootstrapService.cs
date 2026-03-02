using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Data;

namespace MosaicMoney.Api.Domain.Ledger.AgentPrompts;

public sealed record AgentPromptBootstrapResult(
    int Inserted,
    int Updated,
    int Archived);

public sealed class AgentPromptBootstrapService(
    MosaicMoneyDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<AgentPromptBootstrapService> logger)
{
    public async Task<AgentPromptBootstrapResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var prompts = await dbContext.AgentReusablePrompts
            .Where(x => x.Scope == AgentPromptScope.Platform)
            .ToListAsync(cancellationToken);

        var promptsByStableKey = prompts
            .Where(x => !string.IsNullOrWhiteSpace(x.StableKey))
            .GroupBy(x => x.StableKey!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => x
                    .OrderBy(candidate => candidate.IsArchived)
                    .ThenBy(candidate => candidate.CreatedAtUtc)
                    .First(),
                StringComparer.OrdinalIgnoreCase);

        var inserted = 0;
        var updated = 0;
        var archived = 0;
        var activeSeedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var seed in AgentPromptSeedManifest.Prompts.OrderBy(x => x.DisplayOrder))
        {
            activeSeedKeys.Add(seed.StableKey);

            if (!promptsByStableKey.TryGetValue(seed.StableKey, out var prompt))
            {
                prompt = new AgentReusablePrompt
                {
                    Id = Guid.CreateVersion7(),
                    StableKey = seed.StableKey,
                    Title = seed.Title,
                    PromptText = seed.PromptText,
                    Scope = AgentPromptScope.Platform,
                    HouseholdId = null,
                    HouseholdUserId = null,
                    IsFavorite = false,
                    DisplayOrder = seed.DisplayOrder,
                    UsageCount = 0,
                    LastUsedAtUtc = null,
                    IsArchived = false,
                    ArchivedAtUtc = null,
                    CreatedAtUtc = now,
                    LastModifiedAtUtc = now,
                };

                dbContext.AgentReusablePrompts.Add(prompt);
                prompts.Add(prompt);
                promptsByStableKey[seed.StableKey] = prompt;
                inserted++;
                continue;
            }

            var changed = false;
            if (!string.Equals(prompt.Title, seed.Title, StringComparison.Ordinal))
            {
                prompt.Title = seed.Title;
                changed = true;
            }

            if (!string.Equals(prompt.PromptText, seed.PromptText, StringComparison.Ordinal))
            {
                prompt.PromptText = seed.PromptText;
                changed = true;
            }

            if (prompt.DisplayOrder != seed.DisplayOrder)
            {
                prompt.DisplayOrder = seed.DisplayOrder;
                changed = true;
            }

            if (prompt.Scope != AgentPromptScope.Platform)
            {
                prompt.Scope = AgentPromptScope.Platform;
                changed = true;
            }

            if (prompt.HouseholdId.HasValue)
            {
                prompt.HouseholdId = null;
                changed = true;
            }

            if (prompt.HouseholdUserId.HasValue)
            {
                prompt.HouseholdUserId = null;
                changed = true;
            }

            if (prompt.IsFavorite)
            {
                prompt.IsFavorite = false;
                changed = true;
            }

            if (prompt.IsArchived)
            {
                prompt.IsArchived = false;
                prompt.ArchivedAtUtc = null;
                changed = true;
            }

            if (changed)
            {
                prompt.LastModifiedAtUtc = now;
                updated++;
            }
        }

        foreach (var prompt in prompts)
        {
            if (string.IsNullOrWhiteSpace(prompt.StableKey))
            {
                continue;
            }

            if (activeSeedKeys.Contains(prompt.StableKey))
            {
                continue;
            }

            if (prompt.IsArchived)
            {
                continue;
            }

            prompt.IsArchived = true;
            prompt.ArchivedAtUtc = now;
            prompt.LastModifiedAtUtc = now;
            prompt.IsFavorite = false;
            archived++;
        }

        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var result = new AgentPromptBootstrapResult(inserted, updated, archived);
        logger.LogInformation(
            "Agent prompt bootstrap complete. Inserted={Inserted}, Updated={Updated}, Archived={Archived}.",
            result.Inserted,
            result.Updated,
            result.Archived);

        return result;
    }
}
