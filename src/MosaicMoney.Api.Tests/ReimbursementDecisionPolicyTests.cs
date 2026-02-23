using MosaicMoney.Api.Domain.Ledger;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class ReimbursementDecisionPolicyTests
{
    [Theory]
    [InlineData("approve", ReimbursementDecisionAction.Approve)]
    [InlineData("APPROVE", ReimbursementDecisionAction.Approve)]
    [InlineData(" reject ", ReimbursementDecisionAction.Reject)]
    public void TryParseAction_ParsesSupportedActions(string action, ReimbursementDecisionAction expected)
    {
        var parsed = ReimbursementDecisionPolicy.TryParseAction(action, out var actual);

        Assert.True(parsed);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("auto_approve")]
    [InlineData("route_to_needs_review")]
    public void TryParseAction_RejectsUnsupportedActions(string action)
    {
        var parsed = ReimbursementDecisionPolicy.TryParseAction(action, out _);

        Assert.False(parsed);
    }

    [Fact]
    public void ApplyDecision_Approve_SetsActorTimeAndDeterministicAuditMetadata()
    {
        var proposal = CreatePendingProposal();
        var actorUserId = Guid.NewGuid();
        var decidedAtUtc = new DateTime(2026, 2, 22, 12, 30, 0, DateTimeKind.Utc);

        ReimbursementDecisionPolicy.ApplyDecision(
            proposal,
            ReimbursementDecisionAction.Approve,
            actorUserId,
            decidedAtUtc,
            " approved by reviewer ",
            " agent context kept ");

        Assert.Equal(ReimbursementProposalStatus.Approved, proposal.Status);
        Assert.Equal("approved_by_human", proposal.StatusReasonCode);
        Assert.Equal("Proposal approved by human reviewer.", proposal.StatusRationale);
        Assert.Equal(actorUserId, proposal.DecisionedByUserId);
        Assert.Equal(decidedAtUtc, proposal.DecisionedAtUtc);
        Assert.Equal("approved by reviewer", proposal.UserNote);
        Assert.Equal("agent context kept", proposal.AgentNote);
    }

    [Fact]
    public void ApplyDecision_Reject_PreservesExistingNotesWhenNewNotesAreBlank()
    {
        var proposal = CreatePendingProposal();
        proposal.UserNote = "existing-user-note";
        proposal.AgentNote = "existing-agent-note";
        var actorUserId = Guid.NewGuid();
        var decidedAtUtc = new DateTime(2026, 2, 22, 14, 0, 0, DateTimeKind.Utc);

        ReimbursementDecisionPolicy.ApplyDecision(
            proposal,
            ReimbursementDecisionAction.Reject,
            actorUserId,
            decidedAtUtc,
            " ",
            null);

        Assert.Equal(ReimbursementProposalStatus.Rejected, proposal.Status);
        Assert.Equal("rejected_by_human", proposal.StatusReasonCode);
        Assert.Equal("Proposal rejected by human reviewer.", proposal.StatusRationale);
        Assert.Equal(actorUserId, proposal.DecisionedByUserId);
        Assert.Equal(decidedAtUtc, proposal.DecisionedAtUtc);
        Assert.Equal("existing-user-note", proposal.UserNote);
        Assert.Equal("existing-agent-note", proposal.AgentNote);
    }

    private static ReimbursementProposal CreatePendingProposal()
    {
        return new ReimbursementProposal
        {
            Id = Guid.NewGuid(),
            IncomingTransactionId = Guid.NewGuid(),
            RelatedTransactionId = Guid.NewGuid(),
            ProposedAmount = 42.00m,
            LifecycleGroupId = Guid.NewGuid(),
            LifecycleOrdinal = 1,
            Status = ReimbursementProposalStatus.PendingApproval,
            StatusReasonCode = "proposal_created",
            StatusRationale = "Proposal created and awaiting human review.",
            ProposalSource = ReimbursementProposalSource.Deterministic,
            ProvenanceSource = "api",
            CreatedAtUtc = DateTime.UtcNow,
        };
    }
}
