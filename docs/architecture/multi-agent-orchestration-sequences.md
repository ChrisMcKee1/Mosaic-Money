# Multi-Agent Orchestration Sequences

## Purpose
Capture the primary runtime trigger flows for event-driven and conversational multi-agent orchestration.

## Sequence A: Ingestion Completion to Classification Decision
```mermaid
sequenceDiagram
    participant Ingestion as Ingestion Pipeline
    participant SB as Azure Service Bus
    participant Worker as MosaicMoney.Worker
    participant Orch as ConversationOrchestratorAgent
    participant Spec as Specialist Agents
    participant Guard as PolicyAndExplainabilityGuard
    participant DB as PostgreSQL
    participant EG as Azure Event Grid

    Ingestion->>SB: Publish ClassifyTransactionCommand
    SB->>Worker: Deliver command
    Worker->>DB: Create AgentRun (runId, correlationId)
    Worker->>Orch: Start specialist dispatch
    Orch->>Spec: Invoke relevant specialist(s)
    Spec-->>Orch: Return proposal + confidence + rationale
    Orch->>Guard: Enforce ambiguity and external-action policy
    Guard->>DB: Persist AgentRunStages + AgentDecisionAudit
    Guard-->>Worker: Decision (Categorized or NeedsReview)
    Worker->>DB: Update transaction review state
    Worker->>EG: Emit TransactionDecisionChanged event
```

## Sequence B: Conversational Assistant with Approval Card
```mermaid
sequenceDiagram
    participant User as Web/Mobile User
    participant API as MosaicMoney.Api Assistant Endpoint
    participant SB as Azure Service Bus
    participant Worker as MosaicMoney.Worker
    participant Orch as ConversationOrchestratorAgent
    participant Guard as PolicyAndExplainabilityGuard
    participant DB as PostgreSQL

    User->>API: POST /api/v1/assistant/conversations/{id}/messages
    API->>DB: Persist conversation message + context
    API->>SB: Publish AssistantOrchestrationCommand
    SB->>Worker: Deliver command
    Worker->>Orch: Build plan and dispatch specialists
    Orch->>Guard: Validate policy and approval requirements
    Guard->>DB: Persist AgentRun + approval-required signal
    Worker-->>API: Stream assistant update payload
    API-->>User: Render assistant response + approval card
    User->>API: POST approve/reject action
    API->>SB: Publish AssistantApprovalCommand
    SB->>Worker: Deliver approval command
    Worker->>DB: Apply approved transition with audit trail
```

## Trigger Matrix
| Trigger | Transport | Owner | Fail-Closed Behavior |
|---|---|---|---|
| Ingestion completed | Service Bus command | Worker | Route uncertain classification to `NeedsReview` |
| Assistant message posted | Service Bus command | Worker | Return advisory-only response when policy context is incomplete |
| Decision state changed | Event Grid event | Worker | Do not emit completion event if audit persistence fails |
| Trace and scoring events | Event Hubs stream | API/Worker | Do not block business command completion on telemetry stream backpressure |
| Nightly anomaly sweep | Scheduler -> Service Bus | Worker | Queue retry with dead-letter escalation when command exceeds retry policy |
