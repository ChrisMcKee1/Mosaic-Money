using MosaicMoney.Api.Domain.Ledger;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class ReimbursementConflictRoutingPolicyTests
{
    [Fact]
    public void Evaluate_DuplicateProposalConflict_RoutesToNeedsReviewWithExplicitMetadata()
    {
        var incomingTransactionId = Guid.NewGuid();
        var relatedTransactionId = Guid.NewGuid();
        var existing = CreateProposal(
            incomingTransactionId,
            relatedTransactionId: relatedTransactionId,
            relatedTransactionSplitId: null,
            proposedAmount: 25m,
            status: ReimbursementProposalStatus.PendingApproval,
            lifecycleOrdinal: 1);

        var outcome = ReimbursementConflictRoutingPolicy.Evaluate(new ReimbursementConflictRoutingInput(
            incomingTransactionId,
            IncomingTransactionAmount: 120m,
            RelatedTransactionId: relatedTransactionId,
            RelatedTransactionSplitId: null,
            ProposedAmount: 25m,
            LifecycleGroupId: existing.LifecycleGroupId,
            LifecycleOrdinal: 2,
            SupersedesProposalId: null,
            SupersededProposal: null,
            ExistingProposals: [existing]));

        Assert.True(outcome.RouteToNeedsReview);
        Assert.Equal(ReimbursementConflictRoutingPolicy.DuplicateConflictReasonCode, outcome.ReasonCode);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Rationale));
    }

    [Fact]
    public void Evaluate_OverAllocationConflict_RoutesToNeedsReviewWithExplicitMetadata()
    {
        var incomingTransactionId = Guid.NewGuid();
        var existing = CreateProposal(
            incomingTransactionId,
            relatedTransactionId: Guid.NewGuid(),
            relatedTransactionSplitId: null,
            proposedAmount: 85m,
            status: ReimbursementProposalStatus.Approved,
            lifecycleOrdinal: 1);

        var outcome = ReimbursementConflictRoutingPolicy.Evaluate(new ReimbursementConflictRoutingInput(
            incomingTransactionId,
            IncomingTransactionAmount: 100m,
            RelatedTransactionId: Guid.NewGuid(),
            RelatedTransactionSplitId: null,
            ProposedAmount: 20m,
            LifecycleGroupId: Guid.NewGuid(),
            LifecycleOrdinal: 1,
            SupersedesProposalId: null,
            SupersededProposal: null,
            ExistingProposals: [existing]));

        Assert.True(outcome.RouteToNeedsReview);
        Assert.Equal(ReimbursementConflictRoutingPolicy.OverAllocationConflictReasonCode, outcome.ReasonCode);
        Assert.Contains("over-allocates", outcome.Rationale, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_StaleSupersedeConflict_RoutesToNeedsReviewWithExplicitMetadata()
    {
        var incomingTransactionId = Guid.NewGuid();
        var lifecycleGroupId = Guid.NewGuid();

        var staleSuperseded = CreateProposal(
            incomingTransactionId,
            relatedTransactionId: Guid.NewGuid(),
            relatedTransactionSplitId: null,
            proposedAmount: 40m,
            status: ReimbursementProposalStatus.PendingApproval,
            lifecycleGroupId: lifecycleGroupId,
            lifecycleOrdinal: 1);

        var newerSibling = CreateProposal(
            incomingTransactionId,
            relatedTransactionId: Guid.NewGuid(),
            relatedTransactionSplitId: null,
            proposedAmount: 35m,
            status: ReimbursementProposalStatus.PendingApproval,
            lifecycleGroupId: lifecycleGroupId,
            lifecycleOrdinal: 2);

        var outcome = ReimbursementConflictRoutingPolicy.Evaluate(new ReimbursementConflictRoutingInput(
            incomingTransactionId,
            IncomingTransactionAmount: 200m,
            RelatedTransactionId: Guid.NewGuid(),
            RelatedTransactionSplitId: null,
            ProposedAmount: 20m,
            LifecycleGroupId: lifecycleGroupId,
            LifecycleOrdinal: 3,
            SupersedesProposalId: staleSuperseded.Id,
            SupersededProposal: staleSuperseded,
            ExistingProposals: [staleSuperseded, newerSibling]));

        Assert.True(outcome.RouteToNeedsReview);
        Assert.Equal(ReimbursementConflictRoutingPolicy.StaleConflictReasonCode, outcome.ReasonCode);
        Assert.Contains("stale", outcome.Rationale, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_ExactAllocationBoundary_DoesNotRouteToNeedsReview()
    {
        var incomingTransactionId = Guid.NewGuid();
        var existing = CreateProposal(
            incomingTransactionId,
            relatedTransactionId: Guid.NewGuid(),
            relatedTransactionSplitId: null,
            proposedAmount: 80m,
            status: ReimbursementProposalStatus.Approved,
            lifecycleOrdinal: 1);

        var outcome = ReimbursementConflictRoutingPolicy.Evaluate(new ReimbursementConflictRoutingInput(
            incomingTransactionId,
            IncomingTransactionAmount: 100m,
            RelatedTransactionId: Guid.NewGuid(),
            RelatedTransactionSplitId: null,
            ProposedAmount: 20m,
            LifecycleGroupId: Guid.NewGuid(),
            LifecycleOrdinal: 1,
            SupersedesProposalId: null,
            SupersededProposal: null,
            ExistingProposals: [existing]));

        Assert.False(outcome.RouteToNeedsReview);
        Assert.Null(outcome.ReasonCode);
        Assert.Null(outcome.Rationale);
    }

    [Fact]
    public void Evaluate_SupersedeAgainstFinalizedProposal_RoutesToNeedsReviewAsStaleConflict()
    {
        var incomingTransactionId = Guid.NewGuid();
        var finalizedProposal = CreateProposal(
            incomingTransactionId,
            relatedTransactionId: Guid.NewGuid(),
            relatedTransactionSplitId: null,
            proposedAmount: 30m,
            status: ReimbursementProposalStatus.Approved,
            lifecycleOrdinal: 1,
            lifecycleGroupId: Guid.NewGuid());

        var outcome = ReimbursementConflictRoutingPolicy.Evaluate(new ReimbursementConflictRoutingInput(
            incomingTransactionId,
            IncomingTransactionAmount: 100m,
            RelatedTransactionId: Guid.NewGuid(),
            RelatedTransactionSplitId: null,
            ProposedAmount: 20m,
            LifecycleGroupId: finalizedProposal.LifecycleGroupId,
            LifecycleOrdinal: 2,
            SupersedesProposalId: finalizedProposal.Id,
            SupersededProposal: finalizedProposal,
            ExistingProposals: [finalizedProposal]));

        Assert.True(outcome.RouteToNeedsReview);
        Assert.Equal(ReimbursementConflictRoutingPolicy.StaleConflictReasonCode, outcome.ReasonCode);
    }

    private static ReimbursementProposal CreateProposal(
        Guid incomingTransactionId,
        Guid? relatedTransactionId,
        Guid? relatedTransactionSplitId,
        decimal proposedAmount,
        ReimbursementProposalStatus status,
        int lifecycleOrdinal,
        Guid? lifecycleGroupId = null)
    {
        return new ReimbursementProposal
        {
            Id = Guid.NewGuid(),
            IncomingTransactionId = incomingTransactionId,
            RelatedTransactionId = relatedTransactionId,
            RelatedTransactionSplitId = relatedTransactionSplitId,
            ProposedAmount = proposedAmount,
            Status = status,
            StatusReasonCode = "proposal_created",
            StatusRationale = "Proposal created and awaiting human review.",
            ProposalSource = ReimbursementProposalSource.Deterministic,
            ProvenanceSource = "tests",
            LifecycleGroupId = lifecycleGroupId ?? Guid.NewGuid(),
            LifecycleOrdinal = lifecycleOrdinal,
            CreatedAtUtc = DateTime.UtcNow,
        };
    }
}
