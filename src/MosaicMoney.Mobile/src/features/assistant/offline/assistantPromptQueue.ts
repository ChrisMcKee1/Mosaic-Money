import AsyncStorage from "@react-native-async-storage/async-storage";
import { SchemaValidationError } from "../../../../../../packages/shared/src/validation";
import type { AssistantConversationMessageRequest } from "../contracts";

const ASSISTANT_PROMPT_QUEUE_STORAGE_KEY = "mosaic_money.mobile.assistant_prompt_queue.v1";
const ASSISTANT_PROMPT_QUEUE_SCHEMA_VERSION = 1;
const RETRY_BASE_DELAY_MS = 15_000;
const RETRY_MAX_DELAY_MS = 15 * 60_000;

export interface QueuedAssistantPromptRequest {
  conversationId: string;
  replayKey: string;
  summary: string;
  request: AssistantConversationMessageRequest;
}

export interface AssistantPromptQueueEntry {
  id: string;
  schemaVersion: 1;
  mutationType: "assistant-prompt";
  conversationId: string;
  replayKey: string;
  summary: string;
  request: AssistantConversationMessageRequest;
  createdAtUtc: string;
  updatedAtUtc: string;
  attemptCount: number;
  lastAttemptAtUtc?: string;
  nextAttemptAtUtc?: string;
  lastErrorCode?: string;
}

interface AssistantPromptQueueDocument {
  version: 1;
  entries: AssistantPromptQueueEntry[];
}

function createEmptyQueueDocument(): AssistantPromptQueueDocument {
  return {
    version: ASSISTANT_PROMPT_QUEUE_SCHEMA_VERSION,
    entries: [],
  };
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

function readSchemaVersion(value: unknown, path: string): 1 {
  const version = readNumber(value, path);
  if (version !== ASSISTANT_PROMPT_QUEUE_SCHEMA_VERSION) {
    throw new SchemaValidationError(`${path} must be ${ASSISTANT_PROMPT_QUEUE_SCHEMA_VERSION}.`);
  }

  return ASSISTANT_PROMPT_QUEUE_SCHEMA_VERSION;
}

function normalizePromptRequest(
  value: AssistantConversationMessageRequest,
  path: string,
): AssistantConversationMessageRequest {
  const message = value.message?.trim();
  if (!message) {
    throw new SchemaValidationError(`${path}.message is required.`);
  }

  const clientMessageId = value.clientMessageId?.trim();
  const userNote = value.userNote?.trim();

  return {
    message,
    clientMessageId: clientMessageId ? clientMessageId : null,
    userNote: userNote ? userNote : null,
  };
}

function parsePromptRequest(value: unknown, path: string): AssistantConversationMessageRequest {
  if (!isRecord(value)) {
    throw new SchemaValidationError(`${path} must be an object.`);
  }

  const message = readString(value.message, `${path}.message`).trim();
  if (!message) {
    throw new SchemaValidationError(`${path}.message cannot be empty.`);
  }

  return {
    message,
    clientMessageId: readOptionalString(value.clientMessageId, `${path}.clientMessageId`) ?? null,
    userNote: readOptionalString(value.userNote, `${path}.userNote`) ?? null,
  };
}

function parseQueueEntry(value: unknown, path: string): AssistantPromptQueueEntry {
  if (!isRecord(value)) {
    throw new SchemaValidationError(`${path} must be an object.`);
  }

  const schemaVersion = readSchemaVersion(value.schemaVersion, `${path}.schemaVersion`);
  const mutationType = readString(value.mutationType, `${path}.mutationType`);
  if (mutationType !== "assistant-prompt") {
    throw new SchemaValidationError(`${path}.mutationType must be assistant-prompt.`);
  }

  const attemptCount = readNumber(value.attemptCount, `${path}.attemptCount`);
  if (!Number.isInteger(attemptCount) || attemptCount < 0) {
    throw new SchemaValidationError(`${path}.attemptCount must be a non-negative integer.`);
  }

  return {
    id: readString(value.id, `${path}.id`),
    schemaVersion,
    mutationType: "assistant-prompt",
    conversationId: readString(value.conversationId, `${path}.conversationId`),
    replayKey: readString(value.replayKey, `${path}.replayKey`),
    summary: readString(value.summary, `${path}.summary`),
    request: parsePromptRequest(value.request, `${path}.request`),
    createdAtUtc: readString(value.createdAtUtc, `${path}.createdAtUtc`),
    updatedAtUtc: readString(value.updatedAtUtc, `${path}.updatedAtUtc`),
    attemptCount,
    lastAttemptAtUtc: readOptionalString(value.lastAttemptAtUtc, `${path}.lastAttemptAtUtc`),
    nextAttemptAtUtc: readOptionalString(value.nextAttemptAtUtc, `${path}.nextAttemptAtUtc`),
    lastErrorCode: readOptionalString(value.lastErrorCode, `${path}.lastErrorCode`),
  };
}

function parseQueueDocument(value: unknown): AssistantPromptQueueDocument {
  if (!isRecord(value)) {
    throw new SchemaValidationError("assistantPromptQueue must be an object.");
  }

  const version = readSchemaVersion(value.version, "assistantPromptQueue.version");
  if (!Array.isArray(value.entries)) {
    throw new SchemaValidationError("assistantPromptQueue.entries must be an array.");
  }

  return {
    version,
    entries: value.entries.map((entry, index) => parseQueueEntry(entry, `assistantPromptQueue.entries[${index}]`)),
  };
}

function createQueueEntryId(): string {
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
}

function computeNextRetryDelayMs(attemptCount: number): number {
  const boundedExponent = Math.max(0, Math.min(attemptCount - 1, 6));
  return Math.min(RETRY_MAX_DELAY_MS, RETRY_BASE_DELAY_MS * (2 ** boundedExponent));
}

async function readQueueDocument(): Promise<AssistantPromptQueueDocument> {
  const raw = await AsyncStorage.getItem(ASSISTANT_PROMPT_QUEUE_STORAGE_KEY);
  if (!raw) {
    return createEmptyQueueDocument();
  }

  try {
    const parsed = JSON.parse(raw) as unknown;
    return parseQueueDocument(parsed);
  } catch {
    await AsyncStorage.removeItem(ASSISTANT_PROMPT_QUEUE_STORAGE_KEY);
    return createEmptyQueueDocument();
  }
}

async function writeQueueDocument(document: AssistantPromptQueueDocument): Promise<void> {
  await AsyncStorage.setItem(ASSISTANT_PROMPT_QUEUE_STORAGE_KEY, JSON.stringify(document));
}

export async function listAssistantPromptQueueEntries(): Promise<AssistantPromptQueueEntry[]> {
  const queue = await readQueueDocument();
  return [...queue.entries].sort((left, right) => left.createdAtUtc.localeCompare(right.createdAtUtc));
}

export function isAssistantPromptReadyForReplay(
  entry: AssistantPromptQueueEntry,
  nowUtc: Date = new Date(),
): boolean {
  if (!entry.nextAttemptAtUtc) {
    return true;
  }

  return entry.nextAttemptAtUtc <= nowUtc.toISOString();
}

export async function enqueueAssistantPrompt(
  input: QueuedAssistantPromptRequest,
): Promise<AssistantPromptQueueEntry> {
  const normalizedConversationId = input.conversationId.trim();
  if (!normalizedConversationId) {
    throw new SchemaValidationError("assistantPrompt.conversationId is required.");
  }

  const replayKey = input.replayKey.trim();
  if (!replayKey) {
    throw new SchemaValidationError("assistantPrompt.replayKey is required.");
  }

  const summary = input.summary.trim();
  if (!summary) {
    throw new SchemaValidationError("assistantPrompt.summary is required.");
  }

  const normalizedRequest = normalizePromptRequest(input.request, "assistantPrompt.request");

  const queue = await readQueueDocument();
  const existing = queue.entries.find((entry) => entry.replayKey === replayKey);
  if (existing) {
    return existing;
  }

  const nowUtc = new Date().toISOString();
  const entry: AssistantPromptQueueEntry = {
    id: createQueueEntryId(),
    schemaVersion: ASSISTANT_PROMPT_QUEUE_SCHEMA_VERSION,
    mutationType: "assistant-prompt",
    conversationId: normalizedConversationId,
    replayKey,
    summary,
    request: normalizedRequest,
    createdAtUtc: nowUtc,
    updatedAtUtc: nowUtc,
    attemptCount: 0,
  };

  queue.entries.push(entry);
  queue.entries.sort((left, right) => left.createdAtUtc.localeCompare(right.createdAtUtc));
  await writeQueueDocument(queue);

  return entry;
}

export async function removeAssistantPrompt(entryId: string): Promise<void> {
  const queue = await readQueueDocument();
  const nextEntries = queue.entries.filter((entry) => entry.id !== entryId);

  if (nextEntries.length === queue.entries.length) {
    return;
  }

  await writeQueueDocument({
    version: ASSISTANT_PROMPT_QUEUE_SCHEMA_VERSION,
    entries: nextEntries,
  });
}

export async function markAssistantPromptReplayFailure(
  entryId: string,
  errorCode?: string,
): Promise<AssistantPromptQueueEntry | null> {
  const queue = await readQueueDocument();
  const targetIndex = queue.entries.findIndex((entry) => entry.id === entryId);
  if (targetIndex < 0) {
    return null;
  }

  const existing = queue.entries[targetIndex];
  const attemptedAtUtc = new Date().toISOString();
  const attemptCount = existing.attemptCount + 1;
  const nextAttemptAtUtc = new Date(
    Date.parse(attemptedAtUtc) + computeNextRetryDelayMs(attemptCount),
  ).toISOString();

  const updated: AssistantPromptQueueEntry = {
    ...existing,
    attemptCount,
    updatedAtUtc: attemptedAtUtc,
    lastAttemptAtUtc: attemptedAtUtc,
    nextAttemptAtUtc,
    lastErrorCode: errorCode,
  };

  const nextEntries = [...queue.entries];
  nextEntries[targetIndex] = updated;

  await writeQueueDocument({
    version: ASSISTANT_PROMPT_QUEUE_SCHEMA_VERSION,
    entries: nextEntries,
  });

  return updated;
}

export async function clearAssistantPromptBackoff(entryId: string): Promise<AssistantPromptQueueEntry | null> {
  const queue = await readQueueDocument();
  const targetIndex = queue.entries.findIndex((entry) => entry.id === entryId);
  if (targetIndex < 0) {
    return null;
  }

  const existing = queue.entries[targetIndex];
  const updated: AssistantPromptQueueEntry = {
    ...existing,
    updatedAtUtc: new Date().toISOString(),
    nextAttemptAtUtc: undefined,
    lastErrorCode: undefined,
  };

  const nextEntries = [...queue.entries];
  nextEntries[targetIndex] = updated;

  await writeQueueDocument({
    version: ASSISTANT_PROMPT_QUEUE_SCHEMA_VERSION,
    entries: nextEntries,
  });

  return updated;
}

export function getAssistantQueuedPromptReplayRequest(
  entry: AssistantPromptQueueEntry,
): AssistantConversationMessageRequest {
  return {
    message: entry.request.message,
    clientMessageId: entry.request.clientMessageId ?? null,
    userNote: entry.request.userNote ?? null,
  };
}
