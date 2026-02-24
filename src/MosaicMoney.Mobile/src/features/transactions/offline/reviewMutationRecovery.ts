import AsyncStorage from "@react-native-async-storage/async-storage";
import {
  isReviewMutationReadyForReplay,
  listReviewMutationQueueEntries,
  markReviewMutationReplayFailure,
  removeReviewMutation,
} from "./reviewMutationQueue";
import { submitReviewAction } from "../services/mobileTransactionsApi";
import { MobileApiError } from "../../../shared/services/mobileApiClient";

const REVIEW_MUTATION_RECONCILIATION_NOTICE_STORAGE_KEY =
  "mosaic_money.mobile.review_mutation_reconciliation_notices.v1";
const REVIEW_MUTATION_RECONCILIATION_NOTICE_SCHEMA_VERSION = 1;

export type ReviewMutationReconciliationReason = "stale_conflict" | "non_retriable";

export interface ReviewMutationReconciliationNotice {
  id: string;
  schemaVersion: 1;
  queueEntryId: string;
  transactionId: string;
  reason: ReviewMutationReconciliationReason;
  message: string;
  reconciledAtUtc: string;
  errorCode?: string;
}

interface ReviewMutationReconciliationNoticeDocument {
  version: 1;
  notices: ReviewMutationReconciliationNotice[];
}

const EMPTY_RECONCILIATION_NOTICE_DOCUMENT: ReviewMutationReconciliationNoticeDocument = {
  version: REVIEW_MUTATION_RECONCILIATION_NOTICE_SCHEMA_VERSION,
  notices: [],
};

interface ReplayErrorClassification {
  retriable: boolean;
  errorCode?: string;
  reconciliationReason?: ReviewMutationReconciliationReason;
  reconciliationMessage?: string;
}

export interface ReviewMutationReconciliationDetails {
  reason: ReviewMutationReconciliationReason;
  message: string;
  errorCode?: string;
}

export interface ReviewMutationRecoveryResult {
  scannedCount: number;
  replayReadyCount: number;
  replayedCount: number;
  retriedCount: number;
  reconciledCount: number;
}

let activeRecoveryRun: Promise<ReviewMutationRecoveryResult> | null = null;

function createNoticeId(): string {
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

function toStringOrUndefined(value: unknown): string | undefined {
  return typeof value === "string" ? value : undefined;
}

function normalizeNotice(
  value: unknown,
  fallbackTransactionId?: string,
): ReviewMutationReconciliationNotice | null {
  if (!isRecord(value)) {
    return null;
  }

  const transactionId = toStringOrUndefined(value.transactionId) ?? fallbackTransactionId;
  const queueEntryId = toStringOrUndefined(value.queueEntryId);
  const reason = toStringOrUndefined(value.reason);
  const message = toStringOrUndefined(value.message);
  const reconciledAtUtc = toStringOrUndefined(value.reconciledAtUtc);

  if (!transactionId || !queueEntryId || !message || !reconciledAtUtc) {
    return null;
  }

  if (reason !== "stale_conflict" && reason !== "non_retriable") {
    return null;
  }

  return {
    id: toStringOrUndefined(value.id) ?? createNoticeId(),
    schemaVersion: REVIEW_MUTATION_RECONCILIATION_NOTICE_SCHEMA_VERSION,
    queueEntryId,
    transactionId,
    reason,
    message,
    reconciledAtUtc,
    errorCode: toStringOrUndefined(value.errorCode),
  };
}

async function readReconciliationNoticeDocument(): Promise<ReviewMutationReconciliationNoticeDocument> {
  const raw = await AsyncStorage.getItem(REVIEW_MUTATION_RECONCILIATION_NOTICE_STORAGE_KEY);
  if (!raw) {
    return EMPTY_RECONCILIATION_NOTICE_DOCUMENT;
  }

  try {
    const parsed = JSON.parse(raw) as unknown;

    if (Array.isArray(parsed)) {
      const notices = parsed
        .map((value) => normalizeNotice(value))
        .filter((value): value is ReviewMutationReconciliationNotice => value !== null)
        .sort((left, right) => right.reconciledAtUtc.localeCompare(left.reconciledAtUtc));

      return {
        version: REVIEW_MUTATION_RECONCILIATION_NOTICE_SCHEMA_VERSION,
        notices,
      };
    }

    if (!isRecord(parsed)) {
      throw new Error("Invalid reconciliation notice document.");
    }

    if (parsed.version !== REVIEW_MUTATION_RECONCILIATION_NOTICE_SCHEMA_VERSION) {
      throw new Error("Unexpected reconciliation notice schema version.");
    }

    const noticesValue = parsed.notices;
    if (!Array.isArray(noticesValue)) {
      throw new Error("Invalid reconciliation notice collection.");
    }

    const notices = noticesValue
      .map((value) => normalizeNotice(value))
      .filter((value): value is ReviewMutationReconciliationNotice => value !== null)
      .sort((left, right) => right.reconciledAtUtc.localeCompare(left.reconciledAtUtc));

    return {
      version: REVIEW_MUTATION_RECONCILIATION_NOTICE_SCHEMA_VERSION,
      notices,
    };
  } catch {
    await AsyncStorage.removeItem(REVIEW_MUTATION_RECONCILIATION_NOTICE_STORAGE_KEY);
    return EMPTY_RECONCILIATION_NOTICE_DOCUMENT;
  }
}

async function writeReconciliationNoticeDocument(
  document: ReviewMutationReconciliationNoticeDocument,
): Promise<void> {
  await AsyncStorage.setItem(
    REVIEW_MUTATION_RECONCILIATION_NOTICE_STORAGE_KEY,
    JSON.stringify(document),
  );
}

function readErrorCode(error: unknown): string | undefined {
  if (!(error instanceof MobileApiError)) {
    return undefined;
  }

  return error.code ?? `HTTP_${error.status}`;
}

function isLikelyStateMismatchValidation(error: MobileApiError): boolean {
  if (error.status !== 400 || error.code !== "validation_failed") {
    return false;
  }

  const normalizedMessage = error.message.toLowerCase();
  return (
    normalizedMessage.includes("reviewstatus") ||
    normalizedMessage.includes("needsreviewbyuserid") ||
    normalizedMessage.includes("does not exist") ||
    normalizedMessage.includes("route_to_needs_review")
  );
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

  const isStaleConflict =
    (error.status === 409 && error.code === "invalid_review_transition") ||
    (error.status === 404 && error.code === "transaction_not_found") ||
    isLikelyStateMismatchValidation(error);

  if (isStaleConflict) {
    return {
      retriable: false,
      errorCode,
      reconciliationReason: "stale_conflict",
      reconciliationMessage:
        "Queued review action was reconciled because backend review state changed. Refresh details before taking another action.",
    };
  }

  return {
    retriable: false,
    errorCode,
    reconciliationReason: "non_retriable",
    reconciliationMessage:
      "Queued review action was removed because backend rejected replay for the current state. Refresh details before retrying manually.",
  };
}

export function isRetriableReviewMutationError(error: unknown): boolean {
  return classifyReplayError(error).retriable;
}

export function getReviewMutationErrorCode(error: unknown): string | undefined {
  return readErrorCode(error);
}

export function getReviewMutationReconciliationDetails(
  error: unknown,
): ReviewMutationReconciliationDetails | null {
  const classification = classifyReplayError(error);
  if (classification.retriable) {
    return null;
  }

  return {
    reason: classification.reconciliationReason ?? "non_retriable",
    message:
      classification.reconciliationMessage ??
      "Queued review action was reconciled and removed.",
    errorCode: classification.errorCode,
  };
}

export async function recordReviewMutationReconciliationNotice(options: {
  queueEntryId: string;
  transactionId: string;
  reason: ReviewMutationReconciliationReason;
  errorCode?: string;
  message: string;
  reconciledAtUtc?: string;
}): Promise<ReviewMutationReconciliationNotice> {
  const reconciledAtUtc = options.reconciledAtUtc ?? new Date().toISOString();
  const document = await readReconciliationNoticeDocument();

  const nextNotice: ReviewMutationReconciliationNotice = {
    id: createNoticeId(),
    schemaVersion: REVIEW_MUTATION_RECONCILIATION_NOTICE_SCHEMA_VERSION,
    queueEntryId: options.queueEntryId,
    transactionId: options.transactionId,
    reason: options.reason,
    message: options.message,
    reconciledAtUtc,
    errorCode: options.errorCode,
  };

  const preserved = document.notices.filter(
    (notice) => notice.transactionId !== options.transactionId,
  );

  const nextDocument: ReviewMutationReconciliationNoticeDocument = {
    version: REVIEW_MUTATION_RECONCILIATION_NOTICE_SCHEMA_VERSION,
    notices: [nextNotice, ...preserved],
  };

  await writeReconciliationNoticeDocument(nextDocument);
  return nextNotice;
}

export async function getReviewMutationReconciliationNoticeForTransaction(
  transactionId: string,
): Promise<ReviewMutationReconciliationNotice | null> {
  const document = await readReconciliationNoticeDocument();
  return document.notices.find((notice) => notice.transactionId === transactionId) ?? null;
}

export async function clearReviewMutationReconciliationNoticeForTransaction(
  transactionId: string,
): Promise<void> {
  const document = await readReconciliationNoticeDocument();
  const filteredNotices = document.notices.filter(
    (notice) => notice.transactionId !== transactionId,
  );

  if (filteredNotices.length === document.notices.length) {
    return;
  }

  await writeReconciliationNoticeDocument({
    version: REVIEW_MUTATION_RECONCILIATION_NOTICE_SCHEMA_VERSION,
    notices: filteredNotices,
  });
}

async function replayReadyReviewMutations(): Promise<ReviewMutationRecoveryResult> {
  const queueEntries = await listReviewMutationQueueEntries();
  const replayReadyEntries = queueEntries.filter((entry) =>
    isReviewMutationReadyForReplay(entry),
  );

  const result: ReviewMutationRecoveryResult = {
    scannedCount: queueEntries.length,
    replayReadyCount: replayReadyEntries.length,
    replayedCount: 0,
    retriedCount: 0,
    reconciledCount: 0,
  };

  for (const queueEntry of replayReadyEntries) {
    try {
      await submitReviewAction(queueEntry.request);
      await removeReviewMutation(queueEntry.id);
      await clearReviewMutationReconciliationNoticeForTransaction(
        queueEntry.transactionId,
      );
      result.replayedCount += 1;
    } catch (error) {
      const classification = classifyReplayError(error);

      if (classification.retriable) {
        await markReviewMutationReplayFailure({
          entryId: queueEntry.id,
          errorCode: classification.errorCode,
        });
        result.retriedCount += 1;
        continue;
      }

      await removeReviewMutation(queueEntry.id);
      const reconciliationDetails = getReviewMutationReconciliationDetails(error);

      if (!reconciliationDetails) {
        continue;
      }

      await recordReviewMutationReconciliationNotice({
        queueEntryId: queueEntry.id,
        transactionId: queueEntry.transactionId,
        reason: reconciliationDetails.reason,
        errorCode: reconciliationDetails.errorCode,
        message: reconciliationDetails.message,
      });
      result.reconciledCount += 1;
    }
  }

  return result;
}

export function replayQueuedReviewMutations(): Promise<ReviewMutationRecoveryResult> {
  if (activeRecoveryRun) {
    return activeRecoveryRun;
  }

  const replayPromise = replayReadyReviewMutations();
  const trackedReplayPromise = replayPromise.finally(() => {
    if (activeRecoveryRun === trackedReplayPromise) {
      activeRecoveryRun = null;
    }
  });
  activeRecoveryRun = trackedReplayPromise;

  return activeRecoveryRun;
}
