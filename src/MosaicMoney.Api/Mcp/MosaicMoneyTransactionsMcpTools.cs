using System.ComponentModel;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using MosaicMoney.Api.Apis;
using MosaicMoney.Api.Contracts.V1;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.Transactions;

namespace MosaicMoney.Api.Mcp;

[Authorize]
[McpServerToolType]
public sealed class MosaicMoneyTransactionsMcpTools(
    IMcpAuthenticatedContextAccessor contextAccessor,
    ITransactionAccessQueryService transactionAccessQueryService)
{
    [McpServerTool, Description("Lists transactions visible to the authenticated user context. Results are always scoped to the caller's authorized account access.")]
    public async Task<IReadOnlyList<TransactionDto>> ListTransactionsAsync(
        [Description("Optional account identifier. Must be readable by the authenticated user.")] Guid? accountId = null,
        [Description("Optional start date in yyyy-MM-dd format.")] string? fromDate = null,
        [Description("Optional end date in yyyy-MM-dd format.")] string? toDate = null,
        [Description("Optional review status filter: None, NeedsReview, Reviewed.")] string? reviewStatus = null,
        [Description("If true, return only transactions in NeedsReview status.")] bool needsReviewOnly = false,
        [Description("Page number (1-based).") ] int page = 1,
        [Description("Page size (1-200).") ] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            throw new InvalidOperationException("Page must be greater than or equal to 1.");
        }

        if (pageSize is < 1 or > 200)
        {
            throw new InvalidOperationException("PageSize must be between 1 and 200.");
        }

        var parsedFromDate = ParseDateOnlyOrNull(fromDate, nameof(fromDate));
        var parsedToDate = ParseDateOnlyOrNull(toDate, nameof(toDate));

        if (parsedFromDate.HasValue && parsedToDate.HasValue && parsedFromDate.Value > parsedToDate.Value)
        {
            throw new InvalidOperationException("fromDate must be less than or equal to toDate.");
        }

        TransactionReviewStatus? parsedReviewStatus = null;
        if (!string.IsNullOrWhiteSpace(reviewStatus))
        {
            if (!ApiEndpointHelpers.TryParseEnum<TransactionReviewStatus>(reviewStatus, out var parsed))
            {
                throw new InvalidOperationException("reviewStatus must be one of: None, NeedsReview, Reviewed.");
            }

            parsedReviewStatus = parsed;
        }

        var householdUserId = await contextAccessor.GetRequiredHouseholdUserIdAsync(
            householdId: null,
            cancellationToken);

        if (accountId.HasValue && !await transactionAccessQueryService.CanReadAccountAsync(householdUserId, accountId.Value, cancellationToken))
        {
            throw new UnauthorizedAccessException("The authenticated user does not have access to the requested account.");
        }

        var transactions = await transactionAccessQueryService.ListReadableTransactionsAsync(
            householdUserId,
            new TransactionReadQuery(
                accountId,
                parsedFromDate,
                parsedToDate,
                parsedReviewStatus,
                needsReviewOnly,
                page,
                pageSize),
            cancellationToken);

        var latestOutcomes = await transactionAccessQueryService.QueryLatestClassificationOutcomeByTransactionIdAsync(
            transactions.Select(x => x.Id),
            cancellationToken);

        return transactions
            .Select(x =>
            {
                var latestOutcome = latestOutcomes.GetValueOrDefault(x.Id);
                return ApiEndpointHelpers.MapTransaction(
                    x,
                    latestOutcome is null ? null : ApiEndpointHelpers.MapClassificationProvenance(latestOutcome));
            })
            .ToList();
    }

    [McpServerTool, Description("Gets a single transaction visible to the authenticated user context.")]
    public async Task<TransactionDto> GetTransactionAsync(
        [Description("Transaction identifier.")] Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        var householdUserId = await contextAccessor.GetRequiredHouseholdUserIdAsync(
            householdId: null,
            cancellationToken);

        var transaction = await transactionAccessQueryService.GetReadableTransactionAsync(
            householdUserId,
            transactionId,
            cancellationToken)
            ?? throw new UnauthorizedAccessException("Transaction not found or not accessible for the authenticated user.");

        var latestOutcomes = await transactionAccessQueryService.QueryLatestClassificationOutcomeByTransactionIdAsync(
            [transaction.Id],
            cancellationToken);

        var latestOutcome = latestOutcomes.GetValueOrDefault(transaction.Id);

        return ApiEndpointHelpers.MapTransaction(
            transaction,
            latestOutcome is null ? null : ApiEndpointHelpers.MapClassificationProvenance(latestOutcome));
    }

    private static DateOnly? ParseDateOnlyOrNull(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!DateOnly.TryParse(value, out var parsedDate))
        {
            throw new InvalidOperationException($"{parameterName} must be a valid yyyy-MM-dd date.");
        }

        return parsedDate;
    }
}
