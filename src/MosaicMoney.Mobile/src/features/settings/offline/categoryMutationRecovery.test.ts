import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  enqueueCategoryMutation,
  listCategoryMutationQueueEntries,
  listCategoryMutationReconciliationNotices,
} from "./categoryMutationQueue";
import { replayQueuedCategoryMutations } from "./categoryMutationRecovery";
import { MobileApiError } from "../../../shared/services/mobileApiClient";

const storage = new Map<string, string>();
const { requestJsonMock } = vi.hoisted(() => ({
  requestJsonMock: vi.fn(),
}));

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

vi.mock("../../../shared/services/mobileApiClient", () => ({
  requestJson: (...args: unknown[]) => requestJsonMock(...args),
  MobileApiError: class MobileApiError extends Error {
    public readonly status: number;
    public readonly code?: string;

    constructor(status: number, message: string, code?: string) {
      super(message);
      this.name = "MobileApiError";
      this.status = status;
      this.code = code;
    }
  },
}));

describe("categoryMutationRecovery", () => {
  beforeEach(() => {
    storage.clear();
    requestJsonMock.mockReset();
  });

  it("replays queued mutation and removes entry on success", async () => {
    await enqueueCategoryMutation({
      method: "POST",
      path: "/api/v1/categories",
      body: { name: "Food", scope: "User" },
      scope: "User",
      replayKey: "post|/api/v1/categories|food",
      summary: "Create category",
    });

    requestJsonMock.mockResolvedValue({ id: "cat-1" });

    const result = await replayQueuedCategoryMutations();

    expect(result).toEqual({
      scannedCount: 1,
      replayReadyCount: 1,
      replayedCount: 1,
      retriedCount: 0,
      reconciledCount: 0,
    });

    const queue = await listCategoryMutationQueueEntries();
    expect(queue).toHaveLength(0);
    expect(requestJsonMock).toHaveBeenCalledTimes(1);
  });

  it("retains queued mutation with backoff on retriable failure", async () => {
    await enqueueCategoryMutation({
      method: "PATCH",
      path: "/api/v1/categories/123",
      body: { name: "Groceries" },
      scope: "User",
      replayKey: "patch|/api/v1/categories/123|groceries",
      summary: "Rename category",
    });

    requestJsonMock.mockRejectedValue(
      new MobileApiError(503, "Service unavailable", "service_unavailable"),
    );

    const result = await replayQueuedCategoryMutations();

    expect(result).toEqual({
      scannedCount: 1,
      replayReadyCount: 1,
      replayedCount: 0,
      retriedCount: 1,
      reconciledCount: 0,
    });

    const queue = await listCategoryMutationQueueEntries();
    expect(queue).toHaveLength(1);
    expect(queue[0]?.attemptCount).toBe(1);
    expect(queue[0]?.lastErrorCode).toBe("service_unavailable");
  });

  it("records reconciliation notice and removes queue entry on conflict", async () => {
    await enqueueCategoryMutation({
      method: "POST",
      path: "/api/v1/subcategories/abc/reparent",
      body: { targetCategoryId: "def" },
      scope: "HouseholdShared",
      replayKey: "post|/api/v1/subcategories/abc/reparent|def",
      summary: "Reparent subcategory",
    });

    requestJsonMock.mockRejectedValue(
      new MobileApiError(409, "Conflict", "category_reorder_conflict"),
    );

    const result = await replayQueuedCategoryMutations();

    expect(result).toEqual({
      scannedCount: 1,
      replayReadyCount: 1,
      replayedCount: 0,
      retriedCount: 0,
      reconciledCount: 1,
    });

    const queue = await listCategoryMutationQueueEntries();
    expect(queue).toHaveLength(0);

    const notices = await listCategoryMutationReconciliationNotices();
    expect(notices).toHaveLength(1);
    expect(notices[0]?.reason).toBe("stale_conflict");
    expect(notices[0]?.errorCode).toBe("category_reorder_conflict");
  });
});
