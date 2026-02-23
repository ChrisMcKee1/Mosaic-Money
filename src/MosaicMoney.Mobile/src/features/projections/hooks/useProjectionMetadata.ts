import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { MobileApiError } from "../../../shared/services/mobileApiClient";
import { toReadableError } from "../../../shared/services/mobileApiClient";
import type { ProjectionQueryOptions, TransactionProjectionMetadataDto } from "../contracts";
import { fetchProjectionMetadata } from "../services/mobileProjectionApi";

export interface ProjectionMetadataState {
  items: TransactionProjectionMetadataDto[];
  isLoading: boolean;
  isRefreshing: boolean;
  isRetrying: boolean;
  hasLoadedOnce: boolean;
  isOfflineLikely: boolean;
  isStaleData: boolean;
  lastSuccessfulLoadAtUtc: string | null;
  error: string | null;
  refresh: () => Promise<void>;
  retry: () => Promise<void>;
}

function isLikelyOfflineError(error: unknown): boolean {
  if (error instanceof MobileApiError) {
    return error.status === 408 || error.status === 503 || error.status === 504;
  }

  if (error instanceof Error) {
    const normalizedMessage = error.message.toLowerCase();
    return (
      normalizedMessage.includes("network request failed") ||
      normalizedMessage.includes("failed to fetch") ||
      normalizedMessage.includes("networkerror") ||
      normalizedMessage.includes("offline") ||
      normalizedMessage.includes("internet")
    );
  }

  return false;
}

function normalizeQueryOptions(options: ProjectionQueryOptions): ProjectionQueryOptions {
  return {
    accountId: options.accountId,
    fromDate: options.fromDate,
    toDate: options.toDate,
    reviewStatus: options.reviewStatus,
    needsReviewOnly: options.needsReviewOnly,
    page: options.page ?? 1,
    pageSize: options.pageSize ?? 100,
  };
}

export function useProjectionMetadata(options: ProjectionQueryOptions = {}): ProjectionMetadataState {
  const normalizedOptions = useMemo(() => normalizeQueryOptions(options), [
    options.accountId,
    options.fromDate,
    options.toDate,
    options.reviewStatus,
    options.needsReviewOnly,
    options.page,
    options.pageSize,
  ]);

  const [items, setItems] = useState<TransactionProjectionMetadataDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [isRetrying, setIsRetrying] = useState(false);
  const [hasLoadedOnce, setHasLoadedOnce] = useState(false);
  const [isOfflineLikely, setIsOfflineLikely] = useState(false);
  const [isStaleData, setIsStaleData] = useState(false);
  const [lastSuccessfulLoadAtUtc, setLastSuccessfulLoadAtUtc] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const currentRequest = useRef<AbortController | null>(null);

  const load = useCallback(
    async (mode: "initial" | "refresh" | "retry") => {
      currentRequest.current?.abort();
      const controller = new AbortController();
      currentRequest.current = controller;

      if (mode === "refresh") {
        setIsRefreshing(true);
      } else if (mode === "retry") {
        setIsRetrying(true);
      }

      if (mode === "initial" || (mode === "retry" && items.length === 0)) {
        setIsLoading(true);
      }

      try {
        const nextItems = await fetchProjectionMetadata(normalizedOptions, controller.signal);
        setItems(nextItems);
        setHasLoadedOnce(true);
        setIsOfflineLikely(false);
        setIsStaleData(false);
        setLastSuccessfulLoadAtUtc(new Date().toISOString());
        setError(null);
      } catch (requestError) {
        if (!controller.signal.aborted) {
          setIsOfflineLikely(isLikelyOfflineError(requestError));
          setIsStaleData(hasLoadedOnce);
          setError(toReadableError(requestError, "Unexpected error while loading projection metadata."));
        }
      } finally {
        if (!controller.signal.aborted) {
          setIsLoading(false);
          setIsRefreshing(false);
          setIsRetrying(false);
        }
      }
    },
    [hasLoadedOnce, items.length, normalizedOptions],
  );

  useEffect(() => {
    void load("initial");
    return () => {
      currentRequest.current?.abort();
    };
  }, [load]);

  const refresh = useCallback(async () => {
    await load("refresh");
  }, [load]);

  const retry = useCallback(async () => {
    await load("retry");
  }, [load]);

  return {
    items,
    isLoading,
    isRefreshing,
    isRetrying,
    hasLoadedOnce,
    isOfflineLikely,
    isStaleData,
    lastSuccessfulLoadAtUtc,
    error,
    refresh,
    retry,
  };
}
