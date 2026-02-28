import { MobileApiError } from "../../../shared/services/mobileApiClient";
import { postAgentMessage } from "../services/mobileAgentApi";
import {
  getAgentQueuedPromptReplayRequest,
  isAgentPromptReadyForReplay,
  listAgentPromptQueueEntries,
  markAgentPromptReplayFailure,
  removeAgentPrompt,
} from "./agentPromptQueue";

export interface AgentPromptRecoveryResult {
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

let activeReplayRun: Promise<AgentPromptRecoveryResult> | null = null;

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

async function replayReadyAgentPrompts(): Promise<AgentPromptRecoveryResult> {
  const queueEntries = await listAgentPromptQueueEntries();
  const replayReadyEntries = queueEntries.filter((entry) => isAgentPromptReadyForReplay(entry));

  const result: AgentPromptRecoveryResult = {
    scannedCount: queueEntries.length,
    replayReadyCount: replayReadyEntries.length,
    replayedCount: 0,
    retriedCount: 0,
    reconciledCount: 0,
  };

  for (const queueEntry of replayReadyEntries) {
    try {
      const request = getAgentQueuedPromptReplayRequest(queueEntry);
      await postAgentMessage(queueEntry.conversationId, request);
      await removeAgentPrompt(queueEntry.id);
      result.replayedCount += 1;
    } catch (error) {
      const classification = classifyReplayError(error);

      if (classification.retriable) {
        await markAgentPromptReplayFailure(queueEntry.id, classification.errorCode);
        result.retriedCount += 1;
        continue;
      }

      await removeAgentPrompt(queueEntry.id);
      result.reconciledCount += 1;
    }
  }

  return result;
}

export function replayQueuedAgentPrompts(): Promise<AgentPromptRecoveryResult> {
  if (activeReplayRun) {
    return activeReplayRun;
  }

  const replayPromise = replayReadyAgentPrompts();
  const trackedReplayPromise = replayPromise.finally(() => {
    if (activeReplayRun === trackedReplayPromise) {
      activeReplayRun = null;
    }
  });

  activeReplayRun = trackedReplayPromise;
  return activeReplayRun;
}
