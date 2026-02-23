import { useCallback, useEffect, useRef } from "react";
import { AppState, type AppStateStatus } from "react-native";
import { replayQueuedReviewMutations } from "../offline/reviewMutationRecovery";

const DEFAULT_RECOVERY_INTERVAL_MS = 45_000;

export function useReviewMutationRecovery(options?: {
  intervalMs?: number;
}): void {
  const intervalMs = options?.intervalMs ?? DEFAULT_RECOVERY_INTERVAL_MS;
  const appStateRef = useRef<AppStateStatus>(AppState.currentState);
  const localRunRef = useRef<Promise<unknown> | null>(null);

  const runRecovery = useCallback(() => {
    if (localRunRef.current) {
      return localRunRef.current;
    }

    const runPromise = replayQueuedReviewMutations();
    localRunRef.current = runPromise.finally(() => {
      if (localRunRef.current === runPromise) {
        localRunRef.current = null;
      }
    });

    return localRunRef.current;
  }, []);

  useEffect(() => {
    void runRecovery();

    const subscription = AppState.addEventListener("change", (nextState) => {
      const previousState = appStateRef.current;
      appStateRef.current = nextState;

      if (previousState !== "active" && nextState === "active") {
        void runRecovery();
      }
    });

    const timer = setInterval(() => {
      if (appStateRef.current === "active") {
        void runRecovery();
      }
    }, intervalMs);

    return () => {
      subscription.remove();
      clearInterval(timer);
    };
  }, [intervalMs, runRecovery]);
}
