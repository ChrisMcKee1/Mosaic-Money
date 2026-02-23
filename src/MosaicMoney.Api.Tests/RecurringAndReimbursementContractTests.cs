using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class RecurringAndReimbursementContractTests
{
    [Fact]
    public void RecurringItem_DefaultPolicy_IsConfiguredForDeterministicMatching()
    {
        var recurringItem = new RecurringItem();

        Assert.Equal(3, recurringItem.DueWindowDaysBefore);
        Assert.Equal(3, recurringItem.DueWindowDaysAfter);
        Assert.Equal(5.00m, recurringItem.AmountVariancePercent);
        Assert.Equal(0m, recurringItem.AmountVarianceAbsolute);
        Assert.Equal(0.7000m, recurringItem.DeterministicMatchThreshold);
        Assert.Equal(1.0000m, decimal.Round(recurringItem.DueDateScoreWeight + recurringItem.AmountScoreWeight + recurringItem.RecencyScoreWeight, 4));
        Assert.False(string.IsNullOrWhiteSpace(recurringItem.DeterministicScoreVersion));
        Assert.False(string.IsNullOrWhiteSpace(recurringItem.TieBreakPolicy));
    }

    [Fact]
    public void ReimbursementProposal_DefaultLifecycleAndProvenance_AreExplicit()
    {
        var proposal = new ReimbursementProposal();

        Assert.Equal(1, proposal.LifecycleOrdinal);
        Assert.Equal(ReimbursementProposalStatus.PendingApproval, proposal.Status);
        Assert.Equal(ReimbursementProposalSource.Deterministic, proposal.ProposalSource);
        Assert.False(string.IsNullOrWhiteSpace(proposal.StatusReasonCode));
        Assert.False(string.IsNullOrWhiteSpace(proposal.StatusRationale));
        Assert.False(string.IsNullOrWhiteSpace(proposal.ProvenanceSource));
    }

    [Fact]
    public void ReimbursementProposalStatus_IncludesLifecycleStatesNeededFor08A()
    {
        Assert.Contains(ReimbursementProposalStatus.PendingApproval, Enum.GetValues<ReimbursementProposalStatus>());
        Assert.Contains(ReimbursementProposalStatus.Approved, Enum.GetValues<ReimbursementProposalStatus>());
        Assert.Contains(ReimbursementProposalStatus.Rejected, Enum.GetValues<ReimbursementProposalStatus>());
        Assert.Contains(ReimbursementProposalStatus.NeedsReview, Enum.GetValues<ReimbursementProposalStatus>());
        Assert.Contains(ReimbursementProposalStatus.Superseded, Enum.GetValues<ReimbursementProposalStatus>());
        Assert.Contains(ReimbursementProposalStatus.Cancelled, Enum.GetValues<ReimbursementProposalStatus>());
    }

    [Fact]
    public void DbModel_DeclaresRecurringAndReimbursementContractCheckConstraints()
    {
        using var dbContext = CreateDbContext();

        var model = dbContext.GetService<IDesignTimeModel>().Model;

        var recurringConstraintNames = model
            .FindEntityType(typeof(RecurringItem))!
            .GetCheckConstraints()
            .Select(x => x.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("CK_RecurringItem_DueWindowRange", recurringConstraintNames);
        Assert.Contains("CK_RecurringItem_VarianceRange", recurringConstraintNames);
        Assert.Contains("CK_RecurringItem_DeterministicThresholdRange", recurringConstraintNames);
        Assert.Contains("CK_RecurringItem_ScoreWeightsRange", recurringConstraintNames);
        Assert.Contains("CK_RecurringItem_ScoreWeightsSum", recurringConstraintNames);
        Assert.Contains("CK_RecurringItem_DeterministicMetadataRequired", recurringConstraintNames);

        var reimbursementConstraintNames = model
            .FindEntityType(typeof(ReimbursementProposal))!
            .GetCheckConstraints()
            .Select(x => x.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("CK_ReimbursementProposal_OneRelatedTarget", reimbursementConstraintNames);
        Assert.Contains("CK_ReimbursementProposal_LifecycleOrdinal", reimbursementConstraintNames);
        Assert.Contains("CK_ReimbursementProposal_RationaleRequired", reimbursementConstraintNames);
        Assert.Contains("CK_ReimbursementProposal_ProvenanceRequired", reimbursementConstraintNames);
        Assert.Contains("CK_ReimbursementProposal_DecisionAuditForFinalStates", reimbursementConstraintNames);
    }

    private static MosaicMoneyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MosaicMoneyDbContext>()
            .UseInMemoryDatabase($"mosaicmoney-contract-tests-{Guid.NewGuid()}")
            .Options;

        return new MosaicMoneyDbContext(options);
    }
}
