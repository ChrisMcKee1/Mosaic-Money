namespace MosaicMoney.Api.Domain.Ledger;

public enum TransactionReviewAction
{
    Approve = 1,
    Reclassify = 2,
    RouteToNeedsReview = 3,
}

public static class TransactionReviewStateMachine
{
    public static bool TryParseAction(string value, out TransactionReviewAction action)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            action = default;
            return false;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "approve":
                action = TransactionReviewAction.Approve;
                return true;
            case "reclassify":
                action = TransactionReviewAction.Reclassify;
                return true;
            case "route_to_needs_review":
                action = TransactionReviewAction.RouteToNeedsReview;
                return true;
            default:
                action = default;
                return false;
        }
    }

    public static bool TryTransition(
        TransactionReviewStatus currentStatus,
        TransactionReviewAction action,
        out TransactionReviewStatus nextStatus)
    {
        nextStatus = currentStatus;

        if (!CanTransition(currentStatus, action))
        {
            return false;
        }

        nextStatus = action switch
        {
            TransactionReviewAction.Approve => TransactionReviewStatus.Reviewed,
            TransactionReviewAction.Reclassify => TransactionReviewStatus.Reviewed,
            TransactionReviewAction.RouteToNeedsReview => TransactionReviewStatus.NeedsReview,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown transaction review action."),
        };

        return true;
    }

    public static bool CanTransition(TransactionReviewStatus currentStatus, TransactionReviewAction action)
    {
        return action switch
        {
            TransactionReviewAction.Approve => currentStatus == TransactionReviewStatus.NeedsReview,
            TransactionReviewAction.Reclassify => currentStatus == TransactionReviewStatus.NeedsReview,
            TransactionReviewAction.RouteToNeedsReview => currentStatus is TransactionReviewStatus.None
                or TransactionReviewStatus.NeedsReview
                or TransactionReviewStatus.Reviewed,
            _ => false,
        };
    }
}
