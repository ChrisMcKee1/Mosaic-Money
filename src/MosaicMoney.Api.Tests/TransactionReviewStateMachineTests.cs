using MosaicMoney.Api.Domain.Ledger;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class TransactionReviewStateMachineTests
{
    [Theory]
    [InlineData("approve", TransactionReviewAction.Approve)]
    [InlineData("APPROVE", TransactionReviewAction.Approve)]
    [InlineData("reclassify", TransactionReviewAction.Reclassify)]
    [InlineData("route_to_needs_review", TransactionReviewAction.RouteToNeedsReview)]
    [InlineData(" route_to_needs_review ", TransactionReviewAction.RouteToNeedsReview)]
    public void TryParseAction_ReturnsExpectedAction_WhenActionIsRecognized(string rawAction, TransactionReviewAction expected)
    {
        var parsed = TransactionReviewStateMachine.TryParseAction(rawAction, out var action);

        Assert.True(parsed);
        Assert.Equal(expected, action);
    }

    [Fact]
    public void TryParseAction_FailsClosed_WhenActionIsUnknown()
    {
        var parsed = TransactionReviewStateMachine.TryParseAction("auto_resolve", out _);

        Assert.False(parsed);
    }

    [Fact]
    public void TryParseAction_FailsClosed_WhenActionIsNull()
    {
        var parsed = TransactionReviewStateMachine.TryParseAction(null!, out _);

        Assert.False(parsed);
    }

    [Theory]
    [InlineData(TransactionReviewStatus.None, TransactionReviewAction.RouteToNeedsReview, TransactionReviewStatus.NeedsReview)]
    [InlineData(TransactionReviewStatus.NeedsReview, TransactionReviewAction.Approve, TransactionReviewStatus.Reviewed)]
    [InlineData(TransactionReviewStatus.NeedsReview, TransactionReviewAction.Reclassify, TransactionReviewStatus.Reviewed)]
    [InlineData(TransactionReviewStatus.NeedsReview, TransactionReviewAction.RouteToNeedsReview, TransactionReviewStatus.NeedsReview)]
    [InlineData(TransactionReviewStatus.Reviewed, TransactionReviewAction.RouteToNeedsReview, TransactionReviewStatus.NeedsReview)]
    public void TryTransition_AppliesAllowedTransitions(
        TransactionReviewStatus current,
        TransactionReviewAction action,
        TransactionReviewStatus expectedNext)
    {
        var transitioned = TransactionReviewStateMachine.TryTransition(current, action, out var next);

        Assert.True(transitioned);
        Assert.Equal(expectedNext, next);
    }

    [Theory]
    [InlineData(TransactionReviewStatus.None, TransactionReviewAction.Approve)]
    [InlineData(TransactionReviewStatus.None, TransactionReviewAction.Reclassify)]
    [InlineData(TransactionReviewStatus.Reviewed, TransactionReviewAction.Approve)]
    [InlineData(TransactionReviewStatus.Reviewed, TransactionReviewAction.Reclassify)]
    public void TryTransition_FailsClosed_ForInvalidTransitions(TransactionReviewStatus current, TransactionReviewAction action)
    {
        var transitioned = TransactionReviewStateMachine.TryTransition(current, action, out var next);

        Assert.False(transitioned);
        Assert.Equal(current, next);
    }
}
