import { ActivityIndicator, RefreshControl, SafeAreaView, ScrollView, StyleSheet, Text, View } from "react-native";
import { useMemo } from "react";
import { Pie, PolarChart } from "victory-native";
import { PrimarySurfaceNav } from "../../../shared/components/PrimarySurfaceNav";
import { theme } from "../../../theme/tokens";
import { useProjectionMetadata } from "../../projections/hooks/useProjectionMetadata";
import { formatCurrency, formatLedgerDate } from "../../transactions/utils/formatters";
import { StatePanel } from "../../transactions/components/StatePanel";

const CHART_COLORS = [
  theme.colors.primary,
  theme.colors.positive,
  theme.colors.warning,
  theme.colors.negative,
  theme.colors.textMain,
];

export function RecurringsOverviewScreen() {
  const { items, isLoading, isRefreshing, isRetrying, error, refresh, retry } = useProjectionMetadata({ pageSize: 100 });

  const recurringItems = useMemo(
    () =>
      items
        .filter((item) => item.recurring.isLinked)
        .sort((a, b) => (a.recurring.nextDueDate || "") < (b.recurring.nextDueDate || "") ? -1 : 1),
    [items],
  );

  const frequencySummaries = useMemo(() => {
    const buckets = new Map<string, { id: string; amount: number; color: string }>();
    let colorIndex = 0;

    for (const item of recurringItems) {
      const freq = item.recurring.frequency || "Unknown";
      const existing = buckets.get(freq) ?? {
        id: freq,
        amount: 0,
        color: CHART_COLORS[colorIndex++ % CHART_COLORS.length],
      };
      existing.amount += Math.abs(item.rawAmount);
      buckets.set(freq, existing);
    }

    return [...buckets.values()].sort((a, b) => b.amount - a.amount);
  }, [recurringItems]);

  if (isLoading && items.length === 0) {
    return (
      <SafeAreaView style={styles.page}>
        <View style={styles.centered}>
          <ActivityIndicator size="large" color={theme.colors.primary} />
          <Text style={styles.loadingText}>Loading recurrings...</Text>
        </View>
      </SafeAreaView>
    );
  }

  if (error && items.length === 0) {
    return (
      <SafeAreaView style={styles.page}>
        <StatePanel
          title="Unable to load recurring items"
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
        <Text style={styles.heading}>Recurrings</Text>
        <Text style={styles.subheading}>Recurring-linked projection items with due-date and amount context.</Text>
        <PrimarySurfaceNav />

        {frequencySummaries.length > 0 && (
          <View style={styles.chartContainer}>
            <PolarChart
              data={frequencySummaries as { id: string; amount: number; color: string }[]}
              colorKey={"color"}
              valueKey={"amount"}
              labelKey={"id"}
            >
              <Pie.Chart />
            </PolarChart>
          </View>
        )}

        {recurringItems.length === 0 ? (
          <StatePanel title="No recurring items" body="No recurring-linked projection rows are available yet." />
        ) : (
          recurringItems.map((item) => (
            <View key={item.id} style={styles.card}>
              <Text style={styles.description}>{item.description}</Text>
              <Text style={styles.meta}>Due: {item.recurring.nextDueDate ? formatLedgerDate(item.recurring.nextDueDate) : "Unknown"}</Text>
              <Text style={styles.meta}>Frequency: {item.recurring.frequency || "Unknown"}</Text>
              <Text style={styles.amount}>{formatCurrency(item.rawAmount)}</Text>
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
  chartContainer: {
    height: 300,
    marginTop: 16,
    marginBottom: 8,
  },
  card: {
    backgroundColor: theme.colors.surface,
    borderColor: theme.colors.border,
    borderWidth: 1,
    borderRadius: theme.borderRadius.lg,
    marginTop: 10,
    padding: 14,
  },
  description: {
    color: theme.colors.textMain,
    fontSize: 15,
    fontWeight: "700",
  },
  meta: {
    color: theme.colors.textMuted,
    fontSize: 12,
    marginTop: 4,
  },
  amount: {
    color: theme.colors.warning,
    fontFamily: theme.typography.mono.fontFamily,
    fontSize: 18,
    fontWeight: "800",
    marginTop: 10,
  },
});
