import { useCallback } from "react";
import {
  ActivityIndicator,
  FlatList,
  RefreshControl,
  SafeAreaView,
  StyleSheet,
  Text,
  View,
} from "react-native";
import { useRouter } from "expo-router";
import type { TransactionDto } from "../contracts";
import { useNeedsReviewQueue } from "../hooks/useNeedsReviewQueue";
import { NeedsReviewQueueItem } from "./NeedsReviewQueueItem";
import { StatePanel } from "./StatePanel";

export function NeedsReviewQueueScreen() {
  const router = useRouter();
  const { transactions, isLoading, isRefreshing, error, refresh, retry } = useNeedsReviewQueue();

  const openTransactionDetail = useCallback(
    (transactionId: string) => {
      router.push({
        pathname: "/transactions/[transactionId]",
        params: { transactionId },
      });
    },
    [router],
  );

  const renderItem = useCallback(
    ({ item }: { item: TransactionDto }) => (
      <NeedsReviewQueueItem transaction={item} onPress={openTransactionDetail} />
    ),
    [openTransactionDetail],
  );

  if (isLoading && transactions.length === 0) {
    return (
      <SafeAreaView style={styles.centeredPage}>
        <ActivityIndicator size="large" color="#1849a9" />
        <Text style={styles.loadingText}>Loading pending reviews...</Text>
      </SafeAreaView>
    );
  }

  if (error && transactions.length === 0) {
    return (
      <SafeAreaView style={styles.page}>
        <StatePanel
          title="Unable to load NeedsReview queue"
          body={error}
          actionLabel="Try again"
          onAction={() => {
            void retry();
          }}
        />
      </SafeAreaView>
    );
  }

  if (transactions.length === 0) {
    return (
      <SafeAreaView style={styles.page}>
        <StatePanel
          title="Queue is clear"
          body="No transactions currently require human review. Pull down to refresh for new items."
          actionLabel={isRefreshing ? "Refreshing..." : "Refresh"}
          onAction={() => {
            void refresh();
          }}
          disabled={isRefreshing}
        />
      </SafeAreaView>
    );
  }

  return (
    <SafeAreaView style={styles.page}>
      <FlatList
        data={transactions}
        keyExtractor={(item) => item.id}
        renderItem={renderItem}
        contentContainerStyle={styles.listContent}
        refreshControl={<RefreshControl refreshing={isRefreshing} onRefresh={() => void refresh()} />}
        ListHeaderComponent={
          <View style={styles.headerContainer}>
            <Text style={styles.heading}>NeedsReview Queue</Text>
            <Text style={styles.subheading}>Pending transactions waiting on explicit human decision.</Text>
            {error ? <Text style={styles.warning}>Refresh warning: {error}</Text> : null}
          </View>
        }
      />
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
  listContent: {
    paddingBottom: 24,
    paddingHorizontal: 16,
  },
  headerContainer: {
    marginBottom: 12,
    marginTop: 8,
  },
  heading: {
    color: "#101828",
    fontSize: 24,
    fontWeight: "800",
  },
  subheading: {
    color: "#475467",
    fontSize: 14,
    marginTop: 6,
  },
  warning: {
    color: "#b42318",
    fontSize: 13,
    marginTop: 8,
  },
});
