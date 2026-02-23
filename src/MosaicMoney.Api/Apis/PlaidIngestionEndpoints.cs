using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Contracts.V1;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger.Embeddings;
using MosaicMoney.Api.Domain.Ledger.Ingestion;

namespace MosaicMoney.Api.Apis;

public static class PlaidIngestionEndpoints
{
    public static RouteGroupBuilder MapPlaidIngestionEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/ingestion/plaid-delta", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            PlaidDeltaIngestionService ingestionService,
            ITransactionEmbeddingQueueService embeddingQueueService,
            ILoggerFactory loggerFactory,
            IngestPlaidDeltaRequest request,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("MosaicMoney.Api.PlaidIngestion");
            var errors = ApiValidation.ValidateDataAnnotations(request).ToList();

            if (request.Transactions.Count == 0)
            {
                errors.Add(new ApiValidationError(nameof(request.Transactions), "At least one transaction payload is required."));
            }

            for (var index = 0; index < request.Transactions.Count; index++)
            {
                var item = request.Transactions[index];
                errors.AddRange(ApiValidation.ValidateDataAnnotations(item)
                    .Select(x => x with { Field = $"Transactions[{index}].{x.Field}" }));

                if (item.Amount == 0)
                {
                    errors.Add(new ApiValidationError($"Transactions[{index}].Amount", "Amount must be a non-zero signed value to preserve single-entry semantics."));
                }

                if (item.TransactionDate == default)
                {
                    errors.Add(new ApiValidationError($"Transactions[{index}].TransactionDate", "TransactionDate is required."));
                }

                if (string.IsNullOrWhiteSpace(item.Description))
                {
                    errors.Add(new ApiValidationError($"Transactions[{index}].Description", "Description is required."));
                }

                if (string.IsNullOrWhiteSpace(item.RawPayloadJson))
                {
                    errors.Add(new ApiValidationError($"Transactions[{index}].RawPayloadJson", "RawPayloadJson is required."));
                }
            }

            if (errors.Count > 0)
            {
                return ApiValidation.ToValidationResult(httpContext, errors);
            }

            var accountExists = await dbContext.Accounts
                .AsNoTracking()
                .AnyAsync(x => x.Id == request.AccountId, cancellationToken);
            if (!accountExists)
            {
                return ApiValidation.ToValidationResult(httpContext, [new ApiValidationError(nameof(request.AccountId), "AccountId does not exist.")]);
            }

            var ingestionRequest = new PlaidDeltaIngestionRequest(
                request.AccountId,
                request.DeltaCursor,
                request.Transactions
                    .Select(x => new PlaidDeltaIngestionItemInput(
                        x.PlaidTransactionId,
                        x.Description,
                        x.Amount,
                        x.TransactionDate,
                        x.RawPayloadJson,
                        x.IsAmbiguous,
                        x.ReviewReason))
                    .ToList());

            var result = await ingestionService.IngestAsync(ingestionRequest, cancellationToken);

            try
            {
                var transactionIds = result.Items
                    .Select(x => x.EnrichedTransactionId)
                    .Distinct()
                    .ToArray();

                if (transactionIds.Length > 0)
                {
                    await embeddingQueueService.EnqueueTransactionsAsync(transactionIds, cancellationToken);
                }
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // Ingestion/write paths must remain non-blocking for embedding generation failures.
                logger.LogError(
                    ex,
                    "Plaid ingestion succeeded but embedding enqueue failed for account {AccountId} and cursor {DeltaCursor}.",
                    request.AccountId,
                    request.DeltaCursor);
            }

            return Results.Ok(ApiEndpointHelpers.MapPlaidDeltaIngestionResult(result));
        });

        return group;
    }
}
