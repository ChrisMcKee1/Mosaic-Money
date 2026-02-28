import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  enqueueAssistantPrompt,
  getAssistantQueuedPromptReplayRequest,
  isAssistantPromptReadyForReplay,
  listAssistantPromptQueueEntries,
  markAssistantPromptReplayFailure,
  removeAssistantPrompt,
} from "./assistantPromptQueue";

const storage = new Map<string, string>();

vi.mock("@react-native-async-storage/async-storage", () => ({
  default: {
    getItem: vi.fn(async (key: string) => storage.get(key) ?? null),
    setItem: vi.fn(async (key: string, value: string) => {
      storage.set(key, value);
    }),
    removeItem: vi.fn(async (key: string) => {
      storage.delete(key);
    }),
    clear: vi.fn(async () => {
      storage.clear();
    }),
  },
}));

describe("assistantPromptQueue", () => {
  beforeEach(() => {
    storage.clear();
  });

  it("enqueues and lists assistant prompts", async () => {
    await enqueueAssistantPrompt({
      conversationId: "conv-123",
      replayKey: "conv-123|message-1",
      summary: "Need help classifying this payment.",
      request: {
        message: "Need help classifying this payment.",
        clientMessageId: "message-1",
      },
    });

    const queue = await listAssistantPromptQueueEntries();
    expect(queue).toHaveLength(1);
    expect(queue[0]?.conversationId).toBe("conv-123");
    expect(queue[0]?.attemptCount).toBe(0);
    expect(isAssistantPromptReadyForReplay(queue[0]!)).toBe(true);

    const replayRequest = getAssistantQueuedPromptReplayRequest(queue[0]!);
    expect(replayRequest).toEqual({
      message: "Need help classifying this payment.",
      clientMessageId: "message-1",
      userNote: null,
    });
  });

  it("deduplicates prompts using replay key", async () => {
    const first = await enqueueAssistantPrompt({
      conversationId: "conv-123",
      replayKey: "conv-123|message-2",
      summary: "Route this ambiguous transaction.",
      request: {
        message: "Route this ambiguous transaction.",
        clientMessageId: "message-2",
      },
    });

    const second = await enqueueAssistantPrompt({
      conversationId: "conv-123",
      replayKey: "conv-123|message-2",
      summary: "Route this ambiguous transaction.",
      request: {
        message: "Route this ambiguous transaction.",
        clientMessageId: "message-2",
      },
    });

    const queue = await listAssistantPromptQueueEntries();
    expect(queue).toHaveLength(1);
    expect(second.id).toBe(first.id);
  });

  it("marks replay failure with backoff", async () => {
    const queued = await enqueueAssistantPrompt({
      conversationId: "conv-123",
      replayKey: "conv-123|message-3",
      summary: "Generate spend summary.",
      request: {
        message: "Generate spend summary.",
        clientMessageId: "message-3",
      },
    });

    await markAssistantPromptReplayFailure(queued.id, "service_unavailable");

    const queue = await listAssistantPromptQueueEntries();
    expect(queue).toHaveLength(1);
    expect(queue[0]?.attemptCount).toBe(1);
    expect(queue[0]?.lastErrorCode).toBe("service_unavailable");
    expect(queue[0]?.nextAttemptAtUtc).toBeDefined();
    expect(isAssistantPromptReadyForReplay(queue[0]!)).toBe(false);
  });

  it("removes queued prompts", async () => {
    const queued = await enqueueAssistantPrompt({
      conversationId: "conv-123",
      replayKey: "conv-123|message-4",
      summary: "Check this transfer.",
      request: {
        message: "Check this transfer.",
        clientMessageId: "message-4",
      },
    });

    await removeAssistantPrompt(queued.id);

    const queue = await listAssistantPromptQueueEntries();
    expect(queue).toHaveLength(0);
  });
});
