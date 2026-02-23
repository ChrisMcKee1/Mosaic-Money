import { StyleSheet, Text, View } from "react-native";
import type { TransactionProjectionMetadataDto } from "../contracts";
import { formatCurrency, formatLedgerDate, formatUtcDateTime } from "../../transactions/utils/formatters";

interface ProjectionDetailSectionProps {
  projection: TransactionProjectionMetadataDto;
}

interface DetailRowProps {
  label: string;
  value: string;
}

function DetailRow({ label, value }: DetailRowProps) {
  return (
    <View style={styles.detailRow}>
      <Text style={styles.detailLabel}>{label}</Text>
      <Text style={styles.detailValue}>{value}</Text>
    </View>
  );
}

function buildReimbursementStatusText(reimbursement: TransactionProjectionMetadataDto["reimbursement"]): string {
  if (!reimbursement.hasProposals) {
    return "No proposals";
  }

  const latestStatus = reimbursement.latestStatus ?? "Unknown";
  if (reimbursement.latestStatusReasonCode) {
    return `${latestStatus} (${reimbursement.latestStatusReasonCode})`;
  }

  return latestStatus;
}

export function ProjectionDetailSection({ projection }: ProjectionDetailSectionProps) {
  return (
    <View style={styles.section}>
      <Text style={styles.sectionTitle}>Projection Detail</Text>
      <Text style={styles.sectionBody}>Read-only metadata for the selected transaction. No ledger mutation actions.</Text>

      <View style={styles.card}>
        <Text style={styles.cardTitle}>{projection.description}</Text>
        <DetailRow label="Transaction" value={projection.id} />
        <DetailRow label="Account" value={projection.accountId} />
        <DetailRow label="Raw amount" value={formatCurrency(projection.rawAmount)} />
        <DetailRow label="Raw date" value={formatLedgerDate(projection.rawTransactionDate)} />
        <DetailRow label="Review status" value={projection.reviewStatus} />
        <DetailRow label="Review reason" value={projection.reviewReason || "None"} />
        <DetailRow label="Exclude from budget" value={projection.excludeFromBudget ? "Yes" : "No"} />
        <DetailRow label="Extra principal" value={projection.isExtraPrincipal ? "Yes" : "No"} />
        <DetailRow label="Last modified" value={formatUtcDateTime(projection.lastModifiedAtUtc)} />
      </View>

      <View style={styles.card}>
        <Text style={styles.cardTitle}>Recurring Snapshot</Text>
        <DetailRow label="Linked" value={projection.recurring.isLinked ? "Yes" : "No"} />
        <DetailRow label="Recurring item" value={projection.recurring.recurringItemId || "None"} />
        <DetailRow label="Frequency" value={projection.recurring.frequency || "Unknown"} />
        <DetailRow
          label="Next due"
          value={projection.recurring.nextDueDate ? formatLedgerDate(projection.recurring.nextDueDate) : "Unknown"}
        />
      </View>

      <View style={styles.card}>
        <Text style={styles.cardTitle}>Reimbursement Snapshot</Text>
        <DetailRow label="Has proposals" value={projection.reimbursement.hasProposals ? "Yes" : "No"} />
        <DetailRow label="Proposal count" value={String(projection.reimbursement.proposalCount)} />
        <DetailRow
          label="Pending human review"
          value={projection.reimbursement.hasPendingHumanReview ? "Yes" : "No"}
        />
        <DetailRow label="Latest status" value={buildReimbursementStatusText(projection.reimbursement)} />
        <DetailRow
          label="Pending or review amount"
          value={formatCurrency(projection.reimbursement.pendingOrNeedsReviewAmount)}
        />
        <DetailRow label="Approved amount" value={formatCurrency(projection.reimbursement.approvedAmount)} />
      </View>

      <View style={styles.card}>
        <Text style={styles.cardTitle}>Split Projection Metadata</Text>
        {projection.splits.length === 0 ? (
          <Text style={styles.emptySplit}>No split metadata for this transaction.</Text>
        ) : (
          projection.splits.map((split) => (
            <View key={split.id} style={styles.splitRow}>
              <Text style={styles.splitId}>Split {split.id}</Text>
              <Text style={styles.splitMeta}>Amount: {formatCurrency(split.rawAmount)}</Text>
              <Text style={styles.splitMeta}>Amortization: {split.amortizationMonths} month(s)</Text>
              <Text style={styles.splitMeta}>Subcategory: {split.subcategoryId || "None"}</Text>
            </View>
          ))
        )}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  section: {
    marginBottom: 18,
    marginTop: 12,
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
  card: {
    backgroundColor: "#ffffff",
    borderColor: "#d8dee8",
    borderRadius: 12,
    borderWidth: 1,
    marginBottom: 10,
    paddingHorizontal: 12,
    paddingVertical: 12,
  },
  cardTitle: {
    color: "#101828",
    fontSize: 16,
    fontWeight: "700",
    marginBottom: 8,
  },
  detailRow: {
    borderBottomColor: "#eef2f6",
    borderBottomWidth: 1,
    marginBottom: 8,
    paddingBottom: 8,
  },
  detailLabel: {
    color: "#475467",
    fontSize: 11,
    fontWeight: "700",
    textTransform: "uppercase",
  },
  detailValue: {
    color: "#101828",
    fontSize: 14,
    marginTop: 3,
  },
  emptySplit: {
    color: "#667085",
    fontSize: 13,
  },
  splitRow: {
    borderColor: "#e4e7ec",
    borderRadius: 10,
    borderWidth: 1,
    marginBottom: 8,
    paddingHorizontal: 10,
    paddingVertical: 8,
  },
  splitId: {
    color: "#101828",
    fontSize: 13,
    fontWeight: "700",
  },
  splitMeta: {
    color: "#344054",
    fontSize: 12,
    marginTop: 3,
  },
});
