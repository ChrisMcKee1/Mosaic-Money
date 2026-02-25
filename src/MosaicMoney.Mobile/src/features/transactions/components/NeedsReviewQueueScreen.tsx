import { useCallback } from "react";
import {
  ActivityIndicator,
  FlatList,
  Pressable,
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
import { theme } from "../../../theme/tokens";

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

  const openPlaidOnboarding = useCallback(() => {
    router.push("/onboarding/plaid");
  }, [router]);

  const renderItem = useCallback(
    ({ item }: { item: TransactionDto }) => (
      <NeedsReviewQueueItem transaction={item} onPress={openTransactionDetail} />
    ),
    [openTransactionDetail],
  );

  if (isLoading && transactions.length === 0) {
    return (
      <SafeAreaView style={styles.centeredPage}>
        <ActivityIndicator size="large" color={theme.colors.primary} />
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
            <Pressable
              accessibilityRole="button"
              onPress={openPlaidOnboarding}
              style={({ pressed }) => [styles.connectButton, pressed && styles.connectButtonPressed]}
            >
              <Text style={styles.connectButtonText}>Connect Bank (Plaid)</Text>
            </Pressable>
            {error ? <Text style={styles.warning}>Refresh warning: {error}</Text> : null}
          </View>
        }
      />
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  page: {
    backgroundColor: theme.colors.background,
    flex: 1,
  },
  centeredPage: {
    alignItems: "center",
    backgroundColor: theme.colors.background,
    flex: 1,
    justifyContent: "center",
  },
  loadingText: {
    color: theme.colors.textMuted,
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
    color: theme.colors.textMain,
    fontSize: 24,
    fontWeight: "800",
    letterSpacing: -0.5,
  },
  subheading: {
    color: theme.colors.textMuted,
    fontSize: 14,
    marginTop: 6,
  },
  warning: {
    color: theme.colors.warning,
    fontSize: 13,
    marginTop: 8,
  },
  connectButton: {
    alignSelf: "flex-start",
    backgroundColor: theme.colors.surface,
    borderColor: theme.colors.border,
    borderRadius: 8,
    borderWidth: 1,
    marginTop: 12,
    paddingHorizontal: 12,
    paddingVertical: 8,
  },
  connectButtonPressed: {
    backgroundColor: theme.colors.surfaceHover,
  },
  connectButtonText: {
    color: theme.colors.primary,
    fontSize: 13,
    fontWeight: "700",
  },
});
