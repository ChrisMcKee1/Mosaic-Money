export type AssistantApprovalDecision = "Approve" | "Reject";

export interface AssistantConversationMessageRequest {
  message: string;
  clientMessageId?: string | null;
  userNote?: string | null;
}

export interface AssistantApprovalRequest {
  decision: AssistantApprovalDecision;
  clientApprovalId?: string | null;
  rationale?: string | null;
}

export interface AssistantCommandAcceptedDto {
  commandId: string;
  correlationId: string;
  conversationId: string;
  commandType: string;
  queue: string;
  policyDisposition: string;
  queuedAtUtc: string;
  status: string;
}

export interface AssistantConversationRunStatusDto {
  runId: string;
  correlationId: string;
  status: string;
  triggerSource: string;
  failureCode?: string | null;
  failureRationale?: string | null;
  createdAtUtc: string;
  lastModifiedAtUtc: string;
  completedAtUtc?: string | null;
  agentName?: string | null;
  agentSource?: string | null;
  latestStageOutcomeSummary?: string | null;
  assignmentHint?: string | null;
}

export interface AssistantConversationStreamDto {
  conversationId: string;
  runs: AssistantConversationRunStatusDto[];
}
