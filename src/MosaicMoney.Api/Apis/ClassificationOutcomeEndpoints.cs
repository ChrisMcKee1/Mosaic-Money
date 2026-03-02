using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using MosaicMoney.Api.Contracts.V1;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.Classification;
using MosaicMoney.Api.Domain.Ledger.Transactions;

namespace MosaicMoney.Api.Apis;

public static class ClassificationOutcomeEndpoints
{
    public static RouteGroupBuilder MapClassificationOutcomeEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/transactions/{transactionId:guid}/classification-outcomes", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            [FromServices] ITransactionAccessQueryService transactionAccessQueryService,
            Guid transactionId,
            CancellationToken cancellationToken) =>
        {
            var accessScope = await AuthenticatedHouseholdScopeResolver.ResolveAsync(
                httpContext,
                dbContext,
                householdId: null,
                "The authenticated household member is not active and cannot access classification outcomes.",
                cancellationToken);
            if (accessScope.ErrorResult is not null)
            {
                return accessScope.ErrorResult;
            }

            var transaction = await transactionAccessQueryService.GetReadableTransactionAsync(
                accessScope.HouseholdUserId,
                transactionId,
                cancellationToken);

            if (transaction is null)
            {
                return ApiValidation.ToNotFoundResult(httpContext, "transaction_not_found", "The requested transaction was not found.");
            }

            var outcomes = await dbContext.TransactionClassificationOutcomes
                .AsNoTracking()
                .Where(x => x.TransactionId == transactionId)
                .Include(x => x.StageOutputs)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ToListAsync(cancellationToken);

            return Results.Ok(outcomes.Select(ApiEndpointHelpers.MapClassificationOutcome).ToList());
        });

        group.MapPost("/transactions/{transactionId:guid}/classification-outcomes/deterministic", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            [FromServices] ITransactionAccessQueryService transactionAccessQueryService,
            [FromServices] IDeterministicClassificationOrchestrator deterministicClassificationOrchestrator,
            Guid transactionId,
            Guid? needsReviewByUserId,
            CancellationToken cancellationToken) =>
        {
            var accessScope = await AuthenticatedHouseholdScopeResolver.ResolveAsync(
                httpContext,
                dbContext,
                householdId: null,
                "The authenticated household member is not active and cannot trigger deterministic classification.",
                cancellationToken);
            if (accessScope.ErrorResult is not null)
            {
                return accessScope.ErrorResult;
            }

            var transaction = await transactionAccessQueryService.GetReadableTransactionAsync(
                accessScope.HouseholdUserId,
                transactionId,
                cancellationToken);
            if (transaction is null)
            {
                return ApiValidation.ToNotFoundResult(httpContext, "transaction_not_found", "The requested transaction was not found.");
            }

            if (needsReviewByUserId.HasValue)
            {
                var reviewerExists = await dbContext.HouseholdUsers
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.Id == needsReviewByUserId.Value
                        && x.HouseholdId == accessScope.HouseholdId
                        && x.MembershipStatus == HouseholdMembershipStatus.Active,
                        cancellationToken);

                if (!reviewerExists)
                {
                    return ApiValidation.ToValidationResult(
                        httpContext,
                        [new ApiValidationError(nameof(needsReviewByUserId), "NeedsReviewByUserId does not exist.")]);
                }
            }

            var executionResult = await deterministicClassificationOrchestrator.ClassifyAndPersistAsync(
                transactionId,
                needsReviewByUserId,
                cancellationToken);

            if (executionResult is null)
            {
                return ApiValidation.ToNotFoundResult(httpContext, "transaction_not_found", "The requested transaction was not found.");
            }

            var persistedOutcome = await dbContext.TransactionClassificationOutcomes
                .AsNoTracking()
                .Include(x => x.StageOutputs)
                .FirstAsync(x => x.Id == executionResult.Outcome.Id, cancellationToken);

            return Results.Created(
                $"/api/v1/transactions/{transactionId}/classification-outcomes/{persistedOutcome.Id}",
                ApiEndpointHelpers.MapClassificationOutcome(persistedOutcome));
        });

        group.MapPost("/transactions/{transactionId:guid}/classification-outcomes/foundry", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            [FromServices] ITransactionAccessQueryService transactionAccessQueryService,
            [FromServices] IFoundryClassificationOrchestrator foundryClassificationOrchestrator,
            Guid transactionId,
            Guid? needsReviewByUserId,
            CancellationToken cancellationToken) =>
        {
            var accessScope = await AuthenticatedHouseholdScopeResolver.ResolveAsync(
                httpContext,
                dbContext,
                householdId: null,
                "The authenticated household member is not active and cannot trigger Foundry classification.",
                cancellationToken);
            if (accessScope.ErrorResult is not null)
            {
                return accessScope.ErrorResult;
            }

            var transaction = await transactionAccessQueryService.GetReadableTransactionAsync(
                accessScope.HouseholdUserId,
                transactionId,
                cancellationToken);
            if (transaction is null)
            {
                return ApiValidation.ToNotFoundResult(httpContext, "transaction_not_found", "The requested transaction was not found.");
            }

            if (needsReviewByUserId.HasValue)
            {
                var reviewerExists = await dbContext.HouseholdUsers
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.Id == needsReviewByUserId.Value
                        && x.HouseholdId == accessScope.HouseholdId
                        && x.MembershipStatus == HouseholdMembershipStatus.Active,
                        cancellationToken);

                if (!reviewerExists)
                {
                    return ApiValidation.ToValidationResult(
                        httpContext,
                        [new ApiValidationError(nameof(needsReviewByUserId), "NeedsReviewByUserId does not exist.")]);
                }
            }

            var executionResult = await foundryClassificationOrchestrator.ClassifyAndPersistAsync(
                transactionId,
                needsReviewByUserId,
                cancellationToken);

            if (executionResult is null)
            {
                return ApiValidation.ToNotFoundResult(httpContext, "transaction_not_found", "The requested transaction was not found.");
            }

            var persistedOutcome = await dbContext.TransactionClassificationOutcomes
                .AsNoTracking()
                .Include(x => x.StageOutputs)
                .FirstAsync(x => x.Id == executionResult.Outcome.Id, cancellationToken);

            return Results.Created(
                $"/api/v1/transactions/{transactionId}/classification-outcomes/{persistedOutcome.Id}",
                ApiEndpointHelpers.MapClassificationOutcome(persistedOutcome));
        });

        group.MapPost("/transactions/{transactionId:guid}/classification-outcomes", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            Guid transactionId,
            CreateClassificationOutcomeRequest request,
            CancellationToken cancellationToken) =>
        {
            var accessScope = await AuthenticatedHouseholdScopeResolver.ResolveAsync(
                httpContext,
                dbContext,
                householdId: null,
                "The authenticated household member is not active and cannot create classification outcomes.",
                cancellationToken);
            if (accessScope.ErrorResult is not null)
            {
                return accessScope.ErrorResult;
            }

            var errors = ApiValidation.ValidateDataAnnotations(request).ToList();

            if (request.StageOutputs.Count == 0)
            {
                errors.Add(new ApiValidationError(nameof(request.StageOutputs), "At least one stage output is required."));
            }

            if (!ApiEndpointHelpers.TryParseEnum<ClassificationDecision>(request.Decision, out var parsedDecision))
            {
                errors.Add(new ApiValidationError(nameof(request.Decision), "Decision must be one of: Categorized, NeedsReview."));
            }

            if (!ApiEndpointHelpers.TryParseEnum<TransactionReviewStatus>(request.ReviewStatus, out var parsedReviewStatus))
            {
                errors.Add(new ApiValidationError(nameof(request.ReviewStatus), "ReviewStatus must be one of: None, NeedsReview, Reviewed."));
            }

            if (parsedDecision == ClassificationDecision.NeedsReview && parsedReviewStatus != TransactionReviewStatus.NeedsReview)
            {
                errors.Add(new ApiValidationError(nameof(request.ReviewStatus), "ReviewStatus must be NeedsReview when Decision is NeedsReview."));
            }

            if (parsedDecision == ClassificationDecision.Categorized && parsedReviewStatus == TransactionReviewStatus.NeedsReview)
            {
                errors.Add(new ApiValidationError(nameof(request.ReviewStatus), "ReviewStatus cannot be NeedsReview when Decision is Categorized."));
            }

            var parsedStageOutputs = new List<(CreateClassificationStageOutputRequest Request, ClassificationStage Stage)>();
            for (var index = 0; index < request.StageOutputs.Count; index++)
            {
                var stageOutput = request.StageOutputs[index];
                errors.AddRange(ApiValidation.ValidateDataAnnotations(stageOutput)
                    .Select(x => x with { Field = $"StageOutputs[{index}].{x.Field}" }));

                if (!ApiEndpointHelpers.TryParseEnum<ClassificationStage>(stageOutput.Stage, out var parsedStage))
                {
                    errors.Add(new ApiValidationError($"StageOutputs[{index}].Stage", "Stage must be one of: Deterministic, Semantic, MafFallback."));
                    continue;
                }

                var expectedStageOrder = ApiEndpointHelpers.GetExpectedStageOrder(parsedStage);
                if (stageOutput.StageOrder != expectedStageOrder)
                {
                    errors.Add(new ApiValidationError($"StageOutputs[{index}].StageOrder", $"StageOrder must be {expectedStageOrder} for stage {parsedStage}."));
                }

                parsedStageOutputs.Add((stageOutput, parsedStage));
            }

            var distinctStageCount = parsedStageOutputs.Select(x => x.Stage).Distinct().Count();
            if (distinctStageCount != parsedStageOutputs.Count)
            {
                errors.Add(new ApiValidationError(nameof(request.StageOutputs), "Each stage can appear at most once per classification outcome."));
            }

            var distinctStageOrderCount = parsedStageOutputs.Select(x => x.Request.StageOrder).Distinct().Count();
            if (distinctStageOrderCount != parsedStageOutputs.Count)
            {
                errors.Add(new ApiValidationError(nameof(request.StageOutputs), "StageOrder values must be unique per classification outcome."));
            }

            if (parsedStageOutputs.Count > 0)
            {
                var maxStageOrder = parsedStageOutputs.Max(x => x.Request.StageOrder);
                if (parsedStageOutputs.Any(x => x.Request.StageOrder < maxStageOrder && !x.Request.EscalatedToNextStage))
                {
                    errors.Add(new ApiValidationError(nameof(request.StageOutputs), "Intermediate stages must set EscalatedToNextStage to true when later stages are present."));
                }

                if (parsedStageOutputs.Any(x => x.Request.StageOrder == maxStageOrder && x.Request.EscalatedToNextStage))
                {
                    errors.Add(new ApiValidationError(nameof(request.StageOutputs), "Final stage output cannot escalate to a next stage."));
                }
            }

            var transaction = await dbContext.EnrichedTransactions
                .AsNoTracking()
                .Where(x => x.Id == transactionId)
                .Select(x => new
                {
                    x.Id,
                    x.Account.HouseholdId,
                })
                .FirstOrDefaultAsync(cancellationToken);
            if (transaction is null || transaction.HouseholdId != accessScope.HouseholdId)
            {
                return ApiValidation.ToNotFoundResult(httpContext, "transaction_not_found", "The requested transaction was not found.");
            }

            var proposedSubcategoryIds = request.StageOutputs
                .Where(x => x.ProposedSubcategoryId.HasValue)
                .Select(x => x.ProposedSubcategoryId!.Value)
                .ToHashSet();

            if (request.ProposedSubcategoryId.HasValue)
            {
                proposedSubcategoryIds.Add(request.ProposedSubcategoryId.Value);
            }

            if (proposedSubcategoryIds.Count > 0)
            {
                var existingSubcategoryIds = await dbContext.Subcategories
                    .AsNoTracking()
                    .Include(x => x.Category)
                    .Where(x => proposedSubcategoryIds.Contains(x.Id))
                    .Where(x =>
                        x.Category.OwnerType == CategoryOwnerType.Platform
                        || (x.Category.OwnerType == CategoryOwnerType.HouseholdShared
                            && x.Category.HouseholdId == accessScope.HouseholdId)
                        || (x.Category.OwnerType == CategoryOwnerType.User
                            && x.Category.HouseholdId == accessScope.HouseholdId
                            && x.Category.OwnerUserId == accessScope.HouseholdUserId))
                    .Select(x => x.Id)
                    .ToListAsync(cancellationToken);

                var missingIds = proposedSubcategoryIds.Except(existingSubcategoryIds).ToList();
                if (missingIds.Count > 0)
                {
                    errors.Add(new ApiValidationError(nameof(request.ProposedSubcategoryId), "One or more proposed subcategory references do not exist."));
                }
            }

            if (errors.Count > 0)
            {
                return ApiValidation.ToValidationResult(httpContext, errors);
            }

            var now = DateTime.UtcNow;
            var outcome = new TransactionClassificationOutcome
            {
                Id = Guid.NewGuid(),
                TransactionId = transactionId,
                ProposedSubcategoryId = request.ProposedSubcategoryId,
                FinalConfidence = decimal.Round(request.FinalConfidence, 4),
                Decision = parsedDecision,
                ReviewStatus = parsedReviewStatus,
                DecisionReasonCode = request.DecisionReasonCode.Trim(),
                DecisionRationale = request.DecisionRationale.Trim(),
                AgentNoteSummary = AgentNoteSummaryPolicy.Sanitize(request.AgentNoteSummary),
                IsAiAssigned = false,
                AssignmentSource = "human_manual",
                AssignedByAgent = null,
                CreatedAtUtc = now,
            };

            foreach (var parsedStageOutput in parsedStageOutputs.OrderBy(x => x.Request.StageOrder))
            {
                outcome.StageOutputs.Add(new ClassificationStageOutput
                {
                    Id = Guid.NewGuid(),
                    Stage = parsedStageOutput.Stage,
                    StageOrder = parsedStageOutput.Request.StageOrder,
                    ProposedSubcategoryId = parsedStageOutput.Request.ProposedSubcategoryId,
                    Confidence = decimal.Round(parsedStageOutput.Request.Confidence, 4),
                    RationaleCode = parsedStageOutput.Request.RationaleCode.Trim(),
                    Rationale = parsedStageOutput.Request.Rationale.Trim(),
                    EscalatedToNextStage = parsedStageOutput.Request.EscalatedToNextStage,
                    ProducedAtUtc = now,
                });
            }

            dbContext.TransactionClassificationOutcomes.Add(outcome);
            await dbContext.SaveChangesAsync(cancellationToken);

            var persistedOutcome = await dbContext.TransactionClassificationOutcomes
                .AsNoTracking()
                .Include(x => x.StageOutputs)
                .FirstAsync(x => x.Id == outcome.Id);

            return Results.Created(
                $"/api/v1/transactions/{transactionId}/classification-outcomes/{outcome.Id}",
                ApiEndpointHelpers.MapClassificationOutcome(persistedOutcome));
        });

        return group;
    }
}
