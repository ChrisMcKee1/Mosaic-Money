using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Contracts.V1;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.Embeddings;

namespace MosaicMoney.Api.Apis;

public static class TransactionsEndpoints
{
    public static RouteGroupBuilder MapTransactionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/transactions", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            Guid? accountId,
            DateOnly? fromDate,
            DateOnly? toDate,
            string? reviewStatus,
            bool? needsReviewOnly,
            int page = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default) =>
        {
            var errors = ValidateTransactionQuery(page, pageSize, fromDate, toDate, reviewStatus, out var reviewStatusFilter);
            if (errors.Count > 0)
            {
                return ApiValidation.ToValidationResult(httpContext, errors);
            }

            var accessScope = await HouseholdMemberContextResolver.ResolveAsync(
                httpContext,
                dbContext,
                householdId: null,
                "The household member is not active and cannot access account or transaction data.",
                cancellationToken);
            if (accessScope.ErrorResult is not null)
            {
                return accessScope.ErrorResult;
            }

            var readableAccountIds = BuildReadableAccountIdsQuery(dbContext, accessScope.HouseholdUserId);
            if (accountId.HasValue
                && !await readableAccountIds.AnyAsync(x => x == accountId.Value, cancellationToken))
            {
                return ApiValidation.ToForbiddenResult(
                    httpContext,
                    "account_access_denied",
                    "The authenticated household member does not have access to the requested account.");
            }

            var query = dbContext.EnrichedTransactions
                .AsNoTracking()
                .Include(x => x.Splits)
                .Where(x => readableAccountIds.Contains(x.AccountId))
                .AsQueryable();

            if (accountId.HasValue)
            {
                query = query.Where(x => x.AccountId == accountId.Value);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(x => x.TransactionDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(x => x.TransactionDate <= toDate.Value);
            }

            if (reviewStatusFilter.HasValue)
            {
                query = query.Where(x => x.ReviewStatus == reviewStatusFilter.Value);
            }

            if (needsReviewOnly == true)
            {
                query = query.Where(x => x.ReviewStatus == TransactionReviewStatus.NeedsReview);
            }

            var transactions = await query
                .OrderByDescending(x => x.TransactionDate)
                .ThenByDescending(x => x.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return Results.Ok(transactions.Select(ApiEndpointHelpers.MapTransaction).ToList());
        });

        group.MapGet("/transactions/projection-metadata", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            TransactionProjectionMetadataQueryService queryService,
            Guid? accountId,
            DateOnly? fromDate,
            DateOnly? toDate,
            string? reviewStatus,
            bool? needsReviewOnly,
            int page = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default) =>
        {
            var errors = ValidateTransactionQuery(page, pageSize, fromDate, toDate, reviewStatus, out var reviewStatusFilter);
            if (errors.Count > 0)
            {
                return ApiValidation.ToValidationResult(httpContext, errors);
            }

            var accessScope = await HouseholdMemberContextResolver.ResolveAsync(
                httpContext,
                dbContext,
                householdId: null,
                "The household member is not active and cannot access account or transaction data.",
                cancellationToken);
            if (accessScope.ErrorResult is not null)
            {
                return accessScope.ErrorResult;
            }

            var readableAccountIds = BuildReadableAccountIdsQuery(dbContext, accessScope.HouseholdUserId);
            if (accountId.HasValue
                && !await readableAccountIds.AnyAsync(x => x == accountId.Value, cancellationToken))
            {
                return ApiValidation.ToForbiddenResult(
                    httpContext,
                    "account_access_denied",
                    "The authenticated household member does not have access to the requested account.");
            }

            var projectionMetadata = await queryService.QueryAsync(
                householdUserId: accessScope.HouseholdUserId,
                accountId,
                fromDate,
                toDate,
                reviewStatusFilter,
                needsReviewOnly == true,
                page,
                pageSize,
                cancellationToken);

            return Results.Ok(projectionMetadata);
        });

        group.MapGet("/transactions/{id:guid}", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            Guid id,
            CancellationToken cancellationToken = default) =>
        {
            var accessScope = await HouseholdMemberContextResolver.ResolveAsync(
                httpContext,
                dbContext,
                householdId: null,
                "The household member is not active and cannot access account or transaction data.",
                cancellationToken);
            if (accessScope.ErrorResult is not null)
            {
                return accessScope.ErrorResult;
            }

            var readableAccountIds = BuildReadableAccountIdsQuery(dbContext, accessScope.HouseholdUserId);

            var transaction = await dbContext.EnrichedTransactions
                .AsNoTracking()
                .Include(x => x.Splits)
                .Where(x => readableAccountIds.Contains(x.AccountId))
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (transaction is null)
            {
                return ApiValidation.ToNotFoundResult(httpContext, "transaction_not_found", "The requested transaction was not found.");
            }

            return Results.Ok(ApiEndpointHelpers.MapTransaction(transaction));
        });

        group.MapPost("/transactions", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            ITransactionEmbeddingQueueService embeddingQueueService,
            ILoggerFactory loggerFactory,
            CreateTransactionRequest request,
            CancellationToken cancellationToken = default) =>
        {
            var logger = loggerFactory.CreateLogger("MosaicMoney.Api.Transactions");
            var errors = ApiValidation.ValidateDataAnnotations(request).ToList();

            if (request.Amount == 0)
            {
                errors.Add(new ApiValidationError(nameof(request.Amount), "Amount must be a non-zero signed value."));
            }

            if (request.TransactionDate == default)
            {
                errors.Add(new ApiValidationError(nameof(request.TransactionDate), "TransactionDate is required."));
            }

            if (string.IsNullOrWhiteSpace(request.Description))
            {
                errors.Add(new ApiValidationError(nameof(request.Description), "Description is required."));
            }

            if (!ApiEndpointHelpers.TryParseEnum<TransactionReviewStatus>(request.ReviewStatus, out var parsedReviewStatus))
            {
                errors.Add(new ApiValidationError(nameof(request.ReviewStatus), "ReviewStatus must be one of: None, NeedsReview, Reviewed."));
            }

            for (var index = 0; index < request.Splits.Count; index++)
            {
                var split = request.Splits[index];
                errors.AddRange(ApiValidation.ValidateDataAnnotations(split)
                    .Select(x => x with { Field = $"Splits[{index}].{x.Field}" }));

                if (split.Amount == 0)
                {
                    errors.Add(new ApiValidationError($"Splits[{index}].Amount", "Split amount must be non-zero."));
                }
            }

            if (request.Splits.Count > 0)
            {
                var splitTotal = request.Splits.Sum(x => x.Amount);
                if (decimal.Round(splitTotal, 2) != decimal.Round(request.Amount, 2))
                {
                    errors.Add(new ApiValidationError(nameof(request.Splits), "Split amounts must sum to the transaction amount to preserve single-entry truth."));
                }
            }

            if (parsedReviewStatus == TransactionReviewStatus.NeedsReview)
            {
                if (request.NeedsReviewByUserId is null)
                {
                    errors.Add(new ApiValidationError(nameof(request.NeedsReviewByUserId), "NeedsReviewByUserId is required when ReviewStatus is NeedsReview."));
                }

                if (string.IsNullOrWhiteSpace(request.ReviewReason))
                {
                    errors.Add(new ApiValidationError(nameof(request.ReviewReason), "ReviewReason is required when ReviewStatus is NeedsReview."));
                }
            }

            if (errors.Count > 0)
            {
                return ApiValidation.ToValidationResult(httpContext, errors);
            }

            var accountExists = await dbContext.Accounts.AnyAsync(x => x.Id == request.AccountId);
            if (!accountExists)
            {
                return ApiValidation.ToValidationResult(httpContext, [new ApiValidationError(nameof(request.AccountId), "AccountId does not exist.")]);
            }

            if (request.RecurringItemId.HasValue && !await dbContext.RecurringItems.AnyAsync(x => x.Id == request.RecurringItemId.Value))
            {
                return ApiValidation.ToValidationResult(httpContext, [new ApiValidationError(nameof(request.RecurringItemId), "RecurringItemId does not exist.")]);
            }

            if (request.SubcategoryId.HasValue && !await dbContext.Subcategories.AnyAsync(x => x.Id == request.SubcategoryId.Value))
            {
                return ApiValidation.ToValidationResult(httpContext, [new ApiValidationError(nameof(request.SubcategoryId), "SubcategoryId does not exist.")]);
            }

            if (request.NeedsReviewByUserId.HasValue && !await dbContext.HouseholdUsers.AnyAsync(x => x.Id == request.NeedsReviewByUserId.Value))
            {
                return ApiValidation.ToValidationResult(httpContext, [new ApiValidationError(nameof(request.NeedsReviewByUserId), "NeedsReviewByUserId does not exist.")]);
            }

            var transaction = new EnrichedTransaction
            {
                Id = Guid.NewGuid(),
                AccountId = request.AccountId,
                RecurringItemId = request.RecurringItemId,
                SubcategoryId = request.SubcategoryId,
                NeedsReviewByUserId = request.NeedsReviewByUserId,
                PlaidTransactionId = request.PlaidTransactionId,
                Description = request.Description.Trim(),
                Amount = decimal.Round(request.Amount, 2),
                TransactionDate = request.TransactionDate,
                ReviewStatus = parsedReviewStatus,
                ReviewReason = request.ReviewReason,
                ExcludeFromBudget = request.ExcludeFromBudget,
                IsExtraPrincipal = request.IsExtraPrincipal,
                UserNote = request.UserNote,
                AgentNote = request.AgentNote,
                CreatedAtUtc = DateTime.UtcNow,
                LastModifiedAtUtc = DateTime.UtcNow,
            };

            foreach (var split in request.Splits)
            {
                transaction.Splits.Add(new TransactionSplit
                {
                    Id = Guid.NewGuid(),
                    Amount = decimal.Round(split.Amount, 2),
                    SubcategoryId = split.SubcategoryId,
                    AmortizationMonths = split.AmortizationMonths,
                    UserNote = split.UserNote,
                    AgentNote = split.AgentNote,
                });
            }

            dbContext.EnrichedTransactions.Add(transaction);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException) when (!string.IsNullOrWhiteSpace(request.PlaidTransactionId))
            {
                return ApiValidation.ToConflictResult(httpContext, "idempotency_conflict", "A transaction with the same PlaidTransactionId already exists.");
            }

            try
            {
                await embeddingQueueService.EnqueueTransactionsAsync([transaction.Id], cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogError(
                    ex,
                    "Transaction {TransactionId} was created but enqueueing semantic embeddings failed.",
                    transaction.Id);
            }

            var response = await dbContext.EnrichedTransactions
                .AsNoTracking()
                .Include(x => x.Splits)
                .FirstAsync(x => x.Id == transaction.Id, cancellationToken);

            return Results.Created($"/api/v1/transactions/{transaction.Id}", ApiEndpointHelpers.MapTransaction(response));
        });

        return group;
    }

    private static List<ApiValidationError> ValidateTransactionQuery(
        int page,
        int pageSize,
        DateOnly? fromDate,
        DateOnly? toDate,
        string? reviewStatus,
        out TransactionReviewStatus? reviewStatusFilter)
    {
        var errors = new List<ApiValidationError>();

        if (page < 1)
        {
            errors.Add(new ApiValidationError(nameof(page), "Page must be greater than or equal to 1."));
        }

        if (pageSize is < 1 or > 200)
        {
            errors.Add(new ApiValidationError(nameof(pageSize), "PageSize must be between 1 and 200."));
        }

        if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
        {
            errors.Add(new ApiValidationError(nameof(fromDate), "fromDate must be less than or equal to toDate."));
        }

        reviewStatusFilter = null;
        if (!string.IsNullOrWhiteSpace(reviewStatus))
        {
            if (!ApiEndpointHelpers.TryParseEnum<TransactionReviewStatus>(reviewStatus, out var parsedReviewStatus))
            {
                errors.Add(new ApiValidationError(nameof(reviewStatus), "reviewStatus must be one of: None, NeedsReview, Reviewed."));
            }
            else
            {
                reviewStatusFilter = parsedReviewStatus;
            }
        }

        return errors;
    }

    private static IQueryable<Guid> BuildReadableAccountIdsQuery(MosaicMoneyDbContext dbContext, Guid householdUserId)
    {
        return dbContext.AccountMemberAccessEntries
            .AsNoTracking()
            .Where(x =>
                x.HouseholdUserId == householdUserId
                && x.Visibility == AccountAccessVisibility.Visible
                && x.AccessRole != AccountAccessRole.None
                && x.HouseholdUser.MembershipStatus == HouseholdMembershipStatus.Active)
            .Select(x => x.AccountId);
    }

}
