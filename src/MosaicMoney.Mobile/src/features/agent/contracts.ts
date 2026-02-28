export type AgentApprovalDecision = "Approve" | "Reject";

export interface AgentConversationMessageRequest {
  message: string;
  clientMessageId?: string | null;
  userNote?: string | null;
}

export interface AgentApprovalRequest {
  decision: AgentApprovalDecision;
  clientApprovalId?: string | null;
  rationale?: string | null;
}

export interface AgentCommandAcceptedDto {
  commandId: string;
  correlationId: string;
  conversationId: string;
  commandType: string;
  queue: string;
  policyDisposition: string;
  queuedAtUtc: string;
  status: string;
}

export interface AgentConversationRunStatusDto {
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

export interface AgentConversationStreamDto {
  conversationId: string;
  runs: AgentConversationRunStatusDto[];
}
