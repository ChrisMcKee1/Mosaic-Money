import { ActivityIndicator, Pressable, RefreshControl, SafeAreaView, ScrollView, StyleSheet, Text, View } from "react-native";
import { useMemo } from "react";
import { useRouter } from "expo-router";
import { PrimarySurfaceNav } from "../../../shared/components/PrimarySurfaceNav";
import { theme } from "../../../theme/tokens";
import { useProjectionMetadata } from "../../projections/hooks/useProjectionMetadata";
import { useHouseholdAccountAccess } from "../../settings/hooks/useAccountAccess";
import { formatCurrency } from "../../transactions/utils/formatters";
import { StatePanel } from "../../transactions/components/StatePanel";

export function AccountsOverviewScreen() {
  const router = useRouter();
  const { items, isLoading, isRefreshing, isRetrying, error, refresh, retry } = useProjectionMetadata({ pageSize: 100 });
  const { getAccountAccessPolicy, isLoadingAccess, refresh: refreshAccess } = useHouseholdAccountAccess();

  const accountSummaries = useMemo(() => {
    const buckets = new Map<string, { accountId: string; txCount: number; netAmount: number; isReadOnly: boolean; isHidden: boolean }>();

    for (const item of items) {
      const policy = getAccountAccessPolicy(item.accountId);
      const isHidden = policy.visibility === "Hidden" || policy.role === "None";
      
      if (isHidden) continue;

      const existing = buckets.get(item.accountId) ?? { 
        accountId: item.accountId, 
        txCount: 0, 
        netAmount: 0,
        isReadOnly: policy.role === "ReadOnly",
        isHidden: false
      };
      existing.txCount += 1;
      existing.netAmount += item.rawAmount;
      buckets.set(item.accountId, existing);
    }

    return [...buckets.values()].sort((a, b) => Math.abs(b.netAmount) - Math.abs(a.netAmount));
  }, [getAccountAccessPolicy, items]);

  if ((isLoading || isLoadingAccess) && items.length === 0) {
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
        refreshControl={<RefreshControl refreshing={isRefreshing || isLoadingAccess} onRefresh={() => void Promise.all([refresh(), refreshAccess()])} />}
      >
        <Text style={styles.heading}>Accounts</Text>
        <Text style={styles.subheading}>Projection-derived account rollups matching web account summary intent.</Text>
        <PrimarySurfaceNav />
        <Pressable
          accessibilityRole="button"
          onPress={() => router.push("/onboarding/plaid")}
          style={({ pressed }) => [styles.addAccountButton, pressed && styles.addAccountButtonPressed]}
        >
          <Text style={styles.addAccountButtonText}>Add Account (Plaid)</Text>
        </Pressable>

        {accountSummaries.length === 0 ? (
          <StatePanel title="No accounts" body="No account-linked transaction projections are available." />
        ) : (
          accountSummaries.map((account) => (
            <View key={account.accountId} style={styles.card}>
              <View style={styles.cardHeader}>
                <Text style={styles.accountLabel}>Account {account.accountId.slice(0, 8)}</Text>
                {account.isReadOnly && (
                  <View style={styles.readOnlyBadge}>
                    <Text style={styles.readOnlyText}>Read-Only</Text>
                  </View>
                )}
              </View>
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
  addAccountButton: {
    alignSelf: "flex-start",
    backgroundColor: theme.colors.surface,
    borderColor: theme.colors.border,
    borderRadius: 8,
    borderWidth: 1,
    marginTop: 12,
    paddingHorizontal: 12,
    paddingVertical: 8,
  },
  addAccountButtonPressed: {
    backgroundColor: theme.colors.surfaceHover,
  },
  addAccountButtonText: {
    color: theme.colors.primary,
    fontSize: 13,
    fontWeight: "700",
  },
  card: {
    backgroundColor: theme.colors.surface,
    borderColor: theme.colors.border,
    borderWidth: 1,
    borderRadius: theme.borderRadius.lg,
    marginTop: 10,
    padding: 14,
  },
  cardHeader: {
    alignItems: "center",
    flexDirection: "row",
    justifyContent: "space-between",
  },
  accountLabel: {
    color: theme.colors.textMain,
    fontSize: 15,
    fontWeight: "700",
  },
  readOnlyBadge: {
    backgroundColor: theme.colors.surfaceHover,
    borderColor: theme.colors.border,
    borderRadius: 12,
    borderWidth: 1,
    paddingHorizontal: 8,
    paddingVertical: 2,
  },
  readOnlyText: {
    color: theme.colors.textMuted,
    fontSize: 10,
    fontWeight: "600",
    textTransform: "uppercase",
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
