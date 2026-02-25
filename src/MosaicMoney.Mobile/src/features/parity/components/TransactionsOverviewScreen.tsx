import { ActivityIndicator, RefreshControl, SafeAreaView, ScrollView, StyleSheet, Text, View, TextInput } from "react-native";
import { useMemo, useState, useEffect } from "react";
import { PrimarySurfaceNav } from "../../../shared/components/PrimarySurfaceNav";
import { theme } from "../../../theme/tokens";
import { useProjectionMetadata } from "../../projections/hooks/useProjectionMetadata";
import { formatCurrency, formatLedgerDate } from "../../transactions/utils/formatters";
import { StatePanel } from "../../transactions/components/StatePanel";
import { searchTransactions } from "../../transactions/services/mobileTransactionsApi";
import type { TransactionDto } from "../../transactions/contracts";

export function TransactionsOverviewScreen() {
  const { items, isLoading, isRefreshing, isRetrying, error, refresh, retry } = useProjectionMetadata({ pageSize: 100 });
  const [searchQuery, setSearchQuery] = useState("");
  const [searchResults, setSearchResults] = useState<TransactionDto[] | null>(null);
  const [isSearching, setIsSearching] = useState(false);

  useEffect(() => {
    const normalizedQuery = searchQuery.trim();
    if (!normalizedQuery) {
      setSearchResults(null);
      setIsSearching(false);
      return;
    }

    setIsSearching(true);
    const abortController = new AbortController();

    const timeoutId = setTimeout(async () => {
      try {
        const data = await searchTransactions(normalizedQuery, 20, abortController.signal);
        setSearchResults(data);
      } catch (e) {
        if (e instanceof Error && e.name === "AbortError") return;
        console.error("Transaction search failed", e);
        setSearchResults([]);
      } finally {
        setIsSearching(false);
      }
    }, 300);

    return () => {
      clearTimeout(timeoutId);
      abortController.abort();
    };
  }, [searchQuery]);

  const sortedItems = useMemo(() => {
    if (searchResults !== null) {
      return searchResults.map(tx => ({
        id: tx.id,
        description: tx.description,
        rawAmount: tx.amount,
        rawTransactionDate: tx.transactionDate,
        reviewStatus: tx.reviewStatus,
      }));
    }
    return [...items].sort((a, b) => (a.rawTransactionDate < b.rawTransactionDate ? 1 : -1));
  }, [items, searchResults]);

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

        <View style={styles.searchContainer}>
          <TextInput
            style={styles.searchInput}
            placeholder="Search transactions..."
            placeholderTextColor={theme.colors.textMuted}
            value={searchQuery}
            onChangeText={setSearchQuery}
          />
          {isSearching && (
            <ActivityIndicator size="small" color={theme.colors.primary} style={styles.searchSpinner} />
          )}
        </View>

        {sortedItems.length === 0 ? (
          <StatePanel title="No transactions" body={searchResults !== null ? "No transactions match your search." : "No projection transactions available yet."} />
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
  searchContainer: {
    flexDirection: "row",
    alignItems: "center",
    backgroundColor: theme.colors.surface,
    borderWidth: 1,
    borderColor: theme.colors.border,
    borderRadius: 8,
    marginTop: 16,
    minHeight: 44,
    position: "relative",
  },
  searchInput: {
    flex: 1,
    paddingHorizontal: 12,
    paddingVertical: 10,
    color: theme.colors.textMain,
    fontSize: 16,
  },
  searchSpinner: {
    position: "absolute",
    right: 12,
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
