import { StyleSheet, Text, View } from "react-native";
import type { ProjectionSummaryMetrics } from "../hooks/useProjectionSummaryMetrics";
import { formatCurrency } from "../../transactions/utils/formatters";

interface ProjectionSummarySectionProps {
  metrics: ProjectionSummaryMetrics;
  transactionCount: number;
}

interface SummaryMetricCardProps {
  label: string;
  value: string;
  hint?: string;
}

function SummaryMetricCard({ label, value, hint }: SummaryMetricCardProps) {
  return (
    <View style={styles.metricCard}>
      <Text style={styles.metricLabel}>{label}</Text>
      <Text style={styles.metricValue}>{value}</Text>
      {hint ? <Text style={styles.metricHint}>{hint}</Text> : null}
    </View>
  );
}

export function ProjectionSummarySection({ metrics, transactionCount }: ProjectionSummarySectionProps) {
  return (
    <View style={styles.section}>
      <Text style={styles.sectionTitle}>Core Summary</Text>
      <Text style={styles.sectionBody}>
        Read-only totals computed from projection metadata for {transactionCount} loaded transactions.
      </Text>

      <SummaryMetricCard
        label="Total liquidity"
        value={formatCurrency(metrics.totalLiquidity)}
        hint="Net across loaded transactions"
      />
      <SummaryMetricCard
        label="Household budget burn"
        value={formatCurrency(metrics.householdBudgetBurn)}
        hint="Expenses included in household budget"
      />
      <SummaryMetricCard
        label="Business expenses"
        value={formatCurrency(metrics.businessExpenses)}
        hint="Expenses excluded from household budget"
      />
      <SummaryMetricCard
        label="Pending reimbursements"
        value={formatCurrency(metrics.pendingOrNeedsReviewReimbursements)}
        hint="Pending or needs-review reimbursement totals"
      />
      <SummaryMetricCard
        label="Approved reimbursements"
        value={formatCurrency(metrics.approvedReimbursements)}
        hint="Approved reimbursement totals"
      />
      <SummaryMetricCard
        label="Amortized splits"
        value={String(metrics.amortizedSplitCount)}
        hint="Splits amortized over multiple months"
      />
    </View>
  );
}

const styles = StyleSheet.create({
  section: {
    marginTop: 8,
  },
  sectionTitle: {
    color: "#101828",
    fontSize: 20,
    fontWeight: "800",
  },
  sectionBody: {
    color: "#475467",
    fontSize: 13,
    lineHeight: 20,
    marginBottom: 10,
    marginTop: 4,
  },
  metricCard: {
    backgroundColor: "#ffffff",
    borderColor: "#d5d9e0",
    borderRadius: 12,
    borderWidth: 1,
    marginBottom: 10,
    paddingHorizontal: 14,
    paddingVertical: 12,
  },
  metricLabel: {
    color: "#475467",
    fontSize: 12,
    fontWeight: "700",
    textTransform: "uppercase",
  },
  metricValue: {
    color: "#101828",
    fontSize: 24,
    fontWeight: "800",
    marginTop: 6,
  },
  metricHint: {
    color: "#667085",
    fontSize: 12,
    marginTop: 4,
  },
});
