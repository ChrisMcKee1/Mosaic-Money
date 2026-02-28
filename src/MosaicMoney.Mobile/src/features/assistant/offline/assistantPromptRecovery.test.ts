import { beforeEach, describe, expect, it, vi } from "vitest";
import { MobileApiError } from "../../../shared/services/mobileApiClient";
import {
  enqueueAssistantPrompt,
  listAssistantPromptQueueEntries,
} from "./assistantPromptQueue";
import { replayQueuedAssistantPrompts } from "./assistantPromptRecovery";

const storage = new Map<string, string>();
const postAssistantMessageMock = vi.fn();

vi.mock("@react-native-async-storage/async-storage", () => ({
  default: {
    getItem: vi.fn(async (key: string) => storage.get(key) ?? null),
    setItem: vi.fn(async (key: string, value: string) => {
      storage.set(key, value);
    }),
    removeItem: vi.fn(async (key: string) => {
      storage.delete(key);
    }),
  },
}));

vi.mock("../services/mobileAssistantApi", () => ({
  postAssistantMessage: (...args: unknown[]) => postAssistantMessageMock(...args),
}));

describe("assistantPromptRecovery", () => {
  beforeEach(() => {
    storage.clear();
    postAssistantMessageMock.mockReset();
  });

  it("replays queued prompts and removes them on success", async () => {
    await enqueueAssistantPrompt({
      conversationId: "conv-success",
      replayKey: "conv-success|message-1",
      summary: "Need a spending summary.",
      request: {
        message: "Need a spending summary.",
        clientMessageId: "message-1",
      },
    });

    postAssistantMessageMock.mockResolvedValue({ commandId: "cmd-1" });

    const result = await replayQueuedAssistantPrompts();

    expect(result).toEqual({
      scannedCount: 1,
      replayReadyCount: 1,
      replayedCount: 1,
      retriedCount: 0,
      reconciledCount: 0,
    });

    const queue = await listAssistantPromptQueueEntries();
    expect(queue).toHaveLength(0);
    expect(postAssistantMessageMock).toHaveBeenCalledTimes(1);
  });

  it("keeps queued prompts with backoff on retriable errors", async () => {
    await enqueueAssistantPrompt({
      conversationId: "conv-retry",
      replayKey: "conv-retry|message-2",
      summary: "Route this transaction to review.",
      request: {
        message: "Route this transaction to review.",
        clientMessageId: "message-2",
      },
    });

    postAssistantMessageMock.mockRejectedValue(
      new MobileApiError(503, "Service unavailable.", "service_unavailable"),
    );

    const result = await replayQueuedAssistantPrompts();

    expect(result).toEqual({
      scannedCount: 1,
      replayReadyCount: 1,
      replayedCount: 0,
      retriedCount: 1,
      reconciledCount: 0,
    });

    const queue = await listAssistantPromptQueueEntries();
    expect(queue).toHaveLength(1);
    expect(queue[0]?.attemptCount).toBe(1);
    expect(queue[0]?.lastErrorCode).toBe("service_unavailable");
  });

  it("reconciles non-retriable errors by removing queued prompts", async () => {
    await enqueueAssistantPrompt({
      conversationId: "conv-reconcile",
      replayKey: "conv-reconcile|message-3",
      summary: "Queue this high impact request.",
      request: {
        message: "Queue this high impact request.",
        clientMessageId: "message-3",
      },
    });

    postAssistantMessageMock.mockRejectedValue(
      new MobileApiError(400, "Invalid payload.", "validation_failed"),
    );

    const result = await replayQueuedAssistantPrompts();

    expect(result).toEqual({
      scannedCount: 1,
      replayReadyCount: 1,
      replayedCount: 0,
      retriedCount: 0,
      reconciledCount: 1,
    });

    const queue = await listAssistantPromptQueueEntries();
    expect(queue).toHaveLength(0);
  });
});
