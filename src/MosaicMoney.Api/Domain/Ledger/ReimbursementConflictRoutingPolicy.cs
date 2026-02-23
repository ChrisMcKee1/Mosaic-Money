namespace MosaicMoney.Api.Domain.Ledger;

public sealed record ReimbursementConflictRoutingInput(
    Guid IncomingTransactionId,
    decimal IncomingTransactionAmount,
    Guid? RelatedTransactionId,
    Guid? RelatedTransactionSplitId,
    decimal ProposedAmount,
    Guid LifecycleGroupId,
    int LifecycleOrdinal,
    Guid? SupersedesProposalId,
    ReimbursementProposal? SupersededProposal,
    IReadOnlyList<ReimbursementProposal> ExistingProposals);

public sealed record ReimbursementConflictRoutingOutcome(
    bool RouteToNeedsReview,
    string? ReasonCode,
    string? Rationale)
{
    public static readonly ReimbursementConflictRoutingOutcome None = new(false, null, null);

    public static ReimbursementConflictRoutingOutcome NeedsReview(string reasonCode, string rationale) =>
        new(true, reasonCode, rationale);
}

public static class ReimbursementConflictRoutingPolicy
{
    public const string DuplicateConflictReasonCode = "reimbursement_conflict_duplicate_proposal";
    public const string OverAllocationConflictReasonCode = "reimbursement_conflict_over_allocation";
    public const string StaleConflictReasonCode = "reimbursement_conflict_stale_proposal";

    public static ReimbursementConflictRoutingOutcome Evaluate(ReimbursementConflictRoutingInput input)
    {
        if (IsStaleSupersedeConflict(input))
        {
            return ReimbursementConflictRoutingOutcome.NeedsReview(
                StaleConflictReasonCode,
                "Supersede target is stale relative to the current reimbursement lifecycle and requires human review.");
        }

        if (HasDuplicateConflict(input))
        {
            return ReimbursementConflictRoutingOutcome.NeedsReview(
                DuplicateConflictReasonCode,
                "An active reimbursement proposal already targets this incoming transaction and related target; human review is required.");
        }

        var incomingAbsAmount = decimal.Abs(decimal.Round(input.IncomingTransactionAmount, 2));
        var proposedAbsAmount = decimal.Abs(decimal.Round(input.ProposedAmount, 2));
        var allocatedAbsAmount = decimal.Round(
            input.ExistingProposals
                .Where(x => IsActiveForAllocation(x.Status))
                .Sum(x => decimal.Abs(x.ProposedAmount)),
            2);

        if (decimal.Round(allocatedAbsAmount + proposedAbsAmount, 2) > incomingAbsAmount)
        {
            return ReimbursementConflictRoutingOutcome.NeedsReview(
                OverAllocationConflictReasonCode,
                $"Proposed reimbursement over-allocates the incoming transaction amount (allocated={allocatedAbsAmount:F2}, proposed={proposedAbsAmount:F2}, incoming={incomingAbsAmount:F2}); human review is required.");
        }

        return ReimbursementConflictRoutingOutcome.None;
    }

    private static bool HasDuplicateConflict(ReimbursementConflictRoutingInput input)
    {
        return input.ExistingProposals
            .Where(x => IsActiveForDuplicateCheck(x.Status))
            .Where(x => input.SupersedesProposalId != x.Id)
            .Any(x =>
                x.RelatedTransactionId == input.RelatedTransactionId
                && x.RelatedTransactionSplitId == input.RelatedTransactionSplitId);
    }

    private static bool IsStaleSupersedeConflict(ReimbursementConflictRoutingInput input)
    {
        if (!input.SupersedesProposalId.HasValue || input.SupersededProposal is null)
        {
            return false;
        }

        if (!IsSupersedableStatus(input.SupersededProposal.Status))
        {
            return true;
        }

        var hasNewerSibling = input.ExistingProposals.Any(x =>
            x.Id != input.SupersededProposal.Id
            && x.LifecycleGroupId == input.SupersededProposal.LifecycleGroupId
            && x.LifecycleOrdinal > input.SupersededProposal.LifecycleOrdinal);

        return hasNewerSibling;
    }

    private static bool IsActiveForAllocation(ReimbursementProposalStatus status)
    {
        return status is ReimbursementProposalStatus.PendingApproval
            or ReimbursementProposalStatus.Approved
            or ReimbursementProposalStatus.NeedsReview;
    }

    private static bool IsActiveForDuplicateCheck(ReimbursementProposalStatus status)
    {
        return status is ReimbursementProposalStatus.PendingApproval
            or ReimbursementProposalStatus.Approved
            or ReimbursementProposalStatus.NeedsReview;
    }

    private static bool IsSupersedableStatus(ReimbursementProposalStatus status)
    {
        return status is ReimbursementProposalStatus.PendingApproval
            or ReimbursementProposalStatus.NeedsReview;
    }
}
