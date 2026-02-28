import {
  MobileApiError,
  requestJson,
  toReadableError as toReadableErrorBase,
} from "../../../shared/services/mobileApiClient";
import type {
  AssistantApprovalRequest,
  AssistantCommandAcceptedDto,
  AssistantConversationMessageRequest,
  AssistantConversationStreamDto,
} from "../contracts";

export function toReadableError(error: unknown): string {
  return toReadableErrorBase(error, "Assistant request failed.");
}

export function isRetriableAssistantError(error: unknown): boolean {
  if (!(error instanceof MobileApiError)) {
    return true;
  }

  return error.status >= 500 || error.status === 408 || error.status === 429;
}

export function getAssistantErrorCode(error: unknown): string | undefined {
  if (!(error instanceof MobileApiError)) {
    return undefined;
  }

  return error.code ?? `HTTP_${error.status}`;
}

export async function postAssistantMessage(
  conversationId: string,
  request: AssistantConversationMessageRequest,
  signal?: AbortSignal,
): Promise<AssistantCommandAcceptedDto> {
  return requestJson<AssistantCommandAcceptedDto, AssistantConversationMessageRequest>(
    `/api/v1/assistant/conversations/${encodeURIComponent(conversationId)}/messages`,
    {
      method: "POST",
      body: request,
      signal,
    },
  );
}

export async function submitAssistantApproval(
  conversationId: string,
  approvalId: string,
  request: AssistantApprovalRequest,
  signal?: AbortSignal,
): Promise<AssistantCommandAcceptedDto> {
  return requestJson<AssistantCommandAcceptedDto, AssistantApprovalRequest>(
    `/api/v1/assistant/conversations/${encodeURIComponent(conversationId)}/approvals/${encodeURIComponent(approvalId)}`,
    {
      method: "POST",
      body: request,
      signal,
    },
  );
}

export async function getAssistantConversationStream(
  conversationId: string,
  options?: {
    sinceUtc?: string;
    signal?: AbortSignal;
  },
): Promise<AssistantConversationStreamDto> {
  const query = new URLSearchParams();
  if (options?.sinceUtc) {
    query.set("sinceUtc", options.sinceUtc);
  }

  const suffix = query.size > 0 ? `?${query.toString()}` : "";

  return requestJson<AssistantConversationStreamDto>(
    `/api/v1/assistant/conversations/${encodeURIComponent(conversationId)}/stream${suffix}`,
    {
      signal: options?.signal,
    },
  );
}
