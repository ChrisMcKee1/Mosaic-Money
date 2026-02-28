using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Contracts.V1;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.Embeddings;

namespace MosaicMoney.Api.Apis;

public static class ReviewActionEndpoints
{
    public static RouteGroupBuilder MapReviewActionEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/review-actions", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            ITransactionEmbeddingQueueService embeddingQueueService,
            ILoggerFactory loggerFactory,
            ReviewActionRequest request,
            CancellationToken cancellationToken = default) =>
        {
            var logger = loggerFactory.CreateLogger("MosaicMoney.Api.ReviewActions");
            var errors = ApiValidation.ValidateDataAnnotations(request).ToList();
            var actionValue = request.Action?.Trim() ?? string.Empty;
            var parsedAction = default(TransactionReviewAction);

            if (!TransactionReviewStateMachine.TryParseAction(actionValue, out parsedAction))
            {
                errors.Add(new ApiValidationError(nameof(request.Action), "Action must be one of: approve, reclassify, route_to_needs_review."));
            }

            if (parsedAction == TransactionReviewAction.Reclassify && request.SubcategoryId is null)
            {
                errors.Add(new ApiValidationError(nameof(request.SubcategoryId), "SubcategoryId is required for reclassify action."));
            }

            if (parsedAction == TransactionReviewAction.RouteToNeedsReview)
            {
                if (request.NeedsReviewByUserId is null)
                {
                    errors.Add(new ApiValidationError(nameof(request.NeedsReviewByUserId), "NeedsReviewByUserId is required for route_to_needs_review action."));
                }

                if (string.IsNullOrWhiteSpace(request.ReviewReason))
                {
                    errors.Add(new ApiValidationError(nameof(request.ReviewReason), "ReviewReason is required for route_to_needs_review action."));
                }
            }

            if (errors.Count > 0)
            {
                return ApiValidation.ToValidationResult(httpContext, errors);
            }

            var transaction = await dbContext.EnrichedTransactions
                .Include(x => x.Splits)
                .FirstOrDefaultAsync(x => x.Id == request.TransactionId);

            if (transaction is null)
            {
                return ApiValidation.ToNotFoundResult(httpContext, "transaction_not_found", "The transaction for the requested review action was not found.");
            }

            if (request.SubcategoryId.HasValue && !await dbContext.Subcategories.AnyAsync(x => x.Id == request.SubcategoryId.Value))
            {
                return ApiValidation.ToValidationResult(httpContext, [new ApiValidationError(nameof(request.SubcategoryId), "SubcategoryId does not exist.")]);
            }

            if (request.NeedsReviewByUserId.HasValue && !await dbContext.HouseholdUsers.AnyAsync(x => x.Id == request.NeedsReviewByUserId.Value))
            {
                return ApiValidation.ToValidationResult(httpContext, [new ApiValidationError(nameof(request.NeedsReviewByUserId), "NeedsReviewByUserId does not exist.")]);
            }

            if (!TransactionReviewStateMachine.TryTransition(transaction.ReviewStatus, parsedAction, out var targetStatus))
            {
                return ApiValidation.ToConflictResult(
                    httpContext,
                    "invalid_review_transition",
                    $"Action '{actionValue}' is not allowed when ReviewStatus is '{transaction.ReviewStatus}'.");
            }

            switch (parsedAction)
            {
                case TransactionReviewAction.Approve:
                    transaction.ReviewStatus = targetStatus;
                    transaction.ReviewReason = null;
                    transaction.NeedsReviewByUserId = null;
                    break;
                case TransactionReviewAction.Reclassify:
                    transaction.SubcategoryId = request.SubcategoryId;
                    transaction.ReviewStatus = targetStatus;
                    transaction.ReviewReason = request.ReviewReason;
                    transaction.NeedsReviewByUserId = null;
                    break;
                case TransactionReviewAction.RouteToNeedsReview:
                    transaction.ReviewStatus = targetStatus;
                    transaction.ReviewReason = request.ReviewReason;
                    transaction.NeedsReviewByUserId = request.NeedsReviewByUserId;
                    break;
            }

            if (request.ExcludeFromBudget.HasValue)
            {
                transaction.ExcludeFromBudget = request.ExcludeFromBudget.Value;
            }

            if (request.IsExtraPrincipal.HasValue)
            {
                transaction.IsExtraPrincipal = request.IsExtraPrincipal.Value;
            }

            transaction.UserNote = request.UserNote ?? transaction.UserNote;
            transaction.AgentNote = request.AgentNote ?? transaction.AgentNote;
            transaction.LastModifiedAtUtc = DateTime.UtcNow;

            await dbContext.SaveChangesAsync(cancellationToken);

            try
            {
                await embeddingQueueService.EnqueueTransactionsAsync([transaction.Id], cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogError(
                    ex,
                    "Review action saved for transaction {TransactionId}, but semantic embedding enqueue failed.",
                    transaction.Id);
            }

            return Results.Ok(ApiEndpointHelpers.MapTransaction(transaction));
        });

        return group;
    }
}
