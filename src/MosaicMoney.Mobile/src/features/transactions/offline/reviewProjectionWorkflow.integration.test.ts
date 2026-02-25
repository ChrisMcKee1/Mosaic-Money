import { beforeEach, describe, expect, it, vi } from "vitest";
import { enqueueReviewMutation, listReviewMutationQueueEntries } from "./reviewMutationQueue";
import {
  getReviewMutationReconciliationNoticeForTransaction,
  replayQueuedReviewMutations,
} from "./reviewMutationRecovery";
import { fetchProjectionMetadata } from "../../projections/services/mobileProjectionApi";

const storage = new Map<string, string>();
const fetchMock = vi.fn<typeof fetch>();

vi.mock("../services/apiConfig", () => ({
  getApiBaseUrl: () => "https://mobile.test",
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

function createJsonResponse(status: number, payload: unknown): Response {
  return new Response(JSON.stringify(payload), {
    status,
    headers: {
      "Content-Type": "application/json",
    },
  });
}

function buildProjectionMetadata(id: string, reviewStatus: string) {
  return {
    id,
    accountId: "acc-001",
    description: "Electric bill",
    rawAmount: 123.45,
    rawTransactionDate: "2026-02-24",
    reviewStatus,
    reviewReason: reviewStatus === "NeedsReview" ? "Needs human review." : undefined,
    excludeFromBudget: false,
    isExtraPrincipal: false,
    recurring: {
      isLinked: true,
      recurringItemId: "rec-001",
      isActive: true,
      frequency: "monthly",
      nextDueDate: "2026-03-24",
    },
    reimbursement: {
      hasProposals: false,
      proposalCount: 0,
      hasPendingHumanReview: false,
      latestStatus: "none",
      latestStatusReasonCode: "none",
      pendingOrNeedsReviewAmount: 0,
      approvedAmount: 0,
    },
    splits: [],
    createdAtUtc: "2026-02-24T10:00:00.000Z",
    lastModifiedAtUtc: "2026-02-24T10:05:00.000Z",
  };
}

describe("review/projection workflow integration", () => {
  beforeEach(() => {
    storage.clear();
    fetchMock.mockReset();
    vi.stubGlobal("fetch", fetchMock);
  });

  it("replays queued review approval and fetches approved projection metadata", async () => {
    await enqueueReviewMutation({
      actionKind: "approve",
      request: {
        transactionId: "txn-approved",
        action: "approve",
      },
    });

    fetchMock
      .mockResolvedValueOnce(
        createJsonResponse(200, {
          id: "txn-approved",
          accountId: "acc-001",
          description: "Electric bill",
          amount: 123.45,
          transactionDate: "2026-02-24",
          reviewStatus: "Approved",
          excludeFromBudget: false,
          isExtraPrincipal: false,
          splits: [],
          createdAtUtc: "2026-02-24T10:00:00.000Z",
          lastModifiedAtUtc: "2026-02-24T10:05:00.000Z",
        }),
      )
      .mockResolvedValueOnce(
        createJsonResponse(200, [buildProjectionMetadata("txn-approved", "Approved")]),
      );

    const replayResult = await replayQueuedReviewMutations();
    expect(replayResult).toEqual({
      scannedCount: 1,
      replayReadyCount: 1,
      replayedCount: 1,
      retriedCount: 0,
      reconciledCount: 0,
    });

    const queueAfterReplay = await listReviewMutationQueueEntries();
    expect(queueAfterReplay).toHaveLength(0);

    const projections = await fetchProjectionMetadata({
      reviewStatus: "Approved",
      needsReviewOnly: true,
      pageSize: 25,
    });

    const firstCall = fetchMock.mock.calls[0];
    const firstUrl = firstCall?.[0] as string;
    const firstRequest = firstCall?.[1] as RequestInit;

    expect(firstUrl).toBe("https://mobile.test/api/v1/review-actions");
    expect(firstRequest.method).toBe("POST");
    expect(firstRequest.body).toBe(
      JSON.stringify({
        transactionId: "txn-approved",
        action: "approve",
      }),
    );

    const secondCall = fetchMock.mock.calls[1];
    const secondUrl = secondCall?.[0] as string;
    expect(secondUrl).toContain("/api/v1/transactions/projection-metadata?");
    expect(secondUrl).toContain("reviewStatus=Approved");
    expect(secondUrl).toContain("needsReviewOnly=true");
    expect(secondUrl).toContain("page=1");
    expect(secondUrl).toContain("pageSize=25");

    expect(projections).toHaveLength(1);
    expect(projections[0]?.id).toBe("txn-approved");
    expect(projections[0]?.reviewStatus).toBe("Approved");
  });

  it("reconciles stale conflicts and still loads needs-review projections", async () => {
    await enqueueReviewMutation({
      actionKind: "reject",
      request: {
        transactionId: "txn-needs-review",
        action: "route_to_needs_review",
        reviewReason: "Needs a second look",
        needsReviewByUserId: "reviewer-001",
      },
    });

    fetchMock
      .mockResolvedValueOnce(
        createJsonResponse(409, {
          error: {
            code: "invalid_review_transition",
            message: "Invalid review transition.",
            traceId: "trace-001",
          },
        }),
      )
      .mockResolvedValueOnce(
        createJsonResponse(200, [buildProjectionMetadata("txn-needs-review", "NeedsReview")]),
      );

    const replayResult = await replayQueuedReviewMutations();
    expect(replayResult).toEqual({
      scannedCount: 1,
      replayReadyCount: 1,
      replayedCount: 0,
      retriedCount: 0,
      reconciledCount: 1,
    });

    const queueAfterReplay = await listReviewMutationQueueEntries();
    expect(queueAfterReplay).toHaveLength(0);

    const notice = await getReviewMutationReconciliationNoticeForTransaction("txn-needs-review");
    expect(notice?.reason).toBe("stale_conflict");
    expect(notice?.errorCode).toBe("invalid_review_transition");

    const projections = await fetchProjectionMetadata({ needsReviewOnly: true });
    const secondCall = fetchMock.mock.calls[1];
    const secondUrl = secondCall?.[0] as string;

    expect(secondUrl).toContain("needsReviewOnly=true");
    expect(secondUrl).toContain("page=1");
    expect(secondUrl).toContain("pageSize=100");
    expect(projections[0]?.reviewStatus).toBe("NeedsReview");
  });
});