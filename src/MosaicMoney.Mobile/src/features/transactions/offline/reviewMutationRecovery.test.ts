import { beforeEach, describe, expect, it, vi } from "vitest";
import { MobileApiError } from "../../../shared/services/mobileApiClient";
import {
  enqueueReviewMutation,
  listReviewMutationQueueEntries,
} from "./reviewMutationQueue";
import {
  getReviewMutationReconciliationNoticeForTransaction,
  replayQueuedReviewMutations,
} from "./reviewMutationRecovery";

const storage = new Map<string, string>();
const submitReviewActionMock = vi.fn();

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

vi.mock("../services/mobileTransactionsApi", () => ({
  submitReviewAction: (...args: unknown[]) => submitReviewActionMock(...args),
}));

function createApproveRequest(transactionId: string) {
  return {
    transactionId,
    action: "approve",
  };
}

describe("reviewMutationRecovery", () => {
  beforeEach(() => {
    storage.clear();
    submitReviewActionMock.mockReset();
  });

  it("replays queued mutation and clears queue on success", async () => {
    await enqueueReviewMutation({
      actionKind: "approve",
      request: createApproveRequest("txn-success"),
    });

    submitReviewActionMock.mockResolvedValue({ id: "txn-success" });

    const result = await replayQueuedReviewMutations();

    expect(result).toEqual({
      scannedCount: 1,
      replayReadyCount: 1,
      replayedCount: 1,
      retriedCount: 0,
      reconciledCount: 0,
    });

    const queue = await listReviewMutationQueueEntries();
    expect(queue).toHaveLength(0);

    const notice = await getReviewMutationReconciliationNoticeForTransaction("txn-success");
    expect(notice).toBeNull();
  });

  it("keeps queued mutation with backoff when replay fails with retriable error", async () => {
    await enqueueReviewMutation({
      actionKind: "approve",
      request: createApproveRequest("txn-retry"),
    });

    submitReviewActionMock.mockRejectedValue(
      new MobileApiError(503, "Service unavailable.", "service_unavailable"),
    );

    const result = await replayQueuedReviewMutations();

    expect(result).toEqual({
      scannedCount: 1,
      replayReadyCount: 1,
      replayedCount: 0,
      retriedCount: 1,
      reconciledCount: 0,
    });

    const queue = await listReviewMutationQueueEntries();
    expect(queue).toHaveLength(1);
    expect(queue[0]?.attemptCount).toBe(1);
    expect(queue[0]?.lastErrorCode).toBe("service_unavailable");
    expect(queue[0]?.nextAttemptAtUtc).toBeDefined();

    const notice = await getReviewMutationReconciliationNoticeForTransaction("txn-retry");
    expect(notice).toBeNull();
  });

  it("reconciles stale conflict failures and removes queued mutation", async () => {
    await enqueueReviewMutation({
      actionKind: "approve",
      request: createApproveRequest("txn-stale"),
    });

    submitReviewActionMock.mockRejectedValue(
      new MobileApiError(409, "Invalid review transition.", "invalid_review_transition"),
    );

    const result = await replayQueuedReviewMutations();

    expect(result).toEqual({
      scannedCount: 1,
      replayReadyCount: 1,
      replayedCount: 0,
      retriedCount: 0,
      reconciledCount: 1,
    });

    const queue = await listReviewMutationQueueEntries();
    expect(queue).toHaveLength(0);

    const notice = await getReviewMutationReconciliationNoticeForTransaction("txn-stale");
    expect(notice?.reason).toBe("stale_conflict");
    expect(notice?.errorCode).toBe("invalid_review_transition");
    expect(notice?.message).toContain("backend review state changed");
  });

  it("reconciles non-retriable failures and records non_retriable notice", async () => {
    await enqueueReviewMutation({
      actionKind: "approve",
      request: createApproveRequest("txn-non-retriable"),
    });

    submitReviewActionMock.mockRejectedValue(
      new MobileApiError(400, "Rejected by backend.", "invalid_payload"),
    );

    const result = await replayQueuedReviewMutations();

    expect(result).toEqual({
      scannedCount: 1,
      replayReadyCount: 1,
      replayedCount: 0,
      retriedCount: 0,
      reconciledCount: 1,
    });

    const queue = await listReviewMutationQueueEntries();
    expect(queue).toHaveLength(0);

    const notice = await getReviewMutationReconciliationNoticeForTransaction("txn-non-retriable");
    expect(notice?.reason).toBe("non_retriable");
    expect(notice?.errorCode).toBe("invalid_payload");
    expect(notice?.message).toContain("backend rejected replay");
  });
});
