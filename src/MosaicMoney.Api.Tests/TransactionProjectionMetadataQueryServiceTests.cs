using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class TransactionProjectionMetadataQueryServiceTests
{
    [Fact]
    public async Task QueryAsync_ReturnsProjectionMetadataShape_WithRawTruthAndProjectionContext()
    {
        await using var dbContext = CreateDbContext();

        var household = new Household
        {
            Id = Guid.NewGuid(),
            Name = "Projection Test Household",
        };

        var account = new Account
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            Name = "Checking",
            ExternalAccountKey = "projection-checking",
        };

        var recurringItem = new RecurringItem
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            MerchantName = "Gym Membership",
            ExpectedAmount = 60.00m,
            Frequency = RecurringFrequency.Monthly,
            NextDueDate = new DateOnly(2026, 3, 1),
            IsActive = true,
        };

        var transaction = new EnrichedTransaction
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            RecurringItemId = recurringItem.Id,
            Description = "Gym Membership Charge",
            Amount = -120.55m,
            TransactionDate = new DateOnly(2026, 2, 20),
            ReviewStatus = TransactionReviewStatus.NeedsReview,
            ReviewReason = "recurring_competing_candidates",
            ExcludeFromBudget = true,
            IsExtraPrincipal = false,
            CreatedAtUtc = new DateTime(2026, 2, 20, 12, 0, 0, DateTimeKind.Utc),
            LastModifiedAtUtc = new DateTime(2026, 2, 20, 12, 5, 0, DateTimeKind.Utc),
            Splits =
            {
                new TransactionSplit
                {
                    Id = Guid.NewGuid(),
                    Amount = -100.55m,
                    AmortizationMonths = 12,
                },
                new TransactionSplit
                {
                    Id = Guid.NewGuid(),
                    Amount = -20.00m,
                    AmortizationMonths = 1,
                },
            },
        };

        var approvedProposal = new ReimbursementProposal
        {
            Id = Guid.NewGuid(),
            IncomingTransactionId = transaction.Id,
            RelatedTransactionId = Guid.NewGuid(),
            ProposedAmount = 40.00m,
            Status = ReimbursementProposalStatus.Approved,
            StatusReasonCode = "approved_by_human",
            StatusRationale = "Approved by reviewer.",
            ProposalSource = ReimbursementProposalSource.Deterministic,
            ProvenanceSource = "tests",
            LifecycleGroupId = Guid.NewGuid(),
            LifecycleOrdinal = 1,
            CreatedAtUtc = new DateTime(2026, 2, 20, 13, 0, 0, DateTimeKind.Utc),
            DecisionedByUserId = Guid.NewGuid(),
            DecisionedAtUtc = new DateTime(2026, 2, 20, 13, 5, 0, DateTimeKind.Utc),
        };

        var pendingProposal = new ReimbursementProposal
        {
            Id = Guid.NewGuid(),
            IncomingTransactionId = transaction.Id,
            RelatedTransactionId = Guid.NewGuid(),
            ProposedAmount = 15.00m,
            Status = ReimbursementProposalStatus.PendingApproval,
            StatusReasonCode = "proposal_created",
            StatusRationale = "Proposal created and awaiting review.",
            ProposalSource = ReimbursementProposalSource.Deterministic,
            ProvenanceSource = "tests",
            LifecycleGroupId = Guid.NewGuid(),
            LifecycleOrdinal = 1,
            CreatedAtUtc = new DateTime(2026, 2, 20, 14, 0, 0, DateTimeKind.Utc),
        };

        dbContext.Households.Add(household);
        dbContext.Accounts.Add(account);
        dbContext.RecurringItems.Add(recurringItem);
        dbContext.EnrichedTransactions.Add(transaction);
        dbContext.ReimbursementProposals.AddRange(approvedProposal, pendingProposal);
        await dbContext.SaveChangesAsync();

        var service = new TransactionProjectionMetadataQueryService(dbContext);

        var results = await service.QueryAsync(
            accountId: account.Id,
            fromDate: null,
            toDate: null,
            reviewStatus: null,
            needsReviewOnly: false,
            page: 1,
            pageSize: 50,
            cancellationToken: default);

        var result = Assert.Single(results);

        Assert.Equal(transaction.Id, result.Id);
        Assert.Equal(account.Id, result.AccountId);
        Assert.Equal(transaction.Description, result.Description);
        Assert.Equal(-120.55m, result.RawAmount);
        Assert.Equal(new DateOnly(2026, 2, 20), result.RawTransactionDate);

        Assert.Equal(TransactionReviewStatus.NeedsReview.ToString(), result.ReviewStatus);
        Assert.Equal("recurring_competing_candidates", result.ReviewReason);
        Assert.True(result.ExcludeFromBudget);
        Assert.False(result.IsExtraPrincipal);

        Assert.True(result.Recurring.IsLinked);
        Assert.Equal(recurringItem.Id, result.Recurring.RecurringItemId);
        Assert.True(result.Recurring.IsActive);
        Assert.Equal(RecurringFrequency.Monthly.ToString(), result.Recurring.Frequency);
        Assert.Equal(new DateOnly(2026, 3, 1), result.Recurring.NextDueDate);

        Assert.Equal(2, result.Splits.Count);
        Assert.Contains(result.Splits, x => x.AmortizationMonths == 12 && x.RawAmount == -100.55m);
        Assert.Contains(result.Splits, x => x.AmortizationMonths == 1 && x.RawAmount == -20.00m);

        Assert.True(result.Reimbursement.HasProposals);
        Assert.Equal(2, result.Reimbursement.ProposalCount);
        Assert.True(result.Reimbursement.HasPendingHumanReview);
        Assert.Equal(ReimbursementProposalStatus.PendingApproval.ToString(), result.Reimbursement.LatestStatus);
        Assert.Equal("proposal_created", result.Reimbursement.LatestStatusReasonCode);
        Assert.Equal(15.00m, result.Reimbursement.PendingOrNeedsReviewAmount);
        Assert.Equal(40.00m, result.Reimbursement.ApprovedAmount);
    }

    [Fact]
    public async Task QueryAsync_IsReadOnly_AndDoesNotMutateRawLedgerTruth()
    {
        await using var dbContext = CreateDbContext();

        var householdId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var splitId = Guid.NewGuid();
        var lastModifiedAtUtc = new DateTime(2026, 2, 22, 9, 30, 0, DateTimeKind.Utc);

        dbContext.Households.Add(new Household
        {
            Id = householdId,
            Name = "Read Only Household",
        });

        dbContext.Accounts.Add(new Account
        {
            Id = accountId,
            HouseholdId = householdId,
            Name = "Read Only Account",
            ExternalAccountKey = "readonly-account",
        });

        dbContext.EnrichedTransactions.Add(new EnrichedTransaction
        {
            Id = transactionId,
            AccountId = accountId,
            Description = "Immutable Transaction",
            Amount = -88.40m,
            TransactionDate = new DateOnly(2026, 2, 10),
            ReviewStatus = TransactionReviewStatus.None,
            ExcludeFromBudget = false,
            IsExtraPrincipal = true,
            CreatedAtUtc = new DateTime(2026, 2, 10, 8, 0, 0, DateTimeKind.Utc),
            LastModifiedAtUtc = lastModifiedAtUtc,
            Splits =
            {
                new TransactionSplit
                {
                    Id = splitId,
                    Amount = -88.40m,
                    AmortizationMonths = 6,
                },
            },
        });

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var service = new TransactionProjectionMetadataQueryService(dbContext);

        var results = await service.QueryAsync(
            accountId: accountId,
            fromDate: null,
            toDate: null,
            reviewStatus: null,
            needsReviewOnly: false,
            page: 1,
            pageSize: 10,
            cancellationToken: default);

        var result = Assert.Single(results);
        Assert.Equal(-88.40m, result.RawAmount);
        Assert.Equal(new DateOnly(2026, 2, 10), result.RawTransactionDate);
        Assert.Equal(6, Assert.Single(result.Splits).AmortizationMonths);

        Assert.Empty(dbContext.ChangeTracker.Entries());

        var persisted = await dbContext.EnrichedTransactions
            .AsNoTracking()
            .Include(x => x.Splits)
            .SingleAsync(x => x.Id == transactionId);

        Assert.Equal(-88.40m, persisted.Amount);
        Assert.Equal(new DateOnly(2026, 2, 10), persisted.TransactionDate);
        Assert.Equal(lastModifiedAtUtc, persisted.LastModifiedAtUtc);

        var persistedSplit = Assert.Single(persisted.Splits);
        Assert.Equal(splitId, persistedSplit.Id);
        Assert.Equal(-88.40m, persistedSplit.Amount);
        Assert.Equal(6, persistedSplit.AmortizationMonths);
    }

    [Fact]
    public async Task QueryAsync_AppliesNeedsReviewDateFilters_AndPaginatesInDeterministicOrder()
    {
        await using var dbContext = CreateDbContext();

        var householdId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        dbContext.Households.Add(new Household
        {
            Id = householdId,
            Name = "Filter Household",
        });

        dbContext.Accounts.Add(new Account
        {
            Id = accountId,
            HouseholdId = householdId,
            Name = "Filter Account",
            ExternalAccountKey = "filter-account",
        });

        var txOutsideRange = new EnrichedTransaction
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Description = "outside-range",
            Amount = -11.00m,
            TransactionDate = new DateOnly(2026, 1, 31),
            ReviewStatus = TransactionReviewStatus.NeedsReview,
            CreatedAtUtc = new DateTime(2026, 1, 31, 9, 0, 0, DateTimeKind.Utc),
            LastModifiedAtUtc = new DateTime(2026, 1, 31, 9, 1, 0, DateTimeKind.Utc),
        };

        var txInRangeNeedsReview = new EnrichedTransaction
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Description = "in-range-needs-review",
            Amount = -22.00m,
            TransactionDate = new DateOnly(2026, 2, 15),
            ReviewStatus = TransactionReviewStatus.NeedsReview,
            CreatedAtUtc = new DateTime(2026, 2, 15, 10, 0, 0, DateTimeKind.Utc),
            LastModifiedAtUtc = new DateTime(2026, 2, 15, 10, 1, 0, DateTimeKind.Utc),
        };

        var txInRangeReviewed = new EnrichedTransaction
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Description = "in-range-reviewed",
            Amount = -33.00m,
            TransactionDate = new DateOnly(2026, 2, 16),
            ReviewStatus = TransactionReviewStatus.Reviewed,
            CreatedAtUtc = new DateTime(2026, 2, 16, 11, 0, 0, DateTimeKind.Utc),
            LastModifiedAtUtc = new DateTime(2026, 2, 16, 11, 1, 0, DateTimeKind.Utc),
        };

        dbContext.EnrichedTransactions.AddRange(txOutsideRange, txInRangeNeedsReview, txInRangeReviewed);
        await dbContext.SaveChangesAsync();

        var service = new TransactionProjectionMetadataQueryService(dbContext);

        var filtered = await service.QueryAsync(
            accountId: accountId,
            fromDate: new DateOnly(2026, 2, 1),
            toDate: new DateOnly(2026, 2, 28),
            reviewStatus: null,
            needsReviewOnly: true,
            page: 1,
            pageSize: 10,
            cancellationToken: default);

        var filteredResult = Assert.Single(filtered);
        Assert.Equal(txInRangeNeedsReview.Id, filteredResult.Id);
        Assert.Equal(TransactionReviewStatus.NeedsReview.ToString(), filteredResult.ReviewStatus);

        var page1 = await service.QueryAsync(
            accountId: accountId,
            fromDate: null,
            toDate: null,
            reviewStatus: null,
            needsReviewOnly: false,
            page: 1,
            pageSize: 2,
            cancellationToken: default);

        Assert.Equal(2, page1.Count);
        Assert.Equal(txInRangeReviewed.Id, page1[0].Id);
        Assert.Equal(txInRangeNeedsReview.Id, page1[1].Id);

        var page2 = await service.QueryAsync(
            accountId: accountId,
            fromDate: null,
            toDate: null,
            reviewStatus: null,
            needsReviewOnly: false,
            page: 2,
            pageSize: 2,
            cancellationToken: default);

        var page2Result = Assert.Single(page2);
        Assert.Equal(txOutsideRange.Id, page2Result.Id);
    }

    private static MosaicMoneyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MosaicMoneyDbContext>()
            .UseInMemoryDatabase($"mosaicmoney-projection-tests-{Guid.NewGuid()}")
            .Options;

        return new MosaicMoneyDbContext(options);
    }
}
