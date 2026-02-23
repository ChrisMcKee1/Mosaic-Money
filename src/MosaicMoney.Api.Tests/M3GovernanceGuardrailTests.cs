using MosaicMoney.Api.Domain.Ledger;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class M3GovernanceGuardrailTests
{
    [Fact]
    public void TransactionReviewAction_ExposesOnlyHumanReviewActions()
    {
        var actions = Enum.GetValues<TransactionReviewAction>();

        Assert.Equal(
            [
                TransactionReviewAction.Approve,
                TransactionReviewAction.Reclassify,
                TransactionReviewAction.RouteToNeedsReview,
            ],
            actions);
    }

    [Fact]
    public void ReimbursementDecisionAction_ExposesOnlyExplicitHumanDecisionActions()
    {
        var actions = Enum.GetValues<ReimbursementDecisionAction>();

        Assert.Equal(
            [
                ReimbursementDecisionAction.Approve,
                ReimbursementDecisionAction.Reject,
            ],
            actions);
    }

    [Theory]
    [InlineData("auto_approve")]
    [InlineData("auto_reject")]
    [InlineData("send_message")]
    [InlineData("send_payment")]
    [InlineData("notify_external_system")]
    public void ActionParsers_RejectAutonomousAndExternalMessagingCommands(string command)
    {
        var reviewParsed = TransactionReviewStateMachine.TryParseAction(command, out _);
        var reimbursementParsed = ReimbursementDecisionPolicy.TryParseAction(command, out _);

        Assert.False(reviewParsed);
        Assert.False(reimbursementParsed);
    }
}
