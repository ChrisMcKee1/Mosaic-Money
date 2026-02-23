import { useMemo } from "react";
import type { TransactionProjectionMetadataDto } from "../contracts";

export interface ProjectionSummaryMetrics {
  totalLiquidity: number;
  householdBudgetBurn: number;
  businessExpenses: number;
  pendingOrNeedsReviewReimbursements: number;
  approvedReimbursements: number;
  amortizedSplitCount: number;
}

export function useProjectionSummaryMetrics(
  items: TransactionProjectionMetadataDto[],
): ProjectionSummaryMetrics {
  return useMemo(() => {
    return items.reduce<ProjectionSummaryMetrics>(
      (summary, transaction) => {
        summary.totalLiquidity += transaction.rawAmount;

        if (transaction.rawAmount < 0) {
          const absoluteAmount = Math.abs(transaction.rawAmount);
          if (transaction.excludeFromBudget) {
            summary.businessExpenses += absoluteAmount;
          } else {
            summary.householdBudgetBurn += absoluteAmount;
          }
        }

        summary.pendingOrNeedsReviewReimbursements += transaction.reimbursement.pendingOrNeedsReviewAmount;
        summary.approvedReimbursements += transaction.reimbursement.approvedAmount;

        summary.amortizedSplitCount += transaction.splits.filter((split) => split.amortizationMonths > 1).length;

        return summary;
      },
      {
        totalLiquidity: 0,
        householdBudgetBurn: 0,
        businessExpenses: 0,
        pendingOrNeedsReviewReimbursements: 0,
        approvedReimbursements: 0,
        amortizedSplitCount: 0,
      },
    );
  }, [items]);
}
