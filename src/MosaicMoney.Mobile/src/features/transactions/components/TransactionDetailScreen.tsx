import {
  ActivityIndicator,
  RefreshControl,
  SafeAreaView,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from "react-native";
import { useTransactionDetail } from "../hooks/useTransactionDetail";
import { formatCurrency, formatLedgerDate, formatUtcDateTime } from "../utils/formatters";
import { StatePanel } from "./StatePanel";

interface TransactionDetailScreenProps {
  transactionId: string | null | undefined;
}

function LedgerRow({ label, value }: { label: string; value: string }) {
  return (
    <View style={styles.ledgerRow}>
      <Text style={styles.ledgerLabel}>{label}</Text>
      <Text style={styles.ledgerValue}>{value}</Text>
    </View>
  );
}

function NoteSection({ title, content, accentColor }: { title: string; content?: string; accentColor: string }) {
  return (
    <View style={[styles.noteCard, { borderLeftColor: accentColor }]}> 
      <Text style={styles.noteTitle}>{title}</Text>
      <Text style={styles.noteBody}>{content?.trim() ? content : "No note recorded."}</Text>
    </View>
  );
}

export function TransactionDetailScreen({ transactionId }: TransactionDetailScreenProps) {
  const { transaction, isLoading, isRefreshing, isNotFound, error, refresh, retry } = useTransactionDetail(transactionId);

  if (isLoading && !transaction) {
    return (
      <SafeAreaView style={styles.centeredPage}>
        <ActivityIndicator size="large" color="#1849a9" />
        <Text style={styles.loadingText}>Loading transaction detail...</Text>
      </SafeAreaView>
    );
  }

  if (isNotFound) {
    return (
      <SafeAreaView style={styles.page}>
        <StatePanel
          title="Transaction not found"
          body="This transaction no longer exists or is unavailable in the current ledger view."
          actionLabel="Reload"
          onAction={() => {
            void retry();
          }}
        />
      </SafeAreaView>
    );
  }

  if (error && !transaction) {
    return (
      <SafeAreaView style={styles.page}>
        <StatePanel
          title="Unable to load transaction"
          body={error}
          actionLabel="Try again"
          onAction={() => {
            void retry();
          }}
        />
      </SafeAreaView>
    );
  }

  if (!transaction) {
    return (
      <SafeAreaView style={styles.page}>
        <StatePanel title="No transaction selected" body="Select an item from NeedsReview queue." />
      </SafeAreaView>
    );
  }

  return (
    <SafeAreaView style={styles.page}>
      <ScrollView
        contentContainerStyle={styles.content}
        refreshControl={<RefreshControl refreshing={isRefreshing} onRefresh={() => void refresh()} />}
      >
        <View style={styles.headerCard}>
          <Text style={styles.title}>{transaction.description}</Text>
          <Text style={styles.amount}>{formatCurrency(transaction.amount)}</Text>
          <Text style={styles.readOnlyHint}>Ledger values below are read-only source-of-truth fields.</Text>
        </View>

        <View style={styles.sectionCard}>
          <Text style={styles.sectionTitle}>Ledger Truth</Text>
          <LedgerRow label="Transaction Date" value={formatLedgerDate(transaction.transactionDate)} />
          <LedgerRow
            label="Review Status"
            value={transaction.reviewStatus === "NeedsReview" ? "Pending Review (NeedsReview)" : transaction.reviewStatus}
          />
          <LedgerRow label="Review Reason" value={transaction.reviewReason || "None"} />
          <LedgerRow label="Account Id" value={transaction.accountId} />
          <LedgerRow label="Transaction Id" value={transaction.id} />
          <LedgerRow label="Exclude From Budget" value={transaction.excludeFromBudget ? "Yes" : "No"} />
          <LedgerRow label="Extra Principal" value={transaction.isExtraPrincipal ? "Yes" : "No"} />
          <LedgerRow label="Created" value={formatUtcDateTime(transaction.createdAtUtc)} />
          <LedgerRow label="Last Modified" value={formatUtcDateTime(transaction.lastModifiedAtUtc)} />
        </View>

        <View style={styles.sectionCard}>
          <Text style={styles.sectionTitle}>Dual Notes</Text>
          <NoteSection title="UserNote" content={transaction.userNote} accentColor="#175cd3" />
          <NoteSection title="AgentNote" content={transaction.agentNote} accentColor="#087443" />
        </View>
      </ScrollView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  page: {
    backgroundColor: "#f2f4f7",
    flex: 1,
  },
  centeredPage: {
    alignItems: "center",
    backgroundColor: "#f2f4f7",
    flex: 1,
    justifyContent: "center",
  },
  loadingText: {
    color: "#344054",
    marginTop: 12,
  },
  content: {
    padding: 16,
    rowGap: 12,
  },
  headerCard: {
    backgroundColor: "#ffffff",
    borderColor: "#d8dee8",
    borderRadius: 12,
    borderWidth: 1,
    padding: 16,
  },
  title: {
    color: "#101828",
    fontSize: 22,
    fontWeight: "800",
  },
  amount: {
    color: "#0f1728",
    fontSize: 24,
    fontWeight: "700",
    marginTop: 8,
  },
  readOnlyHint: {
    color: "#475467",
    fontSize: 13,
    marginTop: 10,
  },
  sectionCard: {
    backgroundColor: "#ffffff",
    borderColor: "#d8dee8",
    borderRadius: 12,
    borderWidth: 1,
    padding: 16,
  },
  sectionTitle: {
    color: "#101828",
    fontSize: 18,
    fontWeight: "700",
    marginBottom: 12,
  },
  ledgerRow: {
    borderBottomColor: "#eef2f6",
    borderBottomWidth: 1,
    marginBottom: 10,
    paddingBottom: 10,
  },
  ledgerLabel: {
    color: "#475467",
    fontSize: 12,
    fontWeight: "700",
    textTransform: "uppercase",
  },
  ledgerValue: {
    color: "#101828",
    fontSize: 14,
    marginTop: 4,
  },
  noteCard: {
    backgroundColor: "#fcfdff",
    borderColor: "#d8dee8",
    borderLeftWidth: 4,
    borderRadius: 10,
    borderWidth: 1,
    marginBottom: 10,
    padding: 12,
  },
  noteTitle: {
    color: "#101828",
    fontSize: 13,
    fontWeight: "700",
    marginBottom: 6,
  },
  noteBody: {
    color: "#344054",
    fontSize: 14,
    lineHeight: 20,
  },
});
