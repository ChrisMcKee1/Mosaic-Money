using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Contracts.V1;
using MosaicMoney.Api.Data;

namespace MosaicMoney.Api.Domain.Ledger;

public sealed class TransactionProjectionMetadataQueryService
{
    private readonly MosaicMoneyDbContext _dbContext;

    private sealed record ReimbursementProposalRow(
        Guid IncomingTransactionId,
        ReimbursementProposalStatus Status,
        string StatusReasonCode,
        decimal ProposedAmount,
        DateTime CreatedAtUtc,
        int LifecycleOrdinal,
        Guid Id);

    public TransactionProjectionMetadataQueryService(MosaicMoneyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<TransactionProjectionMetadataDto>> QueryAsync(
        Guid householdUserId,
        Guid? accountId,
        DateOnly? fromDate,
        DateOnly? toDate,
        TransactionReviewStatus? reviewStatus,
        bool needsReviewOnly,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var readableAccountIds = _dbContext.AccountMemberAccessEntries
            .AsNoTracking()
            .Where(x =>
                x.HouseholdUserId == householdUserId
                && x.Visibility == AccountAccessVisibility.Visible
                && x.AccessRole != AccountAccessRole.None
                && x.HouseholdUser.MembershipStatus == HouseholdMembershipStatus.Active)
            .Select(x => x.AccountId);

        var query = _dbContext.EnrichedTransactions
            .AsNoTracking()
            .Include(x => x.Splits)
            .Include(x => x.RecurringItem)
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

        if (reviewStatus.HasValue)
        {
            query = query.Where(x => x.ReviewStatus == reviewStatus.Value);
        }

        if (needsReviewOnly)
        {
            query = query.Where(x => x.ReviewStatus == TransactionReviewStatus.NeedsReview);
        }

        var transactions = await query
            .OrderByDescending(x => x.TransactionDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var transactionIds = transactions.Select(x => x.Id).ToList();

        var proposalRows = transactionIds.Count == 0
            ? new List<ReimbursementProposalRow>()
            : await _dbContext.ReimbursementProposals
                .AsNoTracking()
                .Where(x => transactionIds.Contains(x.IncomingTransactionId))
                .Select(x => new ReimbursementProposalRow(
                    x.IncomingTransactionId,
                    x.Status,
                    x.StatusReasonCode,
                    x.ProposedAmount,
                    x.CreatedAtUtc,
                    x.LifecycleOrdinal,
                    x.Id))
                .ToListAsync(cancellationToken);

        var proposalMetadataByTransactionId = proposalRows
            .GroupBy(x => x.IncomingTransactionId)
            .ToDictionary(
                x => x.Key,
                x => BuildReimbursementMetadata(x.ToList()));

        return transactions
            .Select(transaction =>
            {
                proposalMetadataByTransactionId.TryGetValue(transaction.Id, out var reimbursementMetadata);
                reimbursementMetadata ??= new ReimbursementProjectionMetadataDto(
                    HasProposals: false,
                    ProposalCount: 0,
                    HasPendingHumanReview: false,
                    LatestStatus: null,
                    LatestStatusReasonCode: null,
                    PendingOrNeedsReviewAmount: 0m,
                    ApprovedAmount: 0m);

                return new TransactionProjectionMetadataDto(
                    Id: transaction.Id,
                    AccountId: transaction.AccountId,
                    Description: transaction.Description,
                    RawAmount: transaction.Amount,
                    RawTransactionDate: transaction.TransactionDate,
                    ReviewStatus: transaction.ReviewStatus.ToString(),
                    ReviewReason: transaction.ReviewReason,
                    ExcludeFromBudget: transaction.ExcludeFromBudget,
                    IsExtraPrincipal: transaction.IsExtraPrincipal,
                    Recurring: new RecurringProjectionMetadataDto(
                        IsLinked: transaction.RecurringItemId.HasValue,
                        RecurringItemId: transaction.RecurringItemId,
                        IsActive: transaction.RecurringItem?.IsActive,
                        Frequency: transaction.RecurringItem?.Frequency.ToString(),
                        NextDueDate: transaction.RecurringItem?.NextDueDate),
                    Reimbursement: reimbursementMetadata,
                    Splits: transaction.Splits
                        .OrderBy(x => x.Id)
                        .Select(x => new TransactionSplitProjectionMetadataDto(
                            Id: x.Id,
                            SubcategoryId: x.SubcategoryId,
                            RawAmount: x.Amount,
                            AmortizationMonths: x.AmortizationMonths))
                        .ToList(),
                    CreatedAtUtc: transaction.CreatedAtUtc,
                    LastModifiedAtUtc: transaction.LastModifiedAtUtc);
            })
            .ToList();
    }

    private static ReimbursementProjectionMetadataDto BuildReimbursementMetadata(IReadOnlyList<ReimbursementProposalRow> rows)
    {
        if (rows.Count == 0)
        {
            return new ReimbursementProjectionMetadataDto(
                HasProposals: false,
                ProposalCount: 0,
                HasPendingHumanReview: false,
                LatestStatus: null,
                LatestStatusReasonCode: null,
                PendingOrNeedsReviewAmount: 0m,
                ApprovedAmount: 0m);
        }

        var latest = rows
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.LifecycleOrdinal)
            .ThenByDescending(x => x.Id)
            .First();

        var pendingOrNeedsReviewAmount = rows
            .Where(x => x.Status is ReimbursementProposalStatus.PendingApproval or ReimbursementProposalStatus.NeedsReview)
            .Sum(x => (decimal)x.ProposedAmount);

        var approvedAmount = rows
            .Where(x => x.Status == ReimbursementProposalStatus.Approved)
            .Sum(x => (decimal)x.ProposedAmount);

        return new ReimbursementProjectionMetadataDto(
            HasProposals: true,
            ProposalCount: rows.Count,
            HasPendingHumanReview: rows.Any(x => x.Status is ReimbursementProposalStatus.PendingApproval or ReimbursementProposalStatus.NeedsReview),
            LatestStatus: latest.Status.ToString(),
            LatestStatusReasonCode: latest.StatusReasonCode,
            PendingOrNeedsReviewAmount: pendingOrNeedsReviewAmount,
            ApprovedAmount: approvedAmount);
    }
}
