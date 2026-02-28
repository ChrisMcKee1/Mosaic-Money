import { useCallback, useEffect, useRef } from "react";
import { AppState, type AppStateStatus } from "react-native";
import { replayQueuedCategoryMutations } from "./categoryMutationRecovery";

const DEFAULT_RECOVERY_INTERVAL_MS = 45_000;

export function useCategoryMutationRecovery(options?: {
  intervalMs?: number;
}): void {
  const intervalMs = options?.intervalMs ?? DEFAULT_RECOVERY_INTERVAL_MS;
  const appStateRef = useRef<AppStateStatus>(AppState.currentState);
  const localRunRef = useRef<Promise<unknown> | null>(null);

  const runRecovery = useCallback(() => {
    if (localRunRef.current) {
      return localRunRef.current;
    }

    const runPromise = replayQueuedCategoryMutations();
    const trackedRunPromise = runPromise.finally(() => {
      if (localRunRef.current === trackedRunPromise) {
        localRunRef.current = null;
      }
    });
    localRunRef.current = trackedRunPromise;

    return localRunRef.current;
  }, []);

  useEffect(() => {
    void runRecovery();

    const subscription = AppState.addEventListener("change", (nextAppState) => {
      const previousState = appStateRef.current;
      appStateRef.current = nextAppState;

      if (previousState !== "active" && nextAppState === "active") {
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
