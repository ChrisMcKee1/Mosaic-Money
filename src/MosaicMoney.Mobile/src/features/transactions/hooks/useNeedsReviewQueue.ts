import { useCallback, useEffect, useRef, useState } from "react";
import type { TransactionDto } from "../contracts";
import { fetchNeedsReviewTransactions, toReadableError } from "../services/mobileTransactionsApi";

export interface NeedsReviewQueueState {
  transactions: TransactionDto[];
  isLoading: boolean;
  isRefreshing: boolean;
  error: string | null;
  refresh: () => Promise<void>;
  retry: () => Promise<void>;
}

export function useNeedsReviewQueue(): NeedsReviewQueueState {
  const [transactions, setTransactions] = useState<TransactionDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const currentRequest = useRef<AbortController | null>(null);

  const load = useCallback(async (mode: "initial" | "refresh") => {
    currentRequest.current?.abort();
    const controller = new AbortController();
    currentRequest.current = controller;

    if (mode === "refresh") {
      setIsRefreshing(true);
    } else {
      setIsLoading(true);
    }

    try {
      const nextTransactions = await fetchNeedsReviewTransactions(controller.signal);
      setTransactions(nextTransactions);
      setError(null);
    } catch (requestError) {
      if (!controller.signal.aborted) {
        setError(toReadableError(requestError));
      }
    } finally {
      if (!controller.signal.aborted) {
        setIsLoading(false);
        setIsRefreshing(false);
      }
    }
  }, []);

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
    await load("initial");
  }, [load]);

  return {
    transactions,
    isLoading,
    isRefreshing,
    error,
    refresh,
    retry,
  };
}
