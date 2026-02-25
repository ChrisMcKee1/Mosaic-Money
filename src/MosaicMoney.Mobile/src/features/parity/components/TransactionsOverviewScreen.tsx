import { ActivityIndicator, RefreshControl, SafeAreaView, ScrollView, StyleSheet, Text, View } from "react-native";
import { useMemo } from "react";
import { PrimarySurfaceNav } from "../../../shared/components/PrimarySurfaceNav";
import { theme } from "../../../theme/tokens";
import { useProjectionMetadata } from "../../projections/hooks/useProjectionMetadata";
import { formatCurrency, formatLedgerDate } from "../../transactions/utils/formatters";
import { StatePanel } from "../../transactions/components/StatePanel";

export function TransactionsOverviewScreen() {
  const { items, isLoading, isRefreshing, isRetrying, error, refresh, retry } = useProjectionMetadata({ pageSize: 100 });

  const sortedItems = useMemo(
    () => [...items].sort((a, b) => (a.rawTransactionDate < b.rawTransactionDate ? 1 : -1)),
    [items],
  );

  if (isLoading && items.length === 0) {
    return (
      <SafeAreaView style={styles.page}>
        <View style={styles.centered}>
          <ActivityIndicator size="large" color={theme.colors.primary} />
          <Text style={styles.loadingText}>Loading transactions...</Text>
        </View>
      </SafeAreaView>
    );
  }

  if (error && items.length === 0) {
    return (
      <SafeAreaView style={styles.page}>
        <StatePanel
          title="Unable to load transactions"
          body={error}
          actionLabel={isRetrying ? "Retrying..." : "Retry"}
          onAction={() => {
            void retry();
          }}
          disabled={isRetrying}
        />
      </SafeAreaView>
    );
  }

  return (
    <SafeAreaView style={styles.page}>
      <ScrollView
        contentContainerStyle={styles.content}
        refreshControl={<RefreshControl refreshing={isRefreshing} onRefresh={() => void refresh()} />}
      >
        <Text style={styles.heading}>Transactions</Text>
        <Text style={styles.subheading}>Projection-backed transaction feed with mobile parity to web surface.</Text>
        <PrimarySurfaceNav />

        {sortedItems.length === 0 ? (
          <StatePanel title="No transactions" body="No projection transactions available yet." />
        ) : (
          sortedItems.map((item) => (
            <View key={item.id} style={styles.card}>
              <View style={styles.rowTop}>
                <Text style={styles.description} numberOfLines={2}>
                  {item.description}
                </Text>
                <Text style={[styles.amount, item.rawAmount < 0 ? styles.amountNegative : styles.amountPositive]}>
                  {formatCurrency(item.rawAmount)}
                </Text>
              </View>

              <View style={styles.metaRow}>
                <Text style={styles.metaText}>{formatLedgerDate(item.rawTransactionDate)}</Text>
                <Text style={styles.metaText}>Review: {item.reviewStatus}</Text>
              </View>
            </View>
          ))
        )}
      </ScrollView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  page: {
    backgroundColor: theme.colors.background,
    flex: 1,
  },
  centered: {
    alignItems: "center",
    flex: 1,
    justifyContent: "center",
  },
  loadingText: {
    color: theme.colors.textMuted,
    marginTop: 12,
  },
  content: {
    paddingHorizontal: 16,
    paddingBottom: 24,
  },
  heading: {
    color: theme.colors.textMain,
    fontSize: 24,
    fontWeight: "800",
    letterSpacing: -0.5,
    marginTop: 8,
  },
  subheading: {
    color: theme.colors.textMuted,
    fontSize: 14,
    marginTop: 6,
  },
  card: {
    backgroundColor: theme.colors.surface,
    borderColor: theme.colors.border,
    borderWidth: 1,
    borderRadius: theme.borderRadius.lg,
    marginTop: 10,
    padding: 12,
  },
  rowTop: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "flex-start",
  },
  description: {
    color: theme.colors.textMain,
    flex: 1,
    fontSize: 15,
    fontWeight: "700",
    marginRight: 8,
  },
  amount: {
    fontFamily: theme.typography.mono.fontFamily,
    fontSize: 14,
    fontWeight: "700",
  },
  amountNegative: {
    color: theme.colors.negative,
  },
  amountPositive: {
    color: theme.colors.positive,
  },
  metaRow: {
    flexDirection: "row",
    justifyContent: "space-between",
    marginTop: 8,
  },
  metaText: {
    color: theme.colors.textMuted,
    fontSize: 12,
  },
});
