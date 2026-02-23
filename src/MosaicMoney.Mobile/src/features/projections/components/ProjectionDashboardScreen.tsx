import { useEffect, useMemo, useState } from "react";
import {
  ActivityIndicator,
  RefreshControl,
  SafeAreaView,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from "react-native";
import { ProjectionDetailSection } from "./ProjectionDetailSection";
import { ProjectionListSection } from "./ProjectionListSection";
import { ProjectionSummarySection } from "./ProjectionSummarySection";
import { StatePanel } from "../../transactions/components/StatePanel";
import { formatUtcDateTime } from "../../transactions/utils/formatters";
import { useProjectionMetadata } from "../hooks/useProjectionMetadata";
import { useProjectionSummaryMetrics } from "../hooks/useProjectionSummaryMetrics";

export function ProjectionDashboardScreen() {
  const {
    items,
    isLoading,
    isRefreshing,
    isRetrying,
    isOfflineLikely,
    isStaleData,
    lastSuccessfulLoadAtUtc,
    error,
    refresh,
    retry,
  } = useProjectionMetadata({ pageSize: 100 });
  const metrics = useProjectionSummaryMetrics(items);
  const [selectedProjectionId, setSelectedProjectionId] = useState<string | undefined>(undefined);

  useEffect(() => {
    if (items.length === 0) {
      setSelectedProjectionId(undefined);
      return;
    }

    const selectedStillPresent = selectedProjectionId
      ? items.some((item) => item.id === selectedProjectionId)
      : false;

    if (!selectedStillPresent) {
      setSelectedProjectionId(items[0].id);
    }
  }, [items, selectedProjectionId]);

  const selectedProjection = useMemo(
    () => items.find((item) => item.id === selectedProjectionId) ?? items[0],
    [items, selectedProjectionId],
  );

  const lastUpdatedAt = useMemo(() => {
    if (items.length === 0) {
      return undefined;
    }

    return items.reduce<string>((latest, current) => {
      if (!latest) {
        return current.lastModifiedAtUtc;
      }

      return current.lastModifiedAtUtc > latest ? current.lastModifiedAtUtc : latest;
    }, "");
  }, [items]);

  if (isLoading && items.length === 0) {
    return (
      <SafeAreaView style={styles.page}>
        <ScrollView contentContainerStyle={styles.loadingContent}>
          <View style={styles.loadingHeader}>
            <ActivityIndicator size="large" color="#1849a9" />
            <Text style={styles.loadingText}>Loading projection dashboard...</Text>
          </View>

          <ProjectionLoadingCard />
          <ProjectionLoadingCard />
          <ProjectionLoadingCard />
          <ProjectionLoadingCard />
        </ScrollView>
      </SafeAreaView>
    );
  }

  if (error && items.length === 0) {
    return (
      <SafeAreaView style={styles.page}>
        <StatePanel
          title={isOfflineLikely ? "Offline: projection data unavailable" : "Unable to load dashboard"}
          body={
            isOfflineLikely
              ? "No cached projection snapshot is available right now. Reconnect and retry to load read-only projections."
              : error
          }
          actionLabel={isRetrying ? "Retrying..." : "Retry"}
          onAction={() => {
            void retry();
          }}
          disabled={isRetrying}
        />
      </SafeAreaView>
    );
  }

  if (items.length === 0) {
    return (
      <SafeAreaView style={styles.page}>
        <StatePanel
          title="No projection data yet"
          body="Connect transactions and pull to refresh to view read-only projection summaries."
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
      <ScrollView
        contentInsetAdjustmentBehavior="automatic"
        contentContainerStyle={styles.scrollContent}
        refreshControl={<RefreshControl refreshing={isRefreshing} onRefresh={() => void refresh()} />}
      >
        <View style={styles.headerContainer}>
          <Text style={styles.heading}>Dashboard</Text>
          <Text style={styles.subheading}>
            Read-only projection summary built from backend metadata. Ledger source values remain unchanged.
          </Text>
          {lastUpdatedAt ? <Text style={styles.timestamp}>Metadata updated: {formatUtcDateTime(lastUpdatedAt)}</Text> : null}
          {lastSuccessfulLoadAtUtc ? (
            <Text style={styles.timestamp}>Last successful sync: {formatUtcDateTime(lastSuccessfulLoadAtUtc)}</Text>
          ) : null}

          {isRefreshing || isRetrying ? (
            <View style={[styles.statusBanner, styles.statusBannerInfo]}>
              <Text style={styles.statusBannerTextInfo}>Refreshing projection metadata in the background...</Text>
            </View>
          ) : null}

          {isStaleData && error ? (
            <View style={[styles.statusBanner, styles.statusBannerWarning]}>
              <Text style={styles.statusBannerTextWarning}>
                {isOfflineLikely
                  ? "You appear to be offline. Showing the last synced projection snapshot."
                  : `Showing stale projection data while refresh is pending. ${error}`}
              </Text>
            </View>
          ) : null}

          {!isStaleData && error ? <Text style={styles.warning}>Refresh warning: {error}</Text> : null}
        </View>

        <ProjectionSummarySection metrics={metrics} transactionCount={items.length} />
        <ProjectionListSection
          items={items}
          selectedProjectionId={selectedProjectionId}
          onSelect={setSelectedProjectionId}
        />
        {selectedProjection ? <ProjectionDetailSection projection={selectedProjection} /> : null}
      </ScrollView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  page: {
    backgroundColor: "#f2f4f7",
    flex: 1,
  },
  loadingContent: {
    paddingBottom: 24,
    paddingHorizontal: 16,
    paddingTop: 24,
    rowGap: 10,
  },
  loadingHeader: {
    alignItems: "center",
    marginBottom: 8,
  },
  loadingText: {
    color: "#344054",
    marginTop: 12,
  },
  scrollContent: {
    paddingBottom: 24,
    paddingHorizontal: 16,
  },
  headerContainer: {
    marginBottom: 12,
    marginTop: 8,
    rowGap: 6,
  },
  heading: {
    color: "#101828",
    fontSize: 24,
    fontWeight: "800",
  },
  subheading: {
    color: "#475467",
    fontSize: 14,
    lineHeight: 20,
  },
  timestamp: {
    color: "#667085",
    fontSize: 12,
  },
  warning: {
    color: "#b42318",
    fontSize: 13,
  },
  statusBanner: {
    borderRadius: 10,
    borderWidth: 1,
    marginTop: 8,
    paddingHorizontal: 10,
    paddingVertical: 8,
  },
  statusBannerInfo: {
    backgroundColor: "#eff8ff",
    borderColor: "#b2ddff",
  },
  statusBannerWarning: {
    backgroundColor: "#fffaeb",
    borderColor: "#fecd89",
  },
  statusBannerTextInfo: {
    color: "#175cd3",
    fontSize: 12,
    fontWeight: "600",
  },
  statusBannerTextWarning: {
    color: "#b54708",
    fontSize: 12,
    fontWeight: "600",
  },
  loadingCard: {
    backgroundColor: "#ffffff",
    borderColor: "#d8dee8",
    borderRadius: 12,
    borderWidth: 1,
    paddingHorizontal: 12,
    paddingVertical: 12,
  },
  loadingLineWide: {
    backgroundColor: "#eaecf0",
    borderRadius: 8,
    height: 14,
    width: "80%",
  },
  loadingLineMedium: {
    backgroundColor: "#eaecf0",
    borderRadius: 8,
    height: 12,
    marginTop: 10,
    width: "55%",
  },
  loadingLineShort: {
    backgroundColor: "#eaecf0",
    borderRadius: 8,
    height: 12,
    marginTop: 10,
    width: "40%",
  },
});

function ProjectionLoadingCard() {
  return (
    <View style={styles.loadingCard}>
      <View style={styles.loadingLineWide} />
      <View style={styles.loadingLineMedium} />
      <View style={styles.loadingLineShort} />
    </View>
  );
}
