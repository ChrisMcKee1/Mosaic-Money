import AsyncStorage from "@react-native-async-storage/async-storage";
import type { CategoryScope, QueuedCategoryMutationRequest } from "../contracts/CategoryLifecycleContracts";
import { SchemaValidationError } from "../../../../../../packages/shared/src/validation";

const CATEGORY_MUTATION_QUEUE_STORAGE_KEY = "mosaic_money.mobile.category_mutation_queue.v1";
const CATEGORY_MUTATION_NOTICE_STORAGE_KEY = "mosaic_money.mobile.category_mutation_reconciliation_notices.v1";

const CATEGORY_MUTATION_QUEUE_SCHEMA_VERSION = 1;
const RETRY_BASE_DELAY_MS = 15_000;
const RETRY_MAX_DELAY_MS = 15 * 60_000;

export type CategoryMutationReconciliationReason = "stale_conflict" | "non_retriable";

export interface CategoryMutationQueueEntry {
  id: string;
  schemaVersion: 1;
  mutationType: "category-settings";
  method: "POST" | "PATCH" | "DELETE";
  path: string;
  body?: unknown;
  scope: CategoryScope;
  replayKey: string;
  summary: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  attemptCount: number;
  lastAttemptAtUtc?: string;
  nextAttemptAtUtc?: string;
  lastErrorCode?: string;
}

export interface CategoryMutationReconciliationNotice {
  id: string;
  schemaVersion: 1;
  queueEntryId: string;
  path: string;
  scope: CategoryScope;
  reason: CategoryMutationReconciliationReason;
  message: string;
  reconciledAtUtc: string;
  errorCode?: string;
}

interface CategoryMutationQueueDocument {
  version: 1;
  entries: CategoryMutationQueueEntry[];
}

interface CategoryMutationNoticeDocument {
  version: 1;
  notices: CategoryMutationReconciliationNotice[];
}

function createEmptyQueueDocument(): CategoryMutationQueueDocument {
  return {
    version: CATEGORY_MUTATION_QUEUE_SCHEMA_VERSION,
    entries: [],
  };
}

function createEmptyNoticeDocument(): CategoryMutationNoticeDocument {
  return {
    version: CATEGORY_MUTATION_QUEUE_SCHEMA_VERSION,
    notices: [],
  };
}

function createId(): string {
  return `cmq-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
}

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

function readScope(value: unknown, path: string): CategoryScope {
  const scope = readString(value, path);
  if (scope !== "User" && scope !== "HouseholdShared" && scope !== "Platform") {
    throw new SchemaValidationError(`${path} must be User, HouseholdShared, or Platform.`);
  }

  return scope;
}

function readMethod(value: unknown, path: string): "POST" | "PATCH" | "DELETE" {
  const method = readString(value, path);
  if (method !== "POST" && method !== "PATCH" && method !== "DELETE") {
    throw new SchemaValidationError(`${path} must be POST, PATCH, or DELETE.`);
  }

  return method;
}

function readReconciliationReason(value: unknown, path: string): CategoryMutationReconciliationReason {
  const reason = readString(value, path);
  if (reason !== "stale_conflict" && reason !== "non_retriable") {
    throw new SchemaValidationError(`${path} must be stale_conflict or non_retriable.`);
  }

  return reason;
}

function parseQueueEntry(value: unknown, path: string): CategoryMutationQueueEntry {
  if (!isRecord(value)) {
    throw new SchemaValidationError(`${path} must be an object.`);
  }

  return {
    id: readString(value.id, `${path}.id`),
    schemaVersion: CATEGORY_MUTATION_QUEUE_SCHEMA_VERSION,
    mutationType: "category-settings",
    method: readMethod(value.method, `${path}.method`),
    path: readString(value.path, `${path}.path`),
    body: value.body,
    scope: readScope(value.scope, `${path}.scope`),
    replayKey: readString(value.replayKey, `${path}.replayKey`),
    summary: readString(value.summary, `${path}.summary`),
    createdAtUtc: readString(value.createdAtUtc, `${path}.createdAtUtc`),
    updatedAtUtc: readString(value.updatedAtUtc, `${path}.updatedAtUtc`),
    attemptCount: readNumber(value.attemptCount, `${path}.attemptCount`),
    lastAttemptAtUtc: readOptionalString(value.lastAttemptAtUtc, `${path}.lastAttemptAtUtc`),
    nextAttemptAtUtc: readOptionalString(value.nextAttemptAtUtc, `${path}.nextAttemptAtUtc`),
    lastErrorCode: readOptionalString(value.lastErrorCode, `${path}.lastErrorCode`),
  };
}

function parseQueueDocument(value: unknown): CategoryMutationQueueDocument {
  if (!isRecord(value)) {
    throw new SchemaValidationError("categoryMutationQueue must be an object.");
  }

  const version = readNumber(value.version, "categoryMutationQueue.version");
  if (version !== CATEGORY_MUTATION_QUEUE_SCHEMA_VERSION) {
    throw new SchemaValidationError("categoryMutationQueue.version is unsupported.");
  }

  if (!Array.isArray(value.entries)) {
    throw new SchemaValidationError("categoryMutationQueue.entries must be an array.");
  }

  return {
    version: CATEGORY_MUTATION_QUEUE_SCHEMA_VERSION,
    entries: value.entries.map((entry, index) => parseQueueEntry(entry, `categoryMutationQueue.entries[${index}]`)),
  };
}

function parseNotice(value: unknown, path: string): CategoryMutationReconciliationNotice {
  if (!isRecord(value)) {
    throw new SchemaValidationError(`${path} must be an object.`);
  }

  return {
    id: readString(value.id, `${path}.id`),
    schemaVersion: CATEGORY_MUTATION_QUEUE_SCHEMA_VERSION,
    queueEntryId: readString(value.queueEntryId, `${path}.queueEntryId`),
    path: readString(value.path, `${path}.path`),
    scope: readScope(value.scope, `${path}.scope`),
    reason: readReconciliationReason(value.reason, `${path}.reason`),
    message: readString(value.message, `${path}.message`),
    reconciledAtUtc: readString(value.reconciledAtUtc, `${path}.reconciledAtUtc`),
    errorCode: readOptionalString(value.errorCode, `${path}.errorCode`),
  };
}

function parseNoticeDocument(value: unknown): CategoryMutationNoticeDocument {
  if (!isRecord(value)) {
    throw new SchemaValidationError("categoryMutationNotice must be an object.");
  }

  const version = readNumber(value.version, "categoryMutationNotice.version");
  if (version !== CATEGORY_MUTATION_QUEUE_SCHEMA_VERSION) {
    throw new SchemaValidationError("categoryMutationNotice.version is unsupported.");
  }

  if (!Array.isArray(value.notices)) {
    throw new SchemaValidationError("categoryMutationNotice.notices must be an array.");
  }

  return {
    version: CATEGORY_MUTATION_QUEUE_SCHEMA_VERSION,
    notices: value.notices.map((notice, index) => parseNotice(notice, `categoryMutationNotice.notices[${index}]`)),
  };
}

async function readQueueDocument(): Promise<CategoryMutationQueueDocument> {
  const raw = await AsyncStorage.getItem(CATEGORY_MUTATION_QUEUE_STORAGE_KEY);
  if (!raw) {
    return createEmptyQueueDocument();
  }

  try {
    return parseQueueDocument(JSON.parse(raw));
  } catch {
    return createEmptyQueueDocument();
  }
}

async function writeQueueDocument(document: CategoryMutationQueueDocument): Promise<void> {
  await AsyncStorage.setItem(CATEGORY_MUTATION_QUEUE_STORAGE_KEY, JSON.stringify(document));
}

async function readNoticeDocument(): Promise<CategoryMutationNoticeDocument> {
  const raw = await AsyncStorage.getItem(CATEGORY_MUTATION_NOTICE_STORAGE_KEY);
  if (!raw) {
    return createEmptyNoticeDocument();
  }

  try {
    return parseNoticeDocument(JSON.parse(raw));
  } catch {
    return createEmptyNoticeDocument();
  }
}

async function writeNoticeDocument(document: CategoryMutationNoticeDocument): Promise<void> {
  await AsyncStorage.setItem(CATEGORY_MUTATION_NOTICE_STORAGE_KEY, JSON.stringify(document));
}

export async function enqueueCategoryMutation(request: QueuedCategoryMutationRequest): Promise<CategoryMutationQueueEntry> {
  const queue = await readQueueDocument();
  const existing = queue.entries.find((entry) => entry.replayKey === request.replayKey);
  if (existing) {
    return existing;
  }

  const now = new Date().toISOString();
  const entry: CategoryMutationQueueEntry = {
    id: createId(),
    schemaVersion: CATEGORY_MUTATION_QUEUE_SCHEMA_VERSION,
    mutationType: "category-settings",
    method: request.method,
    path: request.path,
    body: request.body,
    scope: request.scope,
    replayKey: request.replayKey,
    summary: request.summary,
    createdAtUtc: now,
    updatedAtUtc: now,
    attemptCount: 0,
  };

  queue.entries.push(entry);
  await writeQueueDocument(queue);

  return entry;
}

export async function listCategoryMutationQueueEntries(): Promise<CategoryMutationQueueEntry[]> {
  const queue = await readQueueDocument();
  return queue.entries;
}

export function isCategoryMutationReadyForReplay(entry: CategoryMutationQueueEntry, nowUtc: Date = new Date()): boolean {
  if (!entry.nextAttemptAtUtc) {
    return true;
  }

  const nextAttemptAt = Date.parse(entry.nextAttemptAtUtc);
  if (Number.isNaN(nextAttemptAt)) {
    return true;
  }

  return nextAttemptAt <= nowUtc.getTime();
}

export async function removeCategoryMutation(entryId: string): Promise<void> {
  const queue = await readQueueDocument();
  queue.entries = queue.entries.filter((entry) => entry.id !== entryId);
  await writeQueueDocument(queue);
}

export async function markCategoryMutationReplayFailure(entryId: string, errorCode?: string): Promise<void> {
  const queue = await readQueueDocument();
  const targetIndex = queue.entries.findIndex((entry) => entry.id === entryId);
  if (targetIndex < 0) {
    return;
  }

  const target = queue.entries[targetIndex];
  const attemptCount = target.attemptCount + 1;
  const delay = Math.min(RETRY_BASE_DELAY_MS * 2 ** Math.max(0, attemptCount - 1), RETRY_MAX_DELAY_MS);
  const now = new Date();

  queue.entries[targetIndex] = {
    ...target,
    attemptCount,
    lastAttemptAtUtc: now.toISOString(),
    nextAttemptAtUtc: new Date(now.getTime() + delay).toISOString(),
    lastErrorCode: errorCode,
    updatedAtUtc: now.toISOString(),
  };

  await writeQueueDocument(queue);
}

export async function addCategoryMutationReconciliationNotice(
  entry: CategoryMutationQueueEntry,
  reason: CategoryMutationReconciliationReason,
  message: string,
  errorCode?: string,
): Promise<CategoryMutationReconciliationNotice> {
  const notice: CategoryMutationReconciliationNotice = {
    id: createId(),
    schemaVersion: CATEGORY_MUTATION_QUEUE_SCHEMA_VERSION,
    queueEntryId: entry.id,
    path: entry.path,
    scope: entry.scope,
    reason,
    message,
    reconciledAtUtc: new Date().toISOString(),
    errorCode,
  };

  const noticeDocument = await readNoticeDocument();
  noticeDocument.notices.push(notice);
  await writeNoticeDocument(noticeDocument);

  return notice;
}

export async function listCategoryMutationReconciliationNotices(): Promise<CategoryMutationReconciliationNotice[]> {
  const noticeDocument = await readNoticeDocument();
  return noticeDocument.notices;
}
