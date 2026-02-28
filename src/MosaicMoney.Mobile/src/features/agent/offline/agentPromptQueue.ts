import AsyncStorage from "@react-native-async-storage/async-storage";
import { SchemaValidationError } from "../../../../../../packages/shared/src/validation";
import type { AgentConversationMessageRequest } from "../contracts";

const AGENT_PROMPT_QUEUE_STORAGE_KEY = "mosaic_money.mobile.assistant_prompt_queue.v1";
const AGENT_PROMPT_QUEUE_SCHEMA_VERSION = 1;
const RETRY_BASE_DELAY_MS = 15_000;
const RETRY_MAX_DELAY_MS = 15 * 60_000;

export interface QueuedAgentPromptRequest {
  conversationId: string;
  replayKey: string;
  summary: string;
  request: AgentConversationMessageRequest;
}

export interface AgentPromptQueueEntry {
  id: string;
  schemaVersion: 1;
  mutationType: "assistant-prompt";
  conversationId: string;
  replayKey: string;
  summary: string;
  request: AgentConversationMessageRequest;
  createdAtUtc: string;
  updatedAtUtc: string;
  attemptCount: number;
  lastAttemptAtUtc?: string;
  nextAttemptAtUtc?: string;
  lastErrorCode?: string;
}

interface AgentPromptQueueDocument {
  version: 1;
  entries: AgentPromptQueueEntry[];
}

function createEmptyQueueDocument(): AgentPromptQueueDocument {
  return {
    version: AGENT_PROMPT_QUEUE_SCHEMA_VERSION,
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
  if (version !== AGENT_PROMPT_QUEUE_SCHEMA_VERSION) {
    throw new SchemaValidationError(`${path} must be ${AGENT_PROMPT_QUEUE_SCHEMA_VERSION}.`);
  }

  return AGENT_PROMPT_QUEUE_SCHEMA_VERSION;
}

function normalizePromptRequest(
  value: AgentConversationMessageRequest,
  path: string,
): AgentConversationMessageRequest {
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

function parsePromptRequest(value: unknown, path: string): AgentConversationMessageRequest {
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

function parseQueueEntry(value: unknown, path: string): AgentPromptQueueEntry {
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

function parseQueueDocument(value: unknown): AgentPromptQueueDocument {
  if (!isRecord(value)) {
    throw new SchemaValidationError("agentPromptQueue must be an object.");
  }

  const version = readSchemaVersion(value.version, "agentPromptQueue.version");
  if (!Array.isArray(value.entries)) {
    throw new SchemaValidationError("agentPromptQueue.entries must be an array.");
  }

  return {
    version,
    entries: value.entries.map((entry, index) => parseQueueEntry(entry, `agentPromptQueue.entries[${index}]`)),
  };
}

function createQueueEntryId(): string {
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
}

function computeNextRetryDelayMs(attemptCount: number): number {
  const boundedExponent = Math.max(0, Math.min(attemptCount - 1, 6));
  return Math.min(RETRY_MAX_DELAY_MS, RETRY_BASE_DELAY_MS * (2 ** boundedExponent));
}

async function readQueueDocument(): Promise<AgentPromptQueueDocument> {
  const raw = await AsyncStorage.getItem(AGENT_PROMPT_QUEUE_STORAGE_KEY);
  if (!raw) {
    return createEmptyQueueDocument();
  }

  try {
    const parsed = JSON.parse(raw) as unknown;
    return parseQueueDocument(parsed);
  } catch {
    await AsyncStorage.removeItem(AGENT_PROMPT_QUEUE_STORAGE_KEY);
    return createEmptyQueueDocument();
  }
}

async function writeQueueDocument(document: AgentPromptQueueDocument): Promise<void> {
  await AsyncStorage.setItem(AGENT_PROMPT_QUEUE_STORAGE_KEY, JSON.stringify(document));
}

export async function listAgentPromptQueueEntries(): Promise<AgentPromptQueueEntry[]> {
  const queue = await readQueueDocument();
  return [...queue.entries].sort((left, right) => left.createdAtUtc.localeCompare(right.createdAtUtc));
}

export function isAgentPromptReadyForReplay(
  entry: AgentPromptQueueEntry,
  nowUtc: Date = new Date(),
): boolean {
  if (!entry.nextAttemptAtUtc) {
    return true;
  }

  return entry.nextAttemptAtUtc <= nowUtc.toISOString();
}

export async function enqueueAgentPrompt(
  input: QueuedAgentPromptRequest,
): Promise<AgentPromptQueueEntry> {
  const normalizedConversationId = input.conversationId.trim();
  if (!normalizedConversationId) {
    throw new SchemaValidationError("agentPrompt.conversationId is required.");
  }

  const replayKey = input.replayKey.trim();
  if (!replayKey) {
    throw new SchemaValidationError("agentPrompt.replayKey is required.");
  }

  const summary = input.summary.trim();
  if (!summary) {
    throw new SchemaValidationError("agentPrompt.summary is required.");
  }

  const normalizedRequest = normalizePromptRequest(input.request, "agentPrompt.request");

  const queue = await readQueueDocument();
  const existing = queue.entries.find((entry) => entry.replayKey === replayKey);
  if (existing) {
    return existing;
  }

  const nowUtc = new Date().toISOString();
  const entry: AgentPromptQueueEntry = {
    id: createQueueEntryId(),
    schemaVersion: AGENT_PROMPT_QUEUE_SCHEMA_VERSION,
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

export async function removeAgentPrompt(entryId: string): Promise<void> {
  const queue = await readQueueDocument();
  const nextEntries = queue.entries.filter((entry) => entry.id !== entryId);

  if (nextEntries.length === queue.entries.length) {
    return;
  }

  await writeQueueDocument({
    version: AGENT_PROMPT_QUEUE_SCHEMA_VERSION,
    entries: nextEntries,
  });
}

export async function markAgentPromptReplayFailure(
  entryId: string,
  errorCode?: string,
): Promise<AgentPromptQueueEntry | null> {
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

  const updated: AgentPromptQueueEntry = {
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
    version: AGENT_PROMPT_QUEUE_SCHEMA_VERSION,
    entries: nextEntries,
  });

  return updated;
}

export async function clearAgentPromptBackoff(entryId: string): Promise<AgentPromptQueueEntry | null> {
  const queue = await readQueueDocument();
  const targetIndex = queue.entries.findIndex((entry) => entry.id === entryId);
  if (targetIndex < 0) {
    return null;
  }

  const existing = queue.entries[targetIndex];
  const updated: AgentPromptQueueEntry = {
    ...existing,
    updatedAtUtc: new Date().toISOString(),
    nextAttemptAtUtc: undefined,
    lastErrorCode: undefined,
  };

  const nextEntries = [...queue.entries];
  nextEntries[targetIndex] = updated;

  await writeQueueDocument({
    version: AGENT_PROMPT_QUEUE_SCHEMA_VERSION,
    entries: nextEntries,
  });

  return updated;
}

export function getAgentQueuedPromptReplayRequest(
  entry: AgentPromptQueueEntry,
): AgentConversationMessageRequest {
  return {
    message: entry.request.message,
    clientMessageId: entry.request.clientMessageId ?? null,
    userNote: entry.request.userNote ?? null,
  };
}
