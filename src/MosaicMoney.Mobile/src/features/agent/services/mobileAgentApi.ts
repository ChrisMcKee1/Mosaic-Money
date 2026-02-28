import {
  MobileApiError,
  requestJson,
  toReadableError as toReadableErrorBase,
} from "../../../shared/services/mobileApiClient";
import type {
  AgentApprovalRequest,
  AgentCommandAcceptedDto,
  AgentConversationMessageRequest,
  AgentConversationStreamDto,
} from "../contracts";

export function toReadableError(error: unknown): string {
  return toReadableErrorBase(error, "Agent request failed.");
}

export function isRetriableAgentError(error: unknown): boolean {
  if (!(error instanceof MobileApiError)) {
    return true;
  }

  return error.status >= 500 || error.status === 408 || error.status === 429;
}

export function getAgentErrorCode(error: unknown): string | undefined {
  if (!(error instanceof MobileApiError)) {
    return undefined;
  }

  return error.code ?? `HTTP_${error.status}`;
}

export async function postAgentMessage(
  conversationId: string,
  request: AgentConversationMessageRequest,
  signal?: AbortSignal,
): Promise<AgentCommandAcceptedDto> {
  return requestJson<AgentCommandAcceptedDto, AgentConversationMessageRequest>(
    `/api/v1/assistant/conversations/${encodeURIComponent(conversationId)}/messages`,
    {
      method: "POST",
      body: request,
      signal,
    },
  );
}

export async function submitAgentApproval(
  conversationId: string,
  approvalId: string,
  request: AgentApprovalRequest,
  signal?: AbortSignal,
): Promise<AgentCommandAcceptedDto> {
  return requestJson<AgentCommandAcceptedDto, AgentApprovalRequest>(
    `/api/v1/assistant/conversations/${encodeURIComponent(conversationId)}/approvals/${encodeURIComponent(approvalId)}`,
    {
      method: "POST",
      body: request,
      signal,
    },
  );
}

export async function getAgentConversationStream(
  conversationId: string,
  options?: {
    sinceUtc?: string;
    signal?: AbortSignal;
  },
): Promise<AgentConversationStreamDto> {
  const query = new URLSearchParams();
  if (options?.sinceUtc) {
    query.set("sinceUtc", options.sinceUtc);
  }

  const suffix = query.size > 0 ? `?${query.toString()}` : "";

  return requestJson<AgentConversationStreamDto>(
    `/api/v1/assistant/conversations/${encodeURIComponent(conversationId)}/stream${suffix}`,
    {
      signal: options?.signal,
    },
  );
}
