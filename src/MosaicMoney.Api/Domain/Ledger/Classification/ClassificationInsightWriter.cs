namespace MosaicMoney.Api.Domain.Ledger.Classification;

public interface IClassificationInsightWriter
{
    void RecordOutcomeInsight(
        EnrichedTransaction transaction,
        TransactionClassificationOutcome outcome,
        string? summaryOverride = null);
}

public sealed class ClassificationInsightWriter : IClassificationInsightWriter
{
    public void RecordOutcomeInsight(
        EnrichedTransaction transaction,
        TransactionClassificationOutcome outcome,
        string? summaryOverride = null)
    {
        var summary = string.IsNullOrWhiteSpace(summaryOverride)
            ? outcome.DecisionRationale
            : summaryOverride;

        var insightType = outcome.Decision == ClassificationDecision.Categorized
            ? "classification_applied"
            : "classification_needs_review";

        var requiresHumanReview = outcome.ReviewStatus == TransactionReviewStatus.NeedsReview;
        var confidence = decimal.Clamp(outcome.FinalConfidence, 0m, 1m);

        outcome.Insights.Add(new ClassificationInsight
        {
            Id = Guid.NewGuid(),
            HouseholdId = transaction.Account.HouseholdId,
            TransactionId = transaction.Id,
            OutcomeId = outcome.Id,
            InsightType = insightType,
            Summary = Truncate(summary, 500),
            Confidence = confidence,
            RequiresHumanReview = requiresHumanReview,
            CreatedAtUtc = DateTime.UtcNow,
        });
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }
}
