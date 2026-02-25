import { ActivityIndicator, RefreshControl, SafeAreaView, ScrollView, StyleSheet, Text, View } from "react-native";
import { useMemo } from "react";
import { PrimarySurfaceNav } from "../../../shared/components/PrimarySurfaceNav";
import { theme } from "../../../theme/tokens";
import { useProjectionMetadata } from "../../projections/hooks/useProjectionMetadata";
import { formatCurrency } from "../../transactions/utils/formatters";
import { StatePanel } from "../../transactions/components/StatePanel";

export function AccountsOverviewScreen() {
  const { items, isLoading, isRefreshing, isRetrying, error, refresh, retry } = useProjectionMetadata({ pageSize: 100 });

  const accountSummaries = useMemo(() => {
    const buckets = new Map<string, { accountId: string; txCount: number; netAmount: number }>();

    for (const item of items) {
      const existing = buckets.get(item.accountId) ?? { accountId: item.accountId, txCount: 0, netAmount: 0 };
      existing.txCount += 1;
      existing.netAmount += item.rawAmount;
      buckets.set(item.accountId, existing);
    }

    return [...buckets.values()].sort((a, b) => Math.abs(b.netAmount) - Math.abs(a.netAmount));
  }, [items]);

  if (isLoading && items.length === 0) {
    return (
      <SafeAreaView style={styles.page}>
        <View style={styles.centered}>
          <ActivityIndicator size="large" color={theme.colors.primary} />
          <Text style={styles.loadingText}>Loading accounts...</Text>
        </View>
      </SafeAreaView>
    );
  }

  if (error && items.length === 0) {
    return (
      <SafeAreaView style={styles.page}>
        <StatePanel
          title="Unable to load accounts"
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
        <Text style={styles.heading}>Accounts</Text>
        <Text style={styles.subheading}>Projection-derived account rollups matching web account summary intent.</Text>
        <PrimarySurfaceNav />

        {accountSummaries.length === 0 ? (
          <StatePanel title="No accounts" body="No account-linked transaction projections are available." />
        ) : (
          accountSummaries.map((account) => (
            <View key={account.accountId} style={styles.card}>
              <Text style={styles.accountLabel}>Account {account.accountId.slice(0, 8)}</Text>
              <Text style={styles.accountMeta}>{account.txCount} transactions loaded</Text>
              <Text style={[styles.balance, account.netAmount < 0 ? styles.negative : styles.positive]}>
                {formatCurrency(account.netAmount)}
              </Text>
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
    padding: 14,
  },
  accountLabel: {
    color: theme.colors.textMain,
    fontSize: 15,
    fontWeight: "700",
  },
  accountMeta: {
    color: theme.colors.textMuted,
    fontSize: 12,
    marginTop: 4,
  },
  balance: {
    fontFamily: theme.typography.mono.fontFamily,
    fontSize: 20,
    fontWeight: "800",
    marginTop: 10,
  },
  positive: {
    color: theme.colors.positive,
  },
  negative: {
    color: theme.colors.negative,
  },
});
