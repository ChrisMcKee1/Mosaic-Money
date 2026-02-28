import { MobileApiError } from "../../../shared/services/mobileApiClient";
import { postAssistantMessage } from "../services/mobileAgentApi";
import {
  getAssistantQueuedPromptReplayRequest,
  isAssistantPromptReadyForReplay,
  listAssistantPromptQueueEntries,
  markAssistantPromptReplayFailure,
  removeAssistantPrompt,
} from "./assistantPromptQueue";

export interface AssistantPromptRecoveryResult {
  scannedCount: number;
  replayReadyCount: number;
  replayedCount: number;
  retriedCount: number;
  reconciledCount: number;
}

interface ReplayErrorClassification {
  retriable: boolean;
  errorCode?: string;
}

let activeReplayRun: Promise<AssistantPromptRecoveryResult> | null = null;

function readErrorCode(error: unknown): string | undefined {
  if (!(error instanceof MobileApiError)) {
    return undefined;
  }

  return error.code ?? `HTTP_${error.status}`;
}

function classifyReplayError(error: unknown): ReplayErrorClassification {
  if (!(error instanceof MobileApiError)) {
    return {
      retriable: true,
    };
  }

  const errorCode = readErrorCode(error);
  if (error.status >= 500 || error.status === 408 || error.status === 429) {
    return {
      retriable: true,
      errorCode,
    };
  }

  return {
    retriable: false,
    errorCode,
  };
}

async function replayReadyAssistantPrompts(): Promise<AssistantPromptRecoveryResult> {
  const queueEntries = await listAssistantPromptQueueEntries();
  const replayReadyEntries = queueEntries.filter((entry) => isAssistantPromptReadyForReplay(entry));

  const result: AssistantPromptRecoveryResult = {
    scannedCount: queueEntries.length,
    replayReadyCount: replayReadyEntries.length,
    replayedCount: 0,
    retriedCount: 0,
    reconciledCount: 0,
  };

  for (const queueEntry of replayReadyEntries) {
    try {
      const request = getAssistantQueuedPromptReplayRequest(queueEntry);
      await postAssistantMessage(queueEntry.conversationId, request);
      await removeAssistantPrompt(queueEntry.id);
      result.replayedCount += 1;
    } catch (error) {
      const classification = classifyReplayError(error);

      if (classification.retriable) {
        await markAssistantPromptReplayFailure(queueEntry.id, classification.errorCode);
        result.retriedCount += 1;
        continue;
      }

      await removeAssistantPrompt(queueEntry.id);
      result.reconciledCount += 1;
    }
  }

  return result;
}

export function replayQueuedAssistantPrompts(): Promise<AssistantPromptRecoveryResult> {
  if (activeReplayRun) {
    return activeReplayRun;
  }

  const replayPromise = replayReadyAssistantPrompts();
  const trackedReplayPromise = replayPromise.finally(() => {
    if (activeReplayRun === trackedReplayPromise) {
      activeReplayRun = null;
    }
  });

  activeReplayRun = trackedReplayPromise;
  return activeReplayRun;
}
