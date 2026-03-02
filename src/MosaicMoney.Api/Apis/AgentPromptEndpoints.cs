using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Contracts.V1;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.AgentPrompts;

namespace MosaicMoney.Api.Apis;

public static class AgentPromptEndpoints
{
    public static RouteGroupBuilder MapAgentPromptEndpoints(this RouteGroupBuilder group)
    {
        var promptsGroup = group.MapGroup("/agent/prompts");

        promptsGroup.MapGet(string.Empty, async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            string? query,
            CancellationToken cancellationToken) =>
        {
            var actor = await ResolveActiveMemberAsync(httpContext, dbContext, cancellationToken);
            if (actor.ErrorResult is not null)
            {
                return actor.ErrorResult;
            }

            var actorContext = actor.Value!;
            var normalizedQuery = query?.Trim();
            var hasSearch = !string.IsNullOrWhiteSpace(normalizedQuery);
            var loweredSearch = normalizedQuery?.ToLowerInvariant();

            var baselineQuery = dbContext.AgentReusablePrompts
                .AsNoTracking()
                .Where(x => x.Scope == AgentPromptScope.Platform && !x.IsArchived);
            var userQuery = dbContext.AgentReusablePrompts
                .AsNoTracking()
                .Where(x =>
                    x.Scope == AgentPromptScope.User
                    && !x.IsArchived
                    && x.HouseholdId == actorContext.HouseholdId
                    && x.HouseholdUserId == actorContext.HouseholdUserId);

            if (hasSearch)
            {
                baselineQuery = baselineQuery.Where(x =>
                    x.Title.ToLower().Contains(loweredSearch!)
                    || x.PromptText.ToLower().Contains(loweredSearch!));

                userQuery = userQuery.Where(x =>
                    x.Title.ToLower().Contains(loweredSearch!)
                    || x.PromptText.ToLower().Contains(loweredSearch!));
            }

            var baselinePrompts = await baselineQuery
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.Title)
                .Select(x => MapPrompt(x))
                .ToListAsync(cancellationToken);

            var userPrompts = await userQuery
                .OrderByDescending(x => x.LastUsedAtUtc)
                .ThenByDescending(x => x.LastModifiedAtUtc)
                .ThenBy(x => x.Title)
                .Select(x => MapPrompt(x))
                .ToListAsync(cancellationToken);

            var favorites = userPrompts
                .Where(x => x.IsFavorite)
                .Take(3)
                .ToList();

            return Results.Ok(new AgentPromptLibraryDto(
                Favorites: favorites,
                UserPrompts: userPrompts,
                BaselinePrompts: baselinePrompts));
        });

        promptsGroup.MapPost(string.Empty, async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            CreateAgentPromptRequest request,
            CancellationToken cancellationToken) =>
        {
            var errors = ApiValidation.ValidateDataAnnotations(request).ToList();
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                errors.Add(new ApiValidationError(nameof(request.Title), "Title is required."));
            }

            if (string.IsNullOrWhiteSpace(request.PromptText))
            {
                errors.Add(new ApiValidationError(nameof(request.PromptText), "Prompt text is required."));
            }

            if (errors.Count > 0)
            {
                return ApiValidation.ToValidationResult(httpContext, errors);
            }

            var actor = await ResolveActiveMemberAsync(httpContext, dbContext, cancellationToken);
            if (actor.ErrorResult is not null)
            {
                return actor.ErrorResult;
            }

            var actorContext = actor.Value!;
            var trimmedTitle = request.Title.Trim();
            var trimmedPromptText = request.PromptText.Trim();

            var duplicateExists = await dbContext.AgentReusablePrompts
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.Scope == AgentPromptScope.User
                        && !x.IsArchived
                        && x.HouseholdId == actorContext.HouseholdId
                        && x.HouseholdUserId == actorContext.HouseholdUserId
                        && x.Title == trimmedTitle,
                    cancellationToken);

            if (duplicateExists)
            {
                return ApiValidation.ToConflictResult(
                    httpContext,
                    "agent_prompt_title_conflict",
                    "A saved prompt with this title already exists.");
            }

            var maxDisplayOrder = await dbContext.AgentReusablePrompts
                .AsNoTracking()
                .Where(x =>
                    x.Scope == AgentPromptScope.User
                    && !x.IsArchived
                    && x.HouseholdId == actorContext.HouseholdId
                    && x.HouseholdUserId == actorContext.HouseholdUserId)
                .Select(x => (int?)x.DisplayOrder)
                .MaxAsync(cancellationToken);

            var now = DateTime.UtcNow;
            var prompt = new AgentReusablePrompt
            {
                Id = Guid.CreateVersion7(),
                Title = trimmedTitle,
                PromptText = trimmedPromptText,
                Scope = AgentPromptScope.User,
                HouseholdId = actorContext.HouseholdId,
                HouseholdUserId = actorContext.HouseholdUserId,
                IsFavorite = request.IsFavorite,
                DisplayOrder = (maxDisplayOrder ?? -1) + 1,
                UsageCount = 0,
                LastUsedAtUtc = null,
                IsArchived = false,
                ArchivedAtUtc = null,
                CreatedAtUtc = now,
                LastModifiedAtUtc = now,
            };

            dbContext.AgentReusablePrompts.Add(prompt);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/v1/agent/prompts/{prompt.Id}", MapPrompt(prompt));
        });

        promptsGroup.MapPatch("/{id:guid}", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            Guid id,
            UpdateAgentPromptRequest request,
            CancellationToken cancellationToken) =>
        {
            var errors = ApiValidation.ValidateDataAnnotations(request).ToList();
            if (request.Title is not null && string.IsNullOrWhiteSpace(request.Title))
            {
                errors.Add(new ApiValidationError(nameof(request.Title), "Title cannot be empty when provided."));
            }

            if (request.PromptText is not null && string.IsNullOrWhiteSpace(request.PromptText))
            {
                errors.Add(new ApiValidationError(nameof(request.PromptText), "Prompt text cannot be empty when provided."));
            }

            if (errors.Count > 0)
            {
                return ApiValidation.ToValidationResult(httpContext, errors);
            }

            var actor = await ResolveActiveMemberAsync(httpContext, dbContext, cancellationToken);
            if (actor.ErrorResult is not null)
            {
                return actor.ErrorResult;
            }

            var actorContext = actor.Value!;

            var prompt = await dbContext.AgentReusablePrompts
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == id
                        && x.Scope == AgentPromptScope.User
                        && x.HouseholdId == actorContext.HouseholdId
                        && x.HouseholdUserId == actorContext.HouseholdUserId,
                    cancellationToken);

            if (prompt is null)
            {
                return ApiValidation.ToNotFoundResult(httpContext, "agent_prompt_not_found", "The requested prompt was not found.");
            }

            if (prompt.IsArchived)
            {
                return ApiValidation.ToConflictResult(httpContext, "agent_prompt_archived", "Archived prompts cannot be modified.");
            }

            var changed = false;
            if (request.Title is not null)
            {
                var trimmedTitle = request.Title.Trim();
                if (!string.Equals(prompt.Title, trimmedTitle, StringComparison.Ordinal))
                {
                    var duplicateExists = await dbContext.AgentReusablePrompts
                        .AsNoTracking()
                        .AnyAsync(
                            x =>
                                x.Id != prompt.Id
                                && x.Scope == AgentPromptScope.User
                                && !x.IsArchived
                                && x.HouseholdId == actorContext.HouseholdId
                                && x.HouseholdUserId == actorContext.HouseholdUserId
                                && x.Title == trimmedTitle,
                            cancellationToken);

                    if (duplicateExists)
                    {
                        return ApiValidation.ToConflictResult(
                            httpContext,
                            "agent_prompt_title_conflict",
                            "A saved prompt with this title already exists.");
                    }

                    prompt.Title = trimmedTitle;
                    changed = true;
                }
            }

            if (request.PromptText is not null)
            {
                var trimmedPromptText = request.PromptText.Trim();
                if (!string.Equals(prompt.PromptText, trimmedPromptText, StringComparison.Ordinal))
                {
                    prompt.PromptText = trimmedPromptText;
                    changed = true;
                }
            }

            if (request.IsFavorite.HasValue && prompt.IsFavorite != request.IsFavorite.Value)
            {
                prompt.IsFavorite = request.IsFavorite.Value;
                changed = true;
            }

            if (!changed)
            {
                return Results.Ok(MapPrompt(prompt));
            }

            prompt.LastModifiedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(MapPrompt(prompt));
        });

        promptsGroup.MapDelete("/{id:guid}", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            Guid id,
            CancellationToken cancellationToken) =>
        {
            var actor = await ResolveActiveMemberAsync(httpContext, dbContext, cancellationToken);
            if (actor.ErrorResult is not null)
            {
                return actor.ErrorResult;
            }

            var actorContext = actor.Value!;

            var prompt = await dbContext.AgentReusablePrompts
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == id
                        && x.Scope == AgentPromptScope.User
                        && x.HouseholdId == actorContext.HouseholdId
                        && x.HouseholdUserId == actorContext.HouseholdUserId,
                    cancellationToken);

            if (prompt is null)
            {
                return ApiValidation.ToNotFoundResult(httpContext, "agent_prompt_not_found", "The requested prompt was not found.");
            }

            if (!prompt.IsArchived)
            {
                var now = DateTime.UtcNow;
                prompt.IsArchived = true;
                prompt.ArchivedAtUtc = now;
                prompt.LastModifiedAtUtc = now;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return Results.NoContent();
        });

        promptsGroup.MapPost("/{id:guid}/use", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            Guid id,
            AgentPromptUseRequest _request,
            CancellationToken cancellationToken) =>
        {
            var actor = await ResolveActiveMemberAsync(httpContext, dbContext, cancellationToken);
            if (actor.ErrorResult is not null)
            {
                return actor.ErrorResult;
            }

            var actorContext = actor.Value!;

            var prompt = await dbContext.AgentReusablePrompts
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == id
                        && x.Scope == AgentPromptScope.User
                        && !x.IsArchived
                        && x.HouseholdId == actorContext.HouseholdId
                        && x.HouseholdUserId == actorContext.HouseholdUserId,
                    cancellationToken);

            if (prompt is null)
            {
                return ApiValidation.ToNotFoundResult(httpContext, "agent_prompt_not_found", "The requested prompt was not found.");
            }

            prompt.UsageCount += 1;
            prompt.LastUsedAtUtc = DateTime.UtcNow;
            prompt.LastModifiedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(MapPrompt(prompt));
        });

        promptsGroup.MapPost("/generate", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            IAgentPromptGenerationService generationService,
            GenerateAgentPromptSuggestionRequest request,
            CancellationToken cancellationToken) =>
        {
            var errors = ApiValidation.ValidateDataAnnotations(request).ToList();

            if (!ApiEndpointHelpers.TryParseEnum<AgentPromptGenerationMode>(request.Mode, out var parsedMode))
            {
                errors.Add(new ApiValidationError(
                    nameof(request.Mode),
                    "Mode must be one of: InitialPrompt, ConversationReusable."));
            }

            var initialPrompt = request.InitialPrompt?.Trim();
            var normalizedMessages = NormalizeGenerationMessages(request.ConversationMessages);

            if (parsedMode == AgentPromptGenerationMode.InitialPrompt && string.IsNullOrWhiteSpace(initialPrompt))
            {
                errors.Add(new ApiValidationError(
                    nameof(request.InitialPrompt),
                    "InitialPrompt is required for InitialPrompt mode."));
            }

            if (parsedMode == AgentPromptGenerationMode.ConversationReusable
                && string.IsNullOrWhiteSpace(initialPrompt)
                && normalizedMessages.Count == 0)
            {
                errors.Add(new ApiValidationError(
                    nameof(request.ConversationMessages),
                    "ConversationReusable mode requires conversation messages or an initial prompt."));
            }

            if (errors.Count > 0)
            {
                return ApiValidation.ToValidationResult(httpContext, errors);
            }

            var actor = await ResolveActiveMemberAsync(httpContext, dbContext, cancellationToken);
            if (actor.ErrorResult is not null)
            {
                return actor.ErrorResult;
            }

            if (!generationService.IsConfigured)
            {
                return ApiValidation.ToServiceUnavailableResult(
                    httpContext,
                    "agent_prompt_generation_unavailable",
                    "Prompt generation is not configured. Configure AiWorkflow:PromptGeneration:ModelRouter before retrying.");
            }

            var suggestion = await generationService.GenerateAsync(
                new AgentPromptGenerationInput(
                    parsedMode,
                    initialPrompt,
                    request.IncludePromptText,
                    normalizedMessages),
                cancellationToken);

            if (suggestion is null)
            {
                return ApiValidation.ToServiceUnavailableResult(
                    httpContext,
                    "agent_prompt_generation_failed",
                    "Prompt generation is temporarily unavailable. Please retry shortly.");
            }

            return Results.Ok(new AgentPromptSuggestionDto(
                Mode: parsedMode.ToString(),
                Title: suggestion.Title,
                PromptText: suggestion.PromptText,
                Model: suggestion.Model,
                SourceSummary: suggestion.SourceSummary));
        });

        return group;
    }

    private static IReadOnlyList<AgentPromptGenerationMessage> NormalizeGenerationMessages(
        IReadOnlyList<AgentPromptGenerationMessageRequest>? messages)
    {
        if (messages is null || messages.Count == 0)
        {
            return [];
        }

        var normalized = new List<AgentPromptGenerationMessage>(messages.Count);
        foreach (var message in messages)
        {
            var text = message.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var role = string.IsNullOrWhiteSpace(message.Role)
                ? "user"
                : message.Role.Trim();

            normalized.Add(new AgentPromptGenerationMessage(role, text));
        }

        return normalized
            .TakeLast(20)
            .ToArray();
    }

    private static AgentReusablePromptDto MapPrompt(AgentReusablePrompt prompt)
    {
        return new AgentReusablePromptDto(
            prompt.Id,
            prompt.Title,
            prompt.PromptText,
            prompt.Scope.ToString(),
            prompt.IsFavorite,
            IsEditable: prompt.Scope == AgentPromptScope.User,
            prompt.DisplayOrder,
            prompt.UsageCount,
            prompt.LastUsedAtUtc,
            prompt.CreatedAtUtc,
            prompt.LastModifiedAtUtc,
            prompt.StableKey);
    }

    private static async Task<ActiveMemberContextScope> ResolveActiveMemberAsync(
        HttpContext httpContext,
        MosaicMoneyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var memberScope = await HouseholdMemberContextResolver.ResolveAsync(
            httpContext,
            dbContext,
            householdId: null,
            "The household member is not active and cannot access reusable prompts.",
            cancellationToken);

        if (memberScope.ErrorResult is not null)
        {
            return new ActiveMemberContextScope(null, memberScope.ErrorResult);
        }

        var membership = await dbContext.HouseholdUsers
            .AsNoTracking()
            .Where(x =>
                x.Id == memberScope.HouseholdUserId
                && x.MembershipStatus == HouseholdMembershipStatus.Active)
            .Select(x => new ActiveMemberContext(
                x.Id,
                x.HouseholdId))
            .FirstOrDefaultAsync(cancellationToken);

        if (membership is null)
        {
            return new ActiveMemberContextScope(
                null,
                ApiValidation.ToForbiddenResult(
                    httpContext,
                    "membership_access_denied",
                    "The household member is not active and cannot access reusable prompts."));
        }

        return new ActiveMemberContextScope(membership, null);
    }

    private sealed record ActiveMemberContext(Guid HouseholdUserId, Guid HouseholdId);

    private sealed record ActiveMemberContextScope(ActiveMemberContext? Value, IResult? ErrorResult);
}
