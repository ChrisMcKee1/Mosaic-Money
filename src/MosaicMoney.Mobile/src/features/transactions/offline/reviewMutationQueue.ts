import AsyncStorage from "@react-native-async-storage/async-storage";
import type { ReviewActionRequest } from "../contracts";
import { SchemaValidationError, parseReviewActionRequest } from "../../../../../../packages/shared/src/validation";

const REVIEW_MUTATION_QUEUE_STORAGE_KEY = "mosaic_money.mobile.review_mutation_queue.v1";
const REVIEW_MUTATION_QUEUE_SCHEMA_VERSION = 1;
const RETRY_BASE_DELAY_MS = 15_000;
const RETRY_MAX_DELAY_MS = 15 * 60_000;

export type ReviewMutationKind = "approve" | "reject";

export interface ReviewMutationQueueEntry {
  id: string;
  schemaVersion: 1;
  mutationType: "review-action";
  transactionId: string;
  actionKind: ReviewMutationKind;
  replayKey: string;
  request: ReviewActionRequest;
  createdAtUtc: string;
  updatedAtUtc: string;
  attemptCount: number;
  lastAttemptAtUtc?: string;
  nextAttemptAtUtc?: string;
  lastErrorCode?: string;
}

interface ReviewMutationQueueDocument {
  version: 1;
  entries: ReviewMutationQueueEntry[];
}

const EMPTY_QUEUE_DOCUMENT: ReviewMutationQueueDocument = {
  version: REVIEW_MUTATION_QUEUE_SCHEMA_VERSION,
  entries: [],
};

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

function readString(value: unknown, path: string): string {
  if (typeof value !== "string") {
    throw new SchemaValidationError(`${path} must be a string.`);
  }

  return value;
}

function readOptionalString(value: unknown, path: string): string | undefined {
  if (value === undefined || value === null) {
    return undefined;
  }

  return readString(value, path);
}

function readNumber(value: unknown, path: string): number {
  if (typeof value !== "number" || Number.isNaN(value)) {
    throw new SchemaValidationError(`${path} must be a finite number.`);
  }

  return value;
}

function readQueueSchemaVersion(value: unknown, path: string): 1 {
  const version = readNumber(value, path);
  if (version !== REVIEW_MUTATION_QUEUE_SCHEMA_VERSION) {
    throw new SchemaValidationError(
      `${path} must be ${REVIEW_MUTATION_QUEUE_SCHEMA_VERSION}.`,
    );
  }

  return REVIEW_MUTATION_QUEUE_SCHEMA_VERSION;
}

function readActionKind(value: unknown, path: string): ReviewMutationKind {
  const actionKind = readString(value, path);
  if (actionKind !== "approve" && actionKind !== "reject") {
    throw new SchemaValidationError(`${path} must be approve or reject.`);
  }

  return actionKind;
}

function normalizeReviewReason(value: string | undefined): string {
  if (!value) {
    return "";
  }

  return value.trim().replace(/\s+/g, " ");
}

function validateRequestForActionKind(
  actionKind: ReviewMutationKind,
  request: ReviewActionRequest,
  path: string,
): void {
  if (actionKind === "approve") {
    if (request.action !== "approve") {
      throw new SchemaValidationError(`${path}.action must be approve for approve mutations.`);
    }

    return;
  }

  if (request.action !== "route_to_needs_review") {
    throw new SchemaValidationError(
      `${path}.action must be route_to_needs_review for reject mutations.`,
    );
  }

  if (!request.needsReviewByUserId?.trim()) {
    throw new SchemaValidationError(
      `${path}.needsReviewByUserId is required for reject mutations.`,
    );
  }

  if (!normalizeReviewReason(request.reviewReason)) {
    throw new SchemaValidationError(`${path}.reviewReason is required for reject mutations.`);
  }
}

function parseReviewMutationQueueEntry(
  value: unknown,
  path: string,
): ReviewMutationQueueEntry {
  if (!isRecord(value)) {
    throw new SchemaValidationError(`${path} must be an object.`);
  }

  const schemaVersion = readQueueSchemaVersion(value.schemaVersion, `${path}.schemaVersion`);
  const mutationType = readString(value.mutationType, `${path}.mutationType`);
  if (mutationType !== "review-action") {
    throw new SchemaValidationError(`${path}.mutationType must be review-action.`);
  }

  const actionKind = readActionKind(value.actionKind, `${path}.actionKind`);
  const transactionId = readString(value.transactionId, `${path}.transactionId`);
  const request = parseReviewActionRequest(value.request, `${path}.request`);

  if (request.transactionId !== transactionId) {
    throw new SchemaValidationError(
      `${path}.request.transactionId must match ${path}.transactionId.`,
    );
  }

  validateRequestForActionKind(actionKind, request, `${path}.request`);

  const attemptCount = readNumber(value.attemptCount, `${path}.attemptCount`);
  if (!Number.isInteger(attemptCount) || attemptCount < 0) {
    throw new SchemaValidationError(`${path}.attemptCount must be a non-negative integer.`);
  }

  return {
    id: readString(value.id, `${path}.id`),
    schemaVersion,
    mutationType: "review-action",
    transactionId,
    actionKind,
    replayKey: readString(value.replayKey, `${path}.replayKey`),
    request,
    createdAtUtc: readString(value.createdAtUtc, `${path}.createdAtUtc`),
    updatedAtUtc: readString(value.updatedAtUtc, `${path}.updatedAtUtc`),
    attemptCount,
    lastAttemptAtUtc: readOptionalString(value.lastAttemptAtUtc, `${path}.lastAttemptAtUtc`),
    nextAttemptAtUtc: readOptionalString(value.nextAttemptAtUtc, `${path}.nextAttemptAtUtc`),
    lastErrorCode: readOptionalString(value.lastErrorCode, `${path}.lastErrorCode`),
  };
}

function parseReviewMutationQueueDocument(value: unknown): ReviewMutationQueueDocument {
  if (Array.isArray(value)) {
    return {
      version: REVIEW_MUTATION_QUEUE_SCHEMA_VERSION,
      entries: value
        .map((entry, index) => parseReviewMutationQueueEntry(entry, `reviewMutationQueue[${index}]`))
        .sort((left, right) => left.createdAtUtc.localeCompare(right.createdAtUtc)),
    };
  }

  if (!isRecord(value)) {
    throw new SchemaValidationError("reviewMutationQueue must be an object.");
  }

  const version = readQueueSchemaVersion(value.version, "reviewMutationQueue.version");
  const entriesValue = value.entries;

  if (!Array.isArray(entriesValue)) {
    throw new SchemaValidationError("reviewMutationQueue.entries must be an array.");
  }

  const entries = entriesValue
    .map((entry, index) => parseReviewMutationQueueEntry(entry, `reviewMutationQueue.entries[${index}]`))
    .sort((left, right) => left.createdAtUtc.localeCompare(right.createdAtUtc));

  return {
    version,
    entries,
  };
}

function createQueueEntryId(): string {
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
}

function buildReplayKey(actionKind: ReviewMutationKind, request: ReviewActionRequest): string {
  return [
    request.transactionId,
    actionKind,
    request.action,
    normalizeReviewReason(request.reviewReason),
    request.needsReviewByUserId?.trim() ?? "",
    request.subcategoryId?.trim() ?? "",
    request.excludeFromBudget === undefined ? "" : String(request.excludeFromBudget),
    request.isExtraPrincipal === undefined ? "" : String(request.isExtraPrincipal),
  ].join("|");
}

function computeNextRetryDelayMs(attemptCount: number): number {
  const boundedExponent = Math.max(0, Math.min(attemptCount - 1, 6));
  return Math.min(RETRY_MAX_DELAY_MS, RETRY_BASE_DELAY_MS * (2 ** boundedExponent));
}

function indexOfEntry(entries: ReviewMutationQueueEntry[], entryId: string): number {
  return entries.findIndex((entry) => entry.id === entryId);
}

async function writeQueueDocument(document: ReviewMutationQueueDocument): Promise<void> {
  await AsyncStorage.setItem(REVIEW_MUTATION_QUEUE_STORAGE_KEY, JSON.stringify(document));
}

async function readQueueDocument(): Promise<ReviewMutationQueueDocument> {
  const raw = await AsyncStorage.getItem(REVIEW_MUTATION_QUEUE_STORAGE_KEY);
  if (!raw) {
    return EMPTY_QUEUE_DOCUMENT;
  }

  try {
    const parsed = JSON.parse(raw) as unknown;
    const document = parseReviewMutationQueueDocument(parsed);
    return document;
  } catch {
    await AsyncStorage.removeItem(REVIEW_MUTATION_QUEUE_STORAGE_KEY);
    return EMPTY_QUEUE_DOCUMENT;
  }
}

export function mapReviewQueueEntryForDisplay(entry: ReviewMutationQueueEntry): {
  kind: ReviewMutationKind;
  request: ReviewActionRequest;
  queuedAtUtc: string;
  attemptCount: number;
  nextAttemptAtUtc?: string;
  queueEntryId: string;
} {
  return {
    kind: entry.actionKind,
    request: entry.request,
    queuedAtUtc: entry.createdAtUtc,
    attemptCount: entry.attemptCount,
    nextAttemptAtUtc: entry.nextAttemptAtUtc,
    queueEntryId: entry.id,
  };
}

export function isReviewMutationReadyForReplay(
  entry: ReviewMutationQueueEntry,
  now: Date = new Date(),
): boolean {
  if (!entry.nextAttemptAtUtc) {
    return true;
  }

  return entry.nextAttemptAtUtc <= now.toISOString();
}

export async function listReviewMutationQueueEntries(): Promise<ReviewMutationQueueEntry[]> {
  const document = await readQueueDocument();
  return [...document.entries];
}

export async function getPendingReviewMutationForTransaction(
  transactionId: string,
): Promise<ReviewMutationQueueEntry | null> {
  const document = await readQueueDocument();
  return document.entries.find((entry) => entry.transactionId === transactionId) ?? null;
}

export async function enqueueReviewMutation(options: {
  actionKind: ReviewMutationKind;
  request: ReviewActionRequest;
}): Promise<ReviewMutationQueueEntry> {
  const normalizedRequest = parseReviewActionRequest(options.request, "reviewMutation.request");
  validateRequestForActionKind(options.actionKind, normalizedRequest, "reviewMutation.request");

  const document = await readQueueDocument();
  const replayKey = buildReplayKey(options.actionKind, normalizedRequest);
  const duplicate = document.entries.find((entry) => entry.replayKey === replayKey);

  if (duplicate) {
    return duplicate;
  }

  const nowUtc = new Date().toISOString();
  const nextEntry: ReviewMutationQueueEntry = {
    id: createQueueEntryId(),
    schemaVersion: REVIEW_MUTATION_QUEUE_SCHEMA_VERSION,
    mutationType: "review-action",
    transactionId: normalizedRequest.transactionId,
    actionKind: options.actionKind,
    replayKey,
    request: normalizedRequest,
    createdAtUtc: nowUtc,
    updatedAtUtc: nowUtc,
    attemptCount: 0,
  };

  const preservedEntries = document.entries.filter(
    (entry) => entry.transactionId !== normalizedRequest.transactionId,
  );

  const nextDocument: ReviewMutationQueueDocument = {
    version: REVIEW_MUTATION_QUEUE_SCHEMA_VERSION,
    entries: [...preservedEntries, nextEntry].sort((left, right) =>
      left.createdAtUtc.localeCompare(right.createdAtUtc),
    ),
  };

  await writeQueueDocument(nextDocument);
  return nextEntry;
}

export async function removeReviewMutation(entryId: string): Promise<void> {
  const document = await readQueueDocument();
  const filteredEntries = document.entries.filter((entry) => entry.id !== entryId);

  if (filteredEntries.length === document.entries.length) {
    return;
  }

  await writeQueueDocument({
    version: REVIEW_MUTATION_QUEUE_SCHEMA_VERSION,
    entries: filteredEntries,
  });
}

export async function removeReviewMutationForTransaction(
  transactionId: string,
): Promise<void> {
  const document = await readQueueDocument();
  const filteredEntries = document.entries.filter(
    (entry) => entry.transactionId !== transactionId,
  );

  if (filteredEntries.length === document.entries.length) {
    return;
  }

  await writeQueueDocument({
    version: REVIEW_MUTATION_QUEUE_SCHEMA_VERSION,
    entries: filteredEntries,
  });
}

export async function markReviewMutationReplayFailure(options: {
  entryId: string;
  errorCode?: string;
  attemptedAtUtc?: string;
}): Promise<ReviewMutationQueueEntry | null> {
  const document = await readQueueDocument();
  const index = indexOfEntry(document.entries, options.entryId);
  if (index < 0) {
    return null;
  }

  const existing = document.entries[index];
  const attemptedAtUtc = options.attemptedAtUtc ?? new Date().toISOString();
  const nextAttemptCount = existing.attemptCount + 1;
  const nextDelayMs = computeNextRetryDelayMs(nextAttemptCount);
  const nextAttemptAtUtc = new Date(Date.parse(attemptedAtUtc) + nextDelayMs).toISOString();

  const updated: ReviewMutationQueueEntry = {
    ...existing,
    attemptCount: nextAttemptCount,
    updatedAtUtc: attemptedAtUtc,
    lastAttemptAtUtc: attemptedAtUtc,
    nextAttemptAtUtc,
    lastErrorCode: options.errorCode,
  };

  const nextEntries = [...document.entries];
  nextEntries[index] = updated;

  await writeQueueDocument({
    version: REVIEW_MUTATION_QUEUE_SCHEMA_VERSION,
    entries: nextEntries,
  });

  return updated;
}

export async function clearReviewMutationBackoff(entryId: string): Promise<ReviewMutationQueueEntry | null> {
  const document = await readQueueDocument();
  const index = indexOfEntry(document.entries, entryId);
  if (index < 0) {
    return null;
  }

  const existing = document.entries[index];
  const updated: ReviewMutationQueueEntry = {
    ...existing,
    updatedAtUtc: new Date().toISOString(),
    nextAttemptAtUtc: undefined,
    lastErrorCode: undefined,
  };

  const nextEntries = [...document.entries];
  nextEntries[index] = updated;

  await writeQueueDocument({
    version: REVIEW_MUTATION_QUEUE_SCHEMA_VERSION,
    entries: nextEntries,
  });

  return updated;
}
