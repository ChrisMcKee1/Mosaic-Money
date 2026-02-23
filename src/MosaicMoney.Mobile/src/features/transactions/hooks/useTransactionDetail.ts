import { useCallback, useEffect, useRef, useState } from "react";
import type { TransactionDto } from "../contracts";
import {
  MobileApiError,
  fetchTransactionDetail,
  toReadableError,
} from "../services/mobileTransactionsApi";

export interface TransactionDetailState {
  transaction: TransactionDto | null;
  isLoading: boolean;
  isRefreshing: boolean;
  isNotFound: boolean;
  error: string | null;
  refresh: () => Promise<void>;
  retry: () => Promise<void>;
}

export function useTransactionDetail(transactionId: string | null | undefined): TransactionDetailState {
  const [transaction, setTransaction] = useState<TransactionDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [isNotFound, setIsNotFound] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const currentRequest = useRef<AbortController | null>(null);

  const load = useCallback(
    async (mode: "initial" | "refresh") => {
      if (!transactionId) {
        setIsLoading(false);
        setIsRefreshing(false);
        setIsNotFound(false);
        setError("A valid transaction id is required.");
        return;
      }

      currentRequest.current?.abort();
      const controller = new AbortController();
      currentRequest.current = controller;

      if (mode === "refresh") {
        setIsRefreshing(true);
      } else {
        setIsLoading(true);
      }

      try {
        const nextTransaction = await fetchTransactionDetail(transactionId, controller.signal);
        setTransaction(nextTransaction);
        setIsNotFound(false);
        setError(null);
      } catch (requestError) {
        if (controller.signal.aborted) {
          return;
        }

        if (requestError instanceof MobileApiError && requestError.status === 404) {
          setTransaction(null);
          setIsNotFound(true);
          setError(null);
        } else {
          setError(toReadableError(requestError));
          setIsNotFound(false);
        }
      } finally {
        if (!controller.signal.aborted) {
          setIsLoading(false);
          setIsRefreshing(false);
        }
      }
    },
    [transactionId],
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
    await load("initial");
  }, [load]);

  return {
    transaction,
    isLoading,
    isRefreshing,
    isNotFound,
    error,
    refresh,
    retry,
  };
}
