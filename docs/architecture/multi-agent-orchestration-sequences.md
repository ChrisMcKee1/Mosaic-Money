# Runtime Agent Orchestration Sequences

## Purpose
Capture the primary runtime trigger flows for event-driven and conversational agent orchestration as currently implemented.

## Sequence A: Ingestion Completion Command Handling
```mermaid
sequenceDiagram
    participant Ingestion as Ingestion Pipeline
    participant SB as Azure Service Bus
    participant Worker as MosaicMoney.Worker
    participant DB as PostgreSQL
    participant EH as Azure Event Hubs

    Ingestion->>SB: Publish ingestion_completed command envelope
    SB->>Worker: Deliver command
    Worker->>DB: Create AgentRun (runId, correlationId)
    Worker->>DB: Persist AgentRunStage outcome
    Worker->>DB: Finalize idempotency key
    Worker->>EH: Emit runtime telemetry event
```

## Sequence B: Conversational Agent with Approval Card
```mermaid
sequenceDiagram
    participant User as Web/Mobile User
    participant API as MosaicMoney.Api Agent Endpoint
    participant SB as Azure Service Bus
    participant Worker as MosaicMoney.Worker
    participant Foundry as FoundryAgentRuntimeService
    participant DB as PostgreSQL

    User->>API: POST /api/v1/agent/conversations/{id}/messages
    API->>SB: Publish runtime-agent-message-posted command envelope
    SB->>Worker: Deliver command
    Worker->>Foundry: Invoke Foundry agent with policy disposition
    Foundry-->>Worker: Response summary + outcome code
    Worker->>DB: Persist AgentRun/AgentRunStages + fail-closed status
    User->>API: GET /api/v1/agent/conversations/{id}/stream
    API->>DB: Read run-state stream records
    API-->>User: Render agent response + approval card
    User->>API: POST approve/reject action
    API->>SB: Publish agent_approval_submitted command envelope
    SB->>Worker: Deliver approval command
    Worker->>Foundry: Invoke Foundry agent for approval disposition
    Worker->>DB: Apply approved transition with audit trail
```

## Trigger Matrix
| Trigger | Transport | Owner | Fail-Closed Behavior |
|---|---|---|---|
| Ingestion completed | Service Bus command | Worker | Route uncertain classification to `NeedsReview` |
| Agent message posted | Service Bus command | Worker | Return advisory-only response when policy context is incomplete |
| Decision state changed | Event Grid event | Worker | Do not emit completion event if audit persistence fails |
| Trace and scoring events | Event Hubs stream | API/Worker | Do not block business command completion on telemetry stream backpressure |
| Nightly anomaly sweep | Scheduler -> Service Bus | Worker | Queue retry with dead-letter escalation when command exceeds retry policy |
