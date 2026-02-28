import { MobileApiError, requestJson } from "../../../shared/services/mobileApiClient";
import {
  addCategoryMutationReconciliationNotice,
  isCategoryMutationReadyForReplay,
  listCategoryMutationQueueEntries,
  markCategoryMutationReplayFailure,
  removeCategoryMutation,
} from "./categoryMutationQueue";

export interface CategoryMutationRecoveryResult {
  scannedCount: number;
  replayReadyCount: number;
  replayedCount: number;
  retriedCount: number;
  reconciledCount: number;
}

let activeReplayRun: Promise<CategoryMutationRecoveryResult> | null = null;

function classifyReplayError(error: unknown): {
  retriable: boolean;
  reason?: "stale_conflict" | "non_retriable";
  message: string;
  errorCode?: string;
} {
  if (error instanceof MobileApiError) {
    if (error.status === 409) {
      return {
        retriable: false,
        reason: "stale_conflict",
        message: "Category mutation replay conflicted with current backend state.",
        errorCode: error.code,
      };
    }

    if (error.status >= 400 && error.status < 500) {
      return {
        retriable: false,
        reason: "non_retriable",
        message: "Category mutation replay was rejected by backend validation.",
        errorCode: error.code,
      };
    }

    return {
      retriable: true,
      message: "Category mutation replay failed with retriable backend status.",
      errorCode: error.code,
    };
  }

  if (error instanceof Error) {
    return {
      retriable: true,
      message: error.message,
    };
  }

  return {
    retriable: true,
    message: "Unknown category mutation replay failure.",
  };
}

export async function replayQueuedCategoryMutations(): Promise<CategoryMutationRecoveryResult> {
  if (activeReplayRun) {
    return activeReplayRun;
  }

  const runPromise = (async (): Promise<CategoryMutationRecoveryResult> => {
    const queue = await listCategoryMutationQueueEntries();
    const replayReady = queue.filter((entry) => isCategoryMutationReadyForReplay(entry));

    const result: CategoryMutationRecoveryResult = {
      scannedCount: queue.length,
      replayReadyCount: replayReady.length,
      replayedCount: 0,
      retriedCount: 0,
      reconciledCount: 0,
    };

    for (const entry of replayReady) {
      try {
        await requestJson<unknown, unknown>(entry.path, {
          method: entry.method,
          body: entry.body,
        });

        await removeCategoryMutation(entry.id);
        result.replayedCount += 1;
      } catch (error) {
        const classification = classifyReplayError(error);
        if (classification.retriable) {
          await markCategoryMutationReplayFailure(entry.id, classification.errorCode);
          result.retriedCount += 1;
          continue;
        }

        await addCategoryMutationReconciliationNotice(
          entry,
          classification.reason ?? "non_retriable",
          classification.message,
          classification.errorCode,
        );
        await removeCategoryMutation(entry.id);
        result.reconciledCount += 1;
      }
    }

    return result;
  })();

  activeReplayRun = runPromise;

  try {
    return await runPromise;
  } finally {
    if (activeReplayRun === runPromise) {
      activeReplayRun = null;
    }
  }
}
