import { ActivityIndicator, RefreshControl, SafeAreaView, ScrollView, StyleSheet, Text, View } from "react-native";
import { useMemo } from "react";
import { PrimarySurfaceNav } from "../../../shared/components/PrimarySurfaceNav";
import { theme } from "../../../theme/tokens";
import { useProjectionMetadata } from "../../projections/hooks/useProjectionMetadata";
import { formatCurrency } from "../../transactions/utils/formatters";
import { StatePanel } from "../../transactions/components/StatePanel";

interface CategorySummary {
  id: string;
  amount: number;
  transactionCount: number;
}

export function CategoriesOverviewScreen() {
  const { items, isLoading, isRefreshing, isRetrying, error, refresh, retry } = useProjectionMetadata({ pageSize: 100 });

  const categorySummaries = useMemo<CategorySummary[]>(() => {
    const buckets = new Map<string, CategorySummary>();

    for (const item of items) {
      if (item.splits.length === 0) {
        const existing = buckets.get("Uncategorized") ?? {
          id: "Uncategorized",
          amount: 0,
          transactionCount: 0,
        };
        existing.amount += Math.abs(item.rawAmount);
        existing.transactionCount += 1;
        buckets.set("Uncategorized", existing);
        continue;
      }

      for (const split of item.splits) {
        const key = split.subcategoryId || "Uncategorized";
        const existing = buckets.get(key) ?? { id: key, amount: 0, transactionCount: 0 };
        existing.amount += Math.abs(split.rawAmount);
        existing.transactionCount += 1;
        buckets.set(key, existing);
      }
    }

    return [...buckets.values()].sort((a, b) => b.amount - a.amount);
  }, [items]);

  if (isLoading && items.length === 0) {
    return (
      <SafeAreaView style={styles.page}>
        <View style={styles.centered}>
          <ActivityIndicator size="large" color={theme.colors.primary} />
          <Text style={styles.loadingText}>Loading categories...</Text>
        </View>
      </SafeAreaView>
    );
  }

  if (error && items.length === 0) {
    return (
      <SafeAreaView style={styles.page}>
        <StatePanel
          title="Unable to load categories"
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
        <Text style={styles.heading}>Categories</Text>
        <Text style={styles.subheading}>Split and uncategorized spending rollups aligned with web budgeting surface.</Text>
        <PrimarySurfaceNav />

        {categorySummaries.length === 0 ? (
          <StatePanel title="No categories" body="No categorized projection data is available yet." />
        ) : (
          categorySummaries.map((category) => (
            <View key={category.id} style={styles.card}>
              <Text style={styles.categoryTitle}>{category.id}</Text>
              <Text style={styles.categoryMeta}>{category.transactionCount} mapped splits/transactions</Text>
              <Text style={styles.spentAmount}>{formatCurrency(category.amount)}</Text>
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
  categoryTitle: {
    color: theme.colors.textMain,
    fontSize: 15,
    fontWeight: "700",
  },
  categoryMeta: {
    color: theme.colors.textMuted,
    fontSize: 12,
    marginTop: 4,
  },
  spentAmount: {
    color: theme.colors.warning,
    fontFamily: theme.typography.mono.fontFamily,
    fontSize: 20,
    fontWeight: "800",
    marginTop: 10,
  },
});
