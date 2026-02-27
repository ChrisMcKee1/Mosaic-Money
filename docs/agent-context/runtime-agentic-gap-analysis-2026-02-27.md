# Runtime Agentic Gap Analysis (2026-02-27)

## Purpose
This audit evaluates Mosaic Money runtime AI capabilities in product architecture terms (API, worker, database, workflow orchestration, and user-facing assistant), not coding-assistant mode definitions.

The analysis is based on:
- PRD and architecture docs.
- Current runtime code in API, Worker, Web, and Mobile.
- Specialist audits from `mosaic-money-backend`, `mosaic-money-ai`, `mosaic-money-devops`, `mosaic-money-frontend`, `mosaic-money-mobile`, and `Microsoft Agent Framework .NET`.
- Microsoft documentation research for Agent Framework workflow patterns and Azure eventing choices.

## Current Runtime Capability Snapshot

### Implemented now
- Deterministic -> semantic -> MAF escalation policy exists with fail-closed routing to `NeedsReview`.
- Semantic retrieval is implemented with PostgreSQL `pgvector` and provenance payloads.
- Dual-track notes are preserved (`UserNote` and `AgentNote`) with summary sanitation policy.
- Human review actions (approve/reject/reclassify) exist and are policy-gated.
- API-hosted background services process Plaid sync and embedding queue work.
- Basic semantic search and AI note rendering exist in web/mobile transaction and review surfaces.

### Missing or partial
- MAF runtime remains effectively disabled by default (`NoOp` fallback executor path).
- No first-class multi-agent runtime catalog for finance specialties (income, transfer, debt quality, investment classification, anomaly).
- `MosaicMoney.Worker` is not yet the orchestration backbone; most async orchestration currently runs in API process.
- No user conversational assistant endpoint/surface that orchestrates backend sub-agents.
- No durable run/state model for agent workflow lifecycle (run IDs, stage audit, signals, checkpoint linkage).
- Event-driven orchestration is incomplete; classification is largely endpoint-invoked instead of event-triggered from ingestion completion.

## Gap Matrix

| Capability | Current state | Gap impact |
|---|---|---|
| Multi-agent specialization | Single staged classifier + optional fallback proposal path | Limits financial intelligence depth and explainability by domain |
| Orchestration runtime | API hosted services + polling queues | Weak scale boundary and ownership between API and worker |
| Conversational assistant | No product chat orchestrator endpoint/UI | PRD journey "ask Copilot" is not runtime-complete |
| Agent run provenance model | Stage outputs exist but no durable run graph model | Hard to audit, replay, or compare specialist performance |
| Event-driven trigger map | Webhooks and pollers exist; no full event topology | Slow evolution to reliable autonomous review support |
| Role-level evaluations | Global gate exists; specialist-role gates do not | Hard to improve specific agent quality safely |

## Target Runtime Agent Catalog

### Orchestrator
- `ConversationOrchestratorAgent`
- Role: user-facing assistant entrypoint, context assembly, backend specialist dispatch, response composition.

### Specialist agents
- `CategorizationSpecialistAgent`
- `TransferDetectionSpecialistAgent`
- `IncomeNormalizationSpecialistAgent`
- `DebtQualitySpecialistAgent`
- `InvestmentClassificationSpecialistAgent`
- `AnomalyDetectionSpecialistAgent`

### Guardrail agent/service
- `PolicyAndExplainabilityGuard`
- Role: enforce `NeedsReview`, external messaging deny, summary sanitation, and provenance completeness.

## Required Database and Contract Additions

### New persistence models
- `AgentRuns`
- `AgentRunStages`
- `AgentSignals`
- `AgentDecisionAudit`
- `IdempotencyKeys` (for event/workflow replay safety)

### Contract additions
- Add `runId`, `correlationId`, `policyVersion`, and `stage provenance` fields to classification/query responses.
- Add assistant orchestration API contracts for conversation invoke, streaming updates, and approval responses.
- Add explicit specialist signal fields for transfer and income confidence where missing.

## Event-Driven Architecture Decision

### Recommended split
- Use **Azure Service Bus** for durable business commands and workflow steps.
- Use **Azure Event Grid** for reactive event fan-out and integration notifications.
- Use **Azure Event Hubs** for high-throughput telemetry, trace/event replay, and analytics streams.

### Why
- Service Bus best fits high-value, retryable, dead-lettered financial command processing.
- Event Grid best fits lightweight publish/subscribe notifications and broad fan-out.
- Event Hubs best fits ordered partitioned event streams at scale.

### Suggested trigger map
- Ingestion completion -> Service Bus command: classify/enrich transaction.
- Classification result changes -> Event Grid notification to subscribers.
- Runtime traces/scoring/tool events -> Event Hubs stream for observability/eval replay.
- Nightly/periodic sweeps (anomaly/debt quality/quality checks) -> scheduler-driven Service Bus dispatch.

## Web and Mobile Assistant Surface Gaps

### Web
- Missing global assistant panel and conversational thread UI.
- Missing approval-card UX for agent-proposed actions.
- Missing semantic explainability UI (score/provenance details).

### Mobile
- Missing assistant chat screen and offline queue for assistant messages.
- Missing push/background refresh path for asynchronous agent updates.
- Missing parity explainability views for semantic/agent decisions.

## Phased Delivery Recommendation

1. Activate real MAF executor path behind feature flag, keep fail-closed fallback.
2. Introduce durable run/stage/signal/audit schema and API contract surface.
3. Move async orchestration responsibilities into Worker and adopt Service Bus command lanes.
4. Add orchestrator assistant endpoint and web/mobile assistant UI shells.
5. Roll out specialist agents incrementally with role-level eval packs.
6. Complete MM-AI-12 replay artifact + cloud evaluator evidence and extend to specialist metrics.

## Reference Research
- Microsoft Agent Framework workflows and agents-in-workflows patterns:
  - https://learn.microsoft.com/agent-framework/workflows/agents-in-workflows
  - https://learn.microsoft.com/agent-framework/workflows/as-agents
  - https://learn.microsoft.com/agent-framework/workflows/declarative
  - https://learn.microsoft.com/agent-framework/workflows/human-in-the-loop
- Azure messaging technology choices and comparison:
  - https://learn.microsoft.com/azure/service-bus-messaging/compare-messaging-services
  - https://learn.microsoft.com/azure/architecture/guide/technology-choices/messaging
  - https://learn.microsoft.com/azure/event-grid/delivery-and-retry
  - https://learn.microsoft.com/azure/event-hubs/event-hubs-about
