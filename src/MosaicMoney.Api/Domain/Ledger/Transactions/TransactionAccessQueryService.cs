using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Data;

namespace MosaicMoney.Api.Domain.Ledger.Transactions;

public sealed record TransactionReadQuery(
    Guid? AccountId,
    DateOnly? FromDate,
    DateOnly? ToDate,
    TransactionReviewStatus? ReviewStatus,
    bool NeedsReviewOnly,
    int Page,
    int PageSize);

public interface ITransactionAccessQueryService
{
    Task<bool> CanReadAccountAsync(Guid householdUserId, Guid accountId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EnrichedTransaction>> ListReadableTransactionsAsync(
        Guid householdUserId,
        TransactionReadQuery query,
        CancellationToken cancellationToken = default);

    Task<EnrichedTransaction?> GetReadableTransactionAsync(
        Guid householdUserId,
        Guid transactionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, TransactionClassificationOutcome>> QueryLatestClassificationOutcomeByTransactionIdAsync(
        IEnumerable<Guid> transactionIds,
        CancellationToken cancellationToken = default);
}

public sealed class TransactionAccessQueryService(MosaicMoneyDbContext dbContext) : ITransactionAccessQueryService
{
    public Task<bool> CanReadAccountAsync(Guid householdUserId, Guid accountId, CancellationToken cancellationToken = default)
    {
        return BuildReadableAccountIdsQuery(householdUserId)
            .AnyAsync(x => x == accountId, cancellationToken);
    }

    public async Task<IReadOnlyList<EnrichedTransaction>> ListReadableTransactionsAsync(
        Guid householdUserId,
        TransactionReadQuery query,
        CancellationToken cancellationToken = default)
    {
        var readableAccountIds = BuildReadableAccountIdsQuery(householdUserId);

        var transactionsQuery = dbContext.EnrichedTransactions
            .AsNoTracking()
            .Include(x => x.Splits)
            .Where(x => readableAccountIds.Contains(x.AccountId))
            .AsQueryable();

        if (query.AccountId.HasValue)
        {
            transactionsQuery = transactionsQuery.Where(x => x.AccountId == query.AccountId.Value);
        }

        if (query.FromDate.HasValue)
        {
            transactionsQuery = transactionsQuery.Where(x => x.TransactionDate >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            transactionsQuery = transactionsQuery.Where(x => x.TransactionDate <= query.ToDate.Value);
        }

        if (query.ReviewStatus.HasValue)
        {
            transactionsQuery = transactionsQuery.Where(x => x.ReviewStatus == query.ReviewStatus.Value);
        }

        if (query.NeedsReviewOnly)
        {
            transactionsQuery = transactionsQuery.Where(x => x.ReviewStatus == TransactionReviewStatus.NeedsReview);
        }

        return await transactionsQuery
            .OrderByDescending(x => x.TransactionDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<EnrichedTransaction?> GetReadableTransactionAsync(
        Guid householdUserId,
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        var readableAccountIds = BuildReadableAccountIdsQuery(householdUserId);

        return await dbContext.EnrichedTransactions
            .AsNoTracking()
            .Include(x => x.Splits)
            .Where(x => readableAccountIds.Contains(x.AccountId))
            .FirstOrDefaultAsync(x => x.Id == transactionId, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, TransactionClassificationOutcome>> QueryLatestClassificationOutcomeByTransactionIdAsync(
        IEnumerable<Guid> transactionIds,
        CancellationToken cancellationToken = default)
    {
        var transactionIdList = transactionIds
            .Distinct()
            .ToList();

        if (transactionIdList.Count == 0)
        {
            return new Dictionary<Guid, TransactionClassificationOutcome>();
        }

        var orderedOutcomes = await dbContext.TransactionClassificationOutcomes
            .AsNoTracking()
            .Where(x => transactionIdList.Contains(x.TransactionId))
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        return orderedOutcomes
            .GroupBy(x => x.TransactionId)
            .ToDictionary(group => group.Key, group => group.First());
    }

    private IQueryable<Guid> BuildReadableAccountIdsQuery(Guid householdUserId)
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
