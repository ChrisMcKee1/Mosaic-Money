using MosaicMoney.Api.Contracts.V1;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.Ingestion;

namespace MosaicMoney.Api.Apis;

internal static class ApiEndpointHelpers
{
    internal static bool TryParseEnum<TEnum>(string value, out TEnum parsed)
        where TEnum : struct, Enum
    {
        return Enum.TryParse(value, ignoreCase: true, out parsed) && Enum.IsDefined(parsed);
    }

    internal static bool AreScoreWeightsValid(decimal dueDateWeight, decimal amountWeight, decimal recencyWeight)
    {
        return decimal.Round(dueDateWeight + amountWeight + recencyWeight, 4) == 1.0000m;
    }

    internal static int GetExpectedStageOrder(ClassificationStage stage)
    {
        return stage switch
        {
            ClassificationStage.Deterministic => 1,
            ClassificationStage.Semantic => 2,
            ClassificationStage.MafFallback => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown classification stage."),
        };
    }

    internal static TransactionDto MapTransaction(EnrichedTransaction transaction)
    {
        return new TransactionDto(
            transaction.Id,
            transaction.AccountId,
            transaction.RecurringItemId,
            transaction.SubcategoryId,
            transaction.NeedsReviewByUserId,
            transaction.PlaidTransactionId,
            transaction.Description,
            transaction.Amount,
            transaction.TransactionDate,
            transaction.ReviewStatus.ToString(),
            transaction.ReviewReason,
            transaction.ExcludeFromBudget,
            transaction.IsExtraPrincipal,
            transaction.UserNote,
            transaction.AgentNote,
            transaction.Splits
                .OrderBy(x => x.Id)
                .Select(x => new TransactionSplitDto(
                    x.Id,
                    x.SubcategoryId,
                    x.Amount,
                    x.AmortizationMonths,
                    x.UserNote,
                    x.AgentNote))
                .ToList(),
            transaction.CreatedAtUtc,
            transaction.LastModifiedAtUtc);
    }

    internal static RecurringItemDto MapRecurringItem(RecurringItem recurringItem)
    {
        return new RecurringItemDto(
            recurringItem.Id,
            recurringItem.HouseholdId,
            recurringItem.MerchantName,
            recurringItem.ExpectedAmount,
            recurringItem.IsVariable,
            recurringItem.Frequency.ToString(),
            recurringItem.NextDueDate,
            recurringItem.DueWindowDaysBefore,
            recurringItem.DueWindowDaysAfter,
            recurringItem.AmountVariancePercent,
            recurringItem.AmountVarianceAbsolute,
            recurringItem.DeterministicMatchThreshold,
            recurringItem.DueDateScoreWeight,
            recurringItem.AmountScoreWeight,
            recurringItem.RecencyScoreWeight,
            recurringItem.DeterministicScoreVersion,
            recurringItem.TieBreakPolicy,
            recurringItem.IsActive,
            recurringItem.UserNote,
            recurringItem.AgentNote);
    }

    internal static ReimbursementProposalDto MapReimbursement(ReimbursementProposal proposal)
    {
        return new ReimbursementProposalDto(
            proposal.Id,
            proposal.IncomingTransactionId,
            proposal.RelatedTransactionId,
            proposal.RelatedTransactionSplitId,
            proposal.ProposedAmount,
            proposal.LifecycleGroupId,
            proposal.LifecycleOrdinal,
            proposal.Status.ToString(),
            proposal.StatusReasonCode,
            proposal.StatusRationale,
            proposal.ProposalSource.ToString(),
            proposal.ProvenanceSource,
            proposal.ProvenanceReference,
            proposal.ProvenancePayloadJson,
            proposal.SupersedesProposalId,
            proposal.DecisionedByUserId,
            proposal.DecisionedAtUtc,
            proposal.UserNote,
            proposal.AgentNote,
            proposal.CreatedAtUtc);
    }

    internal static ClassificationOutcomeDto MapClassificationOutcome(TransactionClassificationOutcome outcome)
    {
        return new ClassificationOutcomeDto(
            outcome.Id,
            outcome.TransactionId,
            outcome.ProposedSubcategoryId,
            outcome.FinalConfidence,
            outcome.Decision.ToString(),
            outcome.ReviewStatus.ToString(),
            outcome.DecisionReasonCode,
            outcome.DecisionRationale,
            outcome.AgentNoteSummary,
            outcome.CreatedAtUtc,
            outcome.StageOutputs
                .OrderBy(x => x.StageOrder)
                .Select(x => new ClassificationStageOutputDto(
                    x.Id,
                    x.Stage.ToString(),
                    x.StageOrder,
                    x.ProposedSubcategoryId,
                    x.Confidence,
                    x.RationaleCode,
                    x.Rationale,
                    x.EscalatedToNextStage,
                    x.ProducedAtUtc))
                .ToList());
    }

    internal static PlaidDeltaIngestionResultDto MapPlaidDeltaIngestionResult(PlaidDeltaIngestionResult result)
    {
        return new PlaidDeltaIngestionResultDto(
            result.RawStoredCount,
            result.RawDuplicateCount,
            result.InsertedCount,
            result.UpdatedCount,
            result.UnchangedCount,
            result.Items.Select(x => new PlaidDeltaIngestionItemResultDto(
                x.PlaidTransactionId,
                x.EnrichedTransactionId,
                x.RawDuplicate,
                x.Disposition.ToString(),
                x.ReviewStatus.ToString(),
                x.ReviewReason)).ToList());
    }
}
