namespace MosaicMoney.Api.Domain.Ledger;

public enum ReimbursementDecisionAction
{
    Approve = 1,
    Reject = 2,
}

public static class ReimbursementDecisionPolicy
{
    public static bool TryParseAction(string? action, out ReimbursementDecisionAction parsedAction)
    {
        parsedAction = default;
        if (string.IsNullOrWhiteSpace(action))
        {
            return false;
        }

        var normalizedAction = action.Trim();
        if (normalizedAction.Equals("approve", StringComparison.OrdinalIgnoreCase))
        {
            parsedAction = ReimbursementDecisionAction.Approve;
            return true;
        }

        if (normalizedAction.Equals("reject", StringComparison.OrdinalIgnoreCase))
        {
            parsedAction = ReimbursementDecisionAction.Reject;
            return true;
        }

        return false;
    }

    public static void ApplyDecision(
        ReimbursementProposal proposal,
        ReimbursementDecisionAction action,
        Guid decisionedByUserId,
        DateTime decisionedAtUtc,
        string? userNote,
        string? agentNote)
    {
        var isApproval = action == ReimbursementDecisionAction.Approve;

        proposal.Status = isApproval
            ? ReimbursementProposalStatus.Approved
            : ReimbursementProposalStatus.Rejected;
        proposal.StatusReasonCode = isApproval
            ? "approved_by_human"
            : "rejected_by_human";
        proposal.StatusRationale = isApproval
            ? "Proposal approved by human reviewer."
            : "Proposal rejected by human reviewer.";
        proposal.DecisionedByUserId = decisionedByUserId;
        proposal.DecisionedAtUtc = decisionedAtUtc;
        proposal.UserNote = string.IsNullOrWhiteSpace(userNote)
            ? proposal.UserNote
            : userNote.Trim();
        proposal.AgentNote = string.IsNullOrWhiteSpace(agentNote)
            ? proposal.AgentNote
            : agentNote.Trim();
    }
}
