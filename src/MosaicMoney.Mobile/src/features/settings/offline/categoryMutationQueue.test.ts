import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  addCategoryMutationReconciliationNotice,
  enqueueCategoryMutation,
  isCategoryMutationReadyForReplay,
  listCategoryMutationQueueEntries,
  listCategoryMutationReconciliationNotices,
  markCategoryMutationReplayFailure,
  removeCategoryMutation,
} from "./categoryMutationQueue";

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

describe("categoryMutationQueue", () => {
  beforeEach(() => {
    storage.clear();
  });

  it("enqueues and lists category mutations", async () => {
    await enqueueCategoryMutation({
      method: "POST",
      path: "/api/v1/categories",
      body: { name: "Food", scope: "User" },
      scope: "User",
      replayKey: "post|/api/v1/categories|food",
      summary: "Create category Food",
    });

    const queue = await listCategoryMutationQueueEntries();
    expect(queue).toHaveLength(1);
    expect(queue[0]?.method).toBe("POST");
    expect(queue[0]?.path).toBe("/api/v1/categories");
    expect(queue[0]?.attemptCount).toBe(0);
    expect(isCategoryMutationReadyForReplay(queue[0]!)).toBe(true);
  });

  it("deduplicates mutations with the same replay key", async () => {
    const first = await enqueueCategoryMutation({
      method: "POST",
      path: "/api/v1/categories",
      body: { name: "Food", scope: "User" },
      scope: "User",
      replayKey: "post|/api/v1/categories|food",
      summary: "Create category Food",
    });

    const second = await enqueueCategoryMutation({
      method: "POST",
      path: "/api/v1/categories",
      body: { name: "Food", scope: "User" },
      scope: "User",
      replayKey: "post|/api/v1/categories|food",
      summary: "Create category Food",
    });

    const queue = await listCategoryMutationQueueEntries();
    expect(queue).toHaveLength(1);
    expect(second.id).toBe(first.id);
  });

  it("marks replay failure with exponential backoff", async () => {
    const queued = await enqueueCategoryMutation({
      method: "PATCH",
      path: "/api/v1/categories/123",
      body: { name: "Groceries" },
      scope: "User",
      replayKey: "patch|/api/v1/categories/123|groceries",
      summary: "Rename category",
    });

    await markCategoryMutationReplayFailure(queued.id, "service_unavailable");

    const queue = await listCategoryMutationQueueEntries();
    expect(queue).toHaveLength(1);
    expect(queue[0]?.attemptCount).toBe(1);
    expect(queue[0]?.lastErrorCode).toBe("service_unavailable");
    expect(queue[0]?.nextAttemptAtUtc).toBeDefined();
    expect(isCategoryMutationReadyForReplay(queue[0]!)).toBe(false);
  });

  it("removes queue entries", async () => {
    const queued = await enqueueCategoryMutation({
      method: "DELETE",
      path: "/api/v1/subcategories/456?allowLinkedTransactions=true",
      scope: "HouseholdShared",
      replayKey: "delete|/api/v1/subcategories/456",
      summary: "Archive subcategory",
    });

    await removeCategoryMutation(queued.id);

    const queue = await listCategoryMutationQueueEntries();
    expect(queue).toHaveLength(0);
  });

  it("records reconciliation notices", async () => {
    const queued = await enqueueCategoryMutation({
      method: "POST",
      path: "/api/v1/subcategories/abc/reparent",
      body: { targetCategoryId: "def" },
      scope: "HouseholdShared",
      replayKey: "post|/api/v1/subcategories/abc/reparent|def",
      summary: "Reparent subcategory",
    });

    await addCategoryMutationReconciliationNotice(
      queued,
      "stale_conflict",
      "Category changed since this mutation was queued.",
      "category_reorder_conflict",
    );

    const notices = await listCategoryMutationReconciliationNotices();
    expect(notices).toHaveLength(1);
    expect(notices[0]?.queueEntryId).toBe(queued.id);
    expect(notices[0]?.reason).toBe("stale_conflict");
    expect(notices[0]?.errorCode).toBe("category_reorder_conflict");
  });
});
