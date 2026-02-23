using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Contracts.V1;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.Classification;

namespace MosaicMoney.Api.Apis;

public static class ClassificationOutcomeEndpoints
{
    public static RouteGroupBuilder MapClassificationOutcomeEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/transactions/{transactionId:guid}/classification-outcomes", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            Guid transactionId) =>
        {
            var transactionExists = await dbContext.EnrichedTransactions
                .AsNoTracking()
                .AnyAsync(x => x.Id == transactionId);

            if (!transactionExists)
            {
                return ApiValidation.ToNotFoundResult(httpContext, "transaction_not_found", "The requested transaction was not found.");
            }

            var outcomes = await dbContext.TransactionClassificationOutcomes
                .AsNoTracking()
                .Where(x => x.TransactionId == transactionId)
                .Include(x => x.StageOutputs)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ToListAsync();

            return Results.Ok(outcomes.Select(ApiEndpointHelpers.MapClassificationOutcome).ToList());
        });

        group.MapPost("/transactions/{transactionId:guid}/classification-outcomes/deterministic", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            IDeterministicClassificationOrchestrator deterministicClassificationOrchestrator,
            Guid transactionId,
            Guid? needsReviewByUserId,
            CancellationToken cancellationToken) =>
        {
            if (needsReviewByUserId.HasValue)
            {
                var reviewerExists = await dbContext.HouseholdUsers
                    .AsNoTracking()
                    .AnyAsync(x => x.Id == needsReviewByUserId.Value, cancellationToken);

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

        group.MapPost("/transactions/{transactionId:guid}/classification-outcomes", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            Guid transactionId,
            CreateClassificationOutcomeRequest request) =>
        {
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

            var transactionExists = await dbContext.EnrichedTransactions
                .AsNoTracking()
                .AnyAsync(x => x.Id == transactionId);
            if (!transactionExists)
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
                    .Where(x => proposedSubcategoryIds.Contains(x.Id))
                    .Select(x => x.Id)
                    .ToListAsync();

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
                AgentNoteSummary = string.IsNullOrWhiteSpace(request.AgentNoteSummary)
                    ? null
                    : request.AgentNoteSummary.Trim(),
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
            await dbContext.SaveChangesAsync();

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
