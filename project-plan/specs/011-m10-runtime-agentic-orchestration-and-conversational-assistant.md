# Spec 011: M10 Runtime Agentic Orchestration and Conversational Assistant

## Status
- Drafted: 2026-02-27
- Milestone: M10
- GitHub tracking epic: `#126`
- GitHub task issues: `#127`, `#128`, `#132`, `#134`, `#137`, `#138`, `#139`, `#140`, `#141`, `#142`, `#143`
- Depends on:
- `project-plan/specs/001-mvp-foundation-task-breakdown.md`
- `project-plan/specs/005-m4-ai-escalation-pipeline-deterministic-semantic-maf.md`
- `project-plan/specs/006-m5-ux-completion-and-release-gates.md`
- `project-plan/specs/008-m7-identity-household-access-and-account-ownership.md`
- `project-plan/specs/009-m8-authentication-and-authorization-clerk.md`
- `project-plan/specs/010-m9-cross-surface-charting-framework-migration.md`

## Objective
Close the runtime product-agent architecture gap by introducing a worker-owned, event-driven multi-agent system with a conversational assistant surface and durable workflow provenance.

## Scope
- Promote runtime orchestration from API-hosted background loops to worker-owned command lanes.
- Introduce durable run-state persistence for agent workflows.
- Add specialist finance-agent routing and a conversational orchestrator.
- Add web/mobile assistant surfaces with explicit human approval cards.
- Add release gates for specialist quality, policy compliance, and event replay safety.

## Guardrails
- Single-entry ledger semantics remain unchanged.
- `UserNote` and `AgentNote` remain separate and never collapsed.
- Ambiguous or high-impact outcomes must route to `NeedsReview`.
- No autonomous external messaging execution.
- Aspire service discovery and `WithReference(...)` remain mandatory for runtime wiring.

## Architecture Deliverables
- `docs/architecture/multi-agent-system-topology.md`
- `docs/architecture/multi-agent-orchestration-sequences.md`

## Task Breakdown
| ID | Domain | Task | Dependencies | Deliverable | Status |
|---|---|---|---|---|---|
| MM-ASP-12 | DevOps | Runtime messaging backbone in AppHost | MM-ASP-10, MM-ASP-11 | AppHost wiring for Service Bus command lanes, Event Grid fan-out, and Event Hubs telemetry streams with secret-safe configuration contracts. | Done |
| MM-ASP-13 | DevOps | Worker orchestration runbooks and diagnostics | MM-ASP-12, MM-ASP-06 | Operational runbook for queue retries, dead-letter recovery, replay, and trace correlation across API/Worker/assistant flows. | Done |
| MM-BE-27 | Backend | Agent workflow lifecycle schema | MM-BE-26 | Add `AgentRuns`, `AgentRunStages`, `AgentSignals`, `AgentDecisionAudit`, and replay-safe idempotency keys via EF migrations and persistence contracts. | Done |
| MM-BE-28 | Backend | Worker-owned orchestration command handlers | MM-BE-27, MM-ASP-12 | Move classification/enrichment workflow triggers into worker command handlers with idempotent retries and deterministic fail-closed behavior. | Done |
| MM-AI-13 | AI | Specialist agent registry and routing policy | MM-AI-10, MM-BE-27 | Configurable specialist map (categorization, transfer, income, debt quality, investment, anomaly) with deterministic precedence and escalation policy. | Done |
| MM-AI-14 | AI | Conversational orchestrator workflow contracts | MM-AI-13, MM-BE-28 | Assistant orchestration contracts for invoke/stream/approve/reject with run correlation and policy-aware response shaping. | Done |
| MM-AI-15 | AI | Specialist evaluator packs and replay artifacts | MM-AI-12, MM-AI-13, MM-AI-14 | Role-level evaluator datasets, pass/fail thresholds, and reproducible replay artifacts for each specialist lane. | Done |
| MM-FE-27 | Web | Assistant shell and approval card UX | MM-AI-14, MM-FE-22 | Global assistant panel with conversational thread, approval cards, and explicit high-impact action confirmations. | Done |
| MM-FE-28 | Web | Agent provenance and explainability timeline | MM-FE-27, MM-BE-27 | UI for run/stage provenance, confidence, and rationale summaries without exposing disallowed transcript/tool dumps. | Done |
| MM-MOB-16 | Mobile | Assistant parity with offline-safe queue | MM-AI-14, MM-MOB-14 | Mobile assistant screen with queued outbound prompts, async update handling, and parity approval interactions. | Done |
| MM-QA-06 | QA | Multi-agent runtime release gate | MM-ASP-13, MM-AI-15, MM-FE-28, MM-MOB-16 | Cross-surface gate validating routing correctness, policy denials, replay safety, and assistant UX acceptance. | Done |

## Acceptance Criteria
- Worker, not API request handlers, owns orchestration command execution for runtime agent pipelines.
- Agent run lifecycle is queryable and auditable end-to-end with run and stage correlation IDs.
- Conversational assistant can orchestrate specialists while preserving fail-closed review routing.
- High-impact actions remain approval-only and externally non-executing.
- Specialist evaluator packs produce reproducible replay artifacts suitable for release gating.

## Verification
- Backend: focused unit/integration tests for lifecycle schema, idempotency, and command retry semantics.
- AI: evaluator thresholds per specialist lane plus policy hard-stop checks.
- Web/Mobile: assistant and approval UX regressions plus explainability rendering checks.
- DevOps: queue/trace diagnostics and replay drill evidence.
- QA: end-to-end matrix artifact for the M10 release gate.

## Update Note (2026-02-27)
- Planner kickoff promoted `MM-ASP-12`, `MM-BE-27`, and `MM-AI-13` to `In Progress` for the first active M10 implementation wave.
- GitHub tracking: `#127`, `#132`, and `#139` moved to active execution under epic `#126`.
- MM-AI-13 implementation slice now includes a configurable specialist registry and fail-closed routing policy integrated into `DeterministicClassificationOrchestrator` for lane selection (`categorization`, `transfer`, `income`, `debt-quality`, `investment`, `anomaly`) while preserving deterministic precedence and existing production defaults via `EnableRoutingPolicy=false` default configuration.
- Planner verification moved `MM-ASP-12`, `MM-BE-27`, and `MM-AI-13` to `In Review` after integrated AppHost/API/Worker compile checks and focused test evidence: `AgentWorkflowLifecycleModelContractTests`, `ClassificationSpecialistRoutingPolicyTests`, `DeterministicClassificationOrchestratorTests`, and `RuntimeMessagingBackboneOptionsTests` (19 passed, 0 failed).

## Update Note (2026-02-27, Review Closeout)
- Planner promoted `MM-ASP-12`, `MM-BE-27`, and `MM-AI-13` to `Done` after final verification pass:
	- Aspire-native AppHost wiring validated for Service Bus command lanes and Event Hubs telemetry with explicit Event Grid config contract.
	- Lifecycle schema and specialist routing changes validated against focused tests (`21` passed, `0` failed).
	- Build validation completed for AppHost, API, and Worker.

## Update Note (2026-02-27, Runtime Worker + Assistant Slice)
- Planner moved `MM-ASP-13`, `MM-BE-28`, and `MM-AI-14` to `In Review` after implementation + focused validation:
	- `MosaicMoney.Worker` now owns runtime command queue processors for ingestion, assistant messages, and nightly sweep lanes with idempotency key reservation/finalization, fail-closed `NeedsReview` run state transitions, and structured failure signal writes.
	- API contract surface now includes assistant invoke/approval/stream endpoints under `/api/v1/agent/conversations/*`, publishing correlated command envelopes to `runtime-agent-message-posted` and exposing run-state streaming via lifecycle tables.
	- Added `docs/agent-context/runtime-agentic-worker-runbook.md` with retry/dead-letter/replay/trace procedures and release-gate evidence checklist.
	- Validation evidence: `dotnet build` for API + Worker succeeded; focused tests passed (`14` passed, `0` failed): `ApiAuthorizationTests`, `RuntimeMessagingBackboneOptionsTests`, `AgentWorkflowLifecycleModelContractTests`.

## Update Note (2026-02-27, Web Assistant UX Slice)
- Planner moved `MM-FE-27` and `MM-FE-28` to `In Review` after implementing global assistant UX and provenance timeline integration:
	- Added global assistant panel in app shell with conversation flow, explicit high-impact approval cards (approve/reject confirmation), and policy-aware UX messaging.
	- Added provenance timeline tab rendering run correlation/status/failure context from `/api/v1/agent/conversations/{conversationId}/stream` without exposing raw tool dumps.
	- Added server actions for assistant invoke/approval/stream fetches and wired the panel into shared app chrome while excluding sign-in/up routes.
	- Validation evidence: `npm --prefix src/MosaicMoney.Web run build` succeeded (non-blocking expected warning during static generation when API base URL is not configured outside Aspire runtime).

## Update Note (2026-02-27, Specialist Evaluator Pack Slice)
- Planner moved `MM-AI-15` to `In Review` after implementing specialist-lane evaluator pack artifacts and release-gate wiring.
	- Added deterministic specialist evaluator pack model + thresholds for all runtime lanes (`categorization`, `transfer`, `income`, `debt-quality`, `investment`, `anomaly`).
	- Extended release-gate artifact emission to generate `MM-AI-12` companion outputs (`criteria-dataset-mapping.json`, `replay-pack.md`) and `MM-AI-15` outputs (`latest.json`, `specialist-lane-datasets.json`, `replay-pack.md`).
	- Updated `.github/scripts/run-mm-ai-11-release-gate.ps1` to emit `MM-AI-15` artifacts and validate `AgenticEvalOfficialEvaluatorArtifactsTests` plus `AgenticEvalSpecialistEvaluatorArtifactsTests` in the same run.
	- Validation evidence: `pwsh .github/scripts/run-mm-ai-11-release-gate.ps1` passed; focused evaluator tests passed (`AgenticEvalSpecialistEvaluatorPackTests`, `AgenticEvalOfficialEvaluatorStackTests`, `AgenticEvalReleaseGateTests`).

## Update Note (2026-02-27, M10 In-Review Closeout)
- Planner promoted `MM-ASP-13`, `MM-BE-28`, `MM-AI-14`, `MM-AI-15`, `MM-FE-27`, and `MM-FE-28` to `Done` after full integrated validation:
	- Runtime health proof: API/Worker/Web plus runtime messaging/telemetry lanes reached `Running + Healthy` (`runtime-messaging`, `runtime-ingestion-completed`, `runtime-agent-message-posted`, `runtime-nightly-anomaly-sweep`, `runtime-telemetry`, `runtime-telemetry-stream`, `mosaic-money-runtime`).
	- Focused backend/AI tests passed (`22` passed, `0` failed): `AgentWorkflowLifecycleModelContractTests`, `RuntimeMessagingBackboneOptionsTests`, `ClassificationSpecialistRoutingPolicyTests`, `AgenticEvalReleaseGateTests`, `AgenticEvalSpecialistEvaluatorPackTests`, and `AgenticEvalSpecialistEvaluatorArtifactsTests`.
	- Web assistant/provenance validation passed via production build (`npm --prefix src/MosaicMoney.Web run build`).
	- Mobile auth deferral preserved per planner directive: `MM-MOB-13` remains `Blocked` and outside this closeout scope.

## Update Note (2026-02-28, MM-MOB-16 In-Review Promotion)
- Planner moved `MM-MOB-16` to `In Review` after implementing mobile assistant parity with offline-safe prompt queue and async provenance updates:
	- Added mobile assistant screen/route (`/assistant`) with conversation + provenance tabs, policy-aware approval cards, and explicit approve/reject interactions.
	- Added assistant mobile API contracts/services for invoke/approval/stream paths under `/api/v1/agent/conversations/*`.
	- Added AsyncStorage-backed outbound prompt queue with retry backoff + replay recovery hook, wired at app root for periodic/background replay.
	- Added focused queue/recovery tests for assistant offline behavior and replay semantics.
- Validation evidence: `npm --prefix src/MosaicMoney.Mobile run typecheck` succeeded; `npm --prefix src/MosaicMoney.Mobile run test:assistant-queue` passed (`7` tests, `0` failed).

## Update Note (2026-02-28, MM-QA-06 + M10 Final Gate Closeout)
- Planner promoted `MM-MOB-16` and `MM-QA-06` to `Done` after completing cross-surface runtime gate validation.
- Runtime readiness and orchestration evidence:
	- AppHost runtime recovered with local-container PostgreSQL fallback and healthy core services (`api`, `worker`, `web`) plus healthy runtime messaging/telemetry lanes (`runtime-*`) via `aspire resources --project src/apphost.cs`.
	- Assistant endpoint denial proof captured with unauthenticated requests returning `401` for both `GET /api/v1/agent/conversations/{id}/stream` and `POST /api/v1/agent/conversations/{id}/messages`.
- Backend/AI validation evidence:
	- `dotnet test src/MosaicMoney.Api.Tests/MosaicMoney.Api.Tests.csproj --filter "FullyQualifiedName~AgentWorkflowLifecycleModelContractTests|FullyQualifiedName~RuntimeMessagingBackboneOptionsTests|FullyQualifiedName~ClassificationSpecialistRoutingPolicyTests"` passed (`11` tests).
	- `dotnet test src/MosaicMoney.Api.Tests/MosaicMoney.Api.Tests.csproj --filter "FullyQualifiedName~ApiAuthorizationTests"` passed (`8` tests).
	- `pwsh .github/scripts/run-mm-ai-11-release-gate.ps1` passed and regenerated `MM-AI-11`, `MM-AI-12`, and `MM-AI-15` release-gate artifacts.
	- `pwsh .github/scripts/test-orchestration-policy-gates.ps1` passed after removing hardcoded localhost defaults from web triage scripts.
- UX acceptance evidence:
	- Web assistant shell/provenance surface remains healthy via `npm --prefix src/MosaicMoney.Web run build` (successful build; known non-blocking API base URL warning during static generation outside orchestrated runtime).
	- Mobile assistant parity + replay safety validated via `npm --prefix src/MosaicMoney.Mobile run typecheck` and `npm --prefix src/MosaicMoney.Mobile run test:assistant-queue` (`7` tests).
