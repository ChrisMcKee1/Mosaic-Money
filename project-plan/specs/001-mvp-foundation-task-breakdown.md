# Spec 001: MVP Foundation Task Breakdown

## Status
- Drafted: 2026-02-20
- Scope: End-to-end MVP task decomposition across DevOps, Backend, AI, Web, and Mobile

Source inputs reviewed:
- `project-plan/PRD.md`
- `project-plan/architecture.md`
- `docs/agent-context/prd-agentic-context.md`
- `docs/agent-context/architecture-agentic-context.md`
- `docs/agent-context/aspire-dotnet-integration-policy.md`
- `docs/agent-context/aspire-javascript-frontend-policy.md`

## Why This Is The First Spec
This spec creates the implementation sequence and dependency graph for the first delivery wave. It is designed to minimize rework by building platform and data invariants first, then layering ingestion, AI routing, and UX.

## Non-Negotiable Guardrails
- Single-entry ledger only. No double-entry debit/credit model.
- Preserve dual-track notes as separate fields: `UserNote` and `AgentNote`.
- Amortization is projection-only. Never mutate raw ledger dates/amounts for projection views.
- Ambiguous classification and high-impact money actions route to `NeedsReview` and require human approval.
- AI cannot autonomously execute external messaging. Draft-only behavior is allowed.
- Aspire-native orchestration and package patterns are required (`Aspire.Hosting.*`, `Aspire.*`, `WithReference(...)`, service discovery).

## Task Status Definitions
All task tables use a `Status` column with the following values:

| Status | Meaning |
|---|---|
| `Not Started` | Work has not begun. |
| `In Progress` | Actively being implemented by a subagent. |
| `Blocked` | Cannot proceed due to a dependency, issue, or external factor. Add a note in the spec or a linked comment explaining the blocker. |
| `Parked` | Deliberately deferred to prioritize other work. Not blocked — just deprioritized. |
| `In Review` | Implementation is complete; awaiting planner verification and approval. |
| `Done` | Planner has verified the work meets the Done Criteria and accepted it. |
| `Cut` | Removed from scope for this milestone. Reason should be documented. |

**Only the `mosaic-money-planner` agent may set a task to `Done` or `Cut`.** Subagents may move tasks to `In Progress`, `Blocked`, `Parked`, or `In Review`, but final acceptance is the planner's responsibility.

## Architecture Signals Used For Ordering
- Polyglot topology requires AppHost wiring before service feature work.
- ERD requires ledger entities and relationship constraints before ingestion and matching.
- Ingestion sequence requires idempotent raw-to-enriched pipeline before recurring and reimbursement logic.
- `NeedsReview` state machine must exist before higher-automation categorization.
- AI escalation order is strict: deterministic rules, then PostgreSQL semantic operators, then MAF fallback.

## Milestone Execution Order
1. M1 Platform and Contract Foundation
2. M2 Ledger Truth and Review Workflow Core
3. M3 Ingestion, Recurring, Reimbursements, and Projection Metadata
4. M4 AI Escalation Pipeline (Deterministic -> Semantic -> MAF)
5. M5 UX Completion (Web + Mobile) and Release Gates

## Cross-Domain Dependency DAG (Small Tasks)

### M1 Platform and Contract Foundation (implement first)
| ID | Domain | Task | Dependencies | Done Criteria | Status |
|---|---|---|---|---|---|
| MM-ASP-01 | DevOps | Bootstrap Aspire topology skeleton | None | AppHost, ServiceDefaults, API, Worker, Web resources exist and build under .NET 10 + JS app stack. | Done |
| MM-ASP-02 | DevOps | Configure JS hosting API in AppHost | MM-ASP-01 | AppHost uses `AddJavaScriptApp` or `AddViteApp` or `AddNodeApp`; no `AddNpmApp`; external endpoint configured for browser app. | Done |
| MM-ASP-03 | DevOps | Add explicit `WithReference(...)` graph | MM-ASP-01, MM-ASP-02 | API/Worker/Web dependencies are wired via references and service discovery; no hardcoded inter-service URLs. | Done |
| MM-ASP-04 | DevOps | Enforce service defaults and health endpoints | MM-ASP-03 | `.NET` services call `AddServiceDefaults()` and API maps default health endpoints. | Done |
| MM-BE-01 | Backend | Backend skeleton and Aspire DB wiring | MM-ASP-03, MM-ASP-04 | API uses `AddNpgsqlDbContext`; Worker uses `AddNpgsqlDataSource`; connection names are reference-driven. | Done |
| MM-BE-02 | Backend | Ledger domain model baseline | MM-BE-01 | Core entities created with single-entry semantics and separate `UserNote`/`AgentNote`. | Done |
| MM-BE-03 | Backend | PostgreSQL schema + extension migration | MM-BE-02 | Migration enables `pgvector` and `azure_ai`; indexes for idempotency, review, recurring, and vector lookup. | Done |
| MM-BE-04 | Backend | Minimal API contract v1 | MM-BE-02 | DTOs/endpoints defined for transactions, recurring, review actions, reimbursements; explicit validation/error contract. | Done |
| MM-AI-01 | AI | Classification outcome contract | MM-BE-04 | Stage outputs, confidence, and rationale structure persisted without raw transcript fields. | Done |
| MM-AI-02 | AI | AI workflow Aspire/DB integration checks | MM-BE-01, MM-BE-03 | AI paths use same reference-driven DB connectivity and do not bypass orchestration patterns. | Done |
| MM-FE-01 | Web | Next.js App Router foundation | MM-ASP-02 | Next.js 16 + React 19 + Tailwind foundation with accessible shell primitives. | Done |
| MM-FE-02 | Web | Server-side API fetch layer | MM-FE-01, MM-ASP-03 | Server-boundary fetch utility uses injected service URLs; no hardcoded localhost paths. | Done |
| MM-FE-03 | Web | Responsive app shell and navigation | MM-FE-01 | Dashboard, Transactions, NeedsReview routes with desktop/mobile navigation behavior. | Done |
| MM-MOB-01 | Mobile | Shared domain contracts and API client | MM-BE-04 | Shared schemas align with backend payloads; mobile does not duplicate financial domain logic. | Done |

### M2 Ledger Truth and Review Workflow Core
| ID | Domain | Task | Dependencies | Done Criteria | Status |
|---|---|---|---|---|---|
| MM-BE-05 | Backend | NeedsReview state machine + transitions | MM-BE-04, MM-AI-01 | Explicit allowed transitions; ambiguous outcomes fail closed into `NeedsReview`. | Done |
| MM-BE-06 | Backend | Idempotent ingestion pipeline (raw -> enriched) | MM-BE-03, MM-BE-05 | Duplicate Plaid delta handling is safe; raw payload stored; enriched record upserted with note preservation. | Done |
| MM-BE-12 | Backend | Plaid Link token lifecycle endpoints | MM-BE-04, MM-ASP-03 | Backend issues OAuth-capable Link token configurations and captures Link session metadata for diagnostics. | Done |
| MM-BE-13 | Backend | Public token exchange + secure Item storage | MM-BE-12 | `public_token` is exchanged server-side and resulting `access_token` + `item_id` are persisted in secure backend storage. | Done |
| MM-BE-14 | Backend | Plaid webhook and Item recovery contract | MM-BE-13, MM-BE-05 | Item/webhook error states (including OAuth expiry/revocation) route to explicit relink/update-mode flows with human review boundaries. | Done |
| MM-BE-15 | Backend | Plaid product capability mapping research | MM-BE-12, MM-BE-13, MM-BE-14 | Cross-reference PRD scenarios to Plaid products/endpoints/webhooks and sandbox institution coverage, then publish an approved MVP product map, schema impact list, and implementation order before expanding beyond `transactions`. | Done |
| MM-FE-04 | Web | Read-only ledger transaction list | MM-FE-02, MM-FE-03, MM-BE-04 | Ledger truth rendered with distinct `UserNote` and `AgentNote`; no client mutation of source amounts/dates. | Done |
| MM-FE-05 | Web | NeedsReview queue and approval UI | MM-FE-04, MM-BE-05 | Approve/reject/reclassify actions call backend review endpoints with explicit user intent. | Done |
| MM-FE-09 | Web | Plaid Link onboarding flow | MM-FE-02, MM-BE-12, MM-BE-13 | Web launches Link with server-issued `link_token` and posts `public_token` + metadata for backend exchange. | Not Started |
| MM-MOB-02 | Mobile | Offline-safe state/caching foundation | MM-MOB-01 | Mobile handles offline read and queued sync states safely. | Done |
| MM-MOB-03 | Mobile | NeedsReview queue screen | MM-MOB-02, MM-BE-05 | Mobile queue lists pending review items with clear status and refresh behavior. | Done |
| MM-MOB-04 | Mobile | Transaction detail with dual notes | MM-MOB-01, MM-BE-04 | Distinct display for `UserNote` vs `AgentNote`; ledger values treated as read-only truth. | Done |
| MM-MOB-05 | Mobile | HITL approval actions | MM-MOB-03, MM-MOB-04, MM-BE-05 | Approve/reject actions route through backend and never bypass human approval requirements. | Done |
| MM-MOB-08 | Mobile | Plaid Link SDK onboarding flow | MM-MOB-01, MM-BE-12, MM-BE-13 | Mobile uses React Native Link SDK with backend-issued `link_token` and server-side token exchange. | Not Started |

Update note (2026-02-23): Mobile scaffold created at `src/MosaicMoney.Mobile` (Expo TypeScript). `MM-MOB-03` and `MM-MOB-04` are unblocked and now in `In Review` after delegated implementation and typecheck pass.

Update note (2026-02-23): `MM-BE-15` research artifact is published at `project-plan/specs/003a-mm-be-15-plaid-product-capability-matrix.md`; planner review is complete and the task is now `Done`. Non-`transactions` product-lane implementation remains gated on future spec promotion of each `Adopt Later` lane.

Update note (2026-02-23): First delegated `transactions` implementation slice is merged in backend scope (`PlaidItemSyncStates` durability + `POST /api/v1/plaid/webhooks/transactions` for `SYNC_UPDATES_AVAILABLE`). Full cursor-pull worker orchestration remains a follow-on slice.

Update note (2026-02-23): Local runtime alignment pass completed for Plaid onboarding infrastructure. AppHost now pins web redirect host port to `http://localhost:53832`, local PostgreSQL runs on `pgvector/pgvector:pg17`, API startup migrations apply successfully, and schema tables are present. Remaining gate to close M2 Plaid onboarding tasks: switch from deterministic token simulation to real Plaid Sandbox provider wiring and execute end-to-end sandbox transaction sync proving persisted rows in `PlaidItemCredentials`, `PlaidItemSyncStates`, `RawTransactionIngestionRecords`, and `EnrichedTransactions`. `MM-BE-12/13/14` moved to `In Progress` and `MM-MOB-08` moved to `Parked` pending backend readiness.

Update note (2026-02-23): API provider wiring now defaults to real Plaid environment endpoints for `/link/token/create` and `/item/public_token/exchange`, and public-token exchange now bootstraps `/transactions/sync` cursor state into `PlaidItemSyncStates`. Deterministic token simulation remains available only behind `Plaid:UseDeterministicProvider=true` for controlled local/test fallback.

Update note (2026-02-23): Delegated backend/devops execution checkpoint completed for M2 Plaid proof gate. Backend now includes a hosted Plaid sync processor/background service that pulls paged `/transactions/sync` deltas from stored Item credentials and routes data into existing ingestion + embedding pipelines. Runtime evidence captured non-empty persistence and API retrieval (`PlaidItemCredentials`, `PlaidItemSyncStates`, `RawTransactionIngestionRecords`, `EnrichedTransactions`, and `GET /api/v1/transactions`), and a follow-on fail-closed guard now prevents silent cursor advancement when account mapping is missing. `MM-BE-12/13/14` remain `In Progress` pending rerun of full sandbox proof with automatic webhook-to-ingestion flow as the primary path (no manual fallback).

Update note (2026-02-24): Planner reran sandbox happy-path validation end-to-end using real Plaid provider wiring and verified non-empty persistence through the primary pipeline (`PlaidItemCredentials`, `PlaidItemSyncStates`, `RawTransactionIngestionRecords`, `EnrichedTransactions`) plus API retrieval via `GET /api/v1/transactions`. With this runtime proof gate satisfied, `MM-BE-12`, `MM-BE-13`, and `MM-BE-14` are promoted to `Done`.

Update note (2026-02-24): Frontend execution is intentionally paused due frontend-agent model unavailability; `MM-FE-09` is moved to `Parked` until frontend capacity is restored.

Update note (2026-02-24): Planner backlog sweep unparked `MM-FE-09` and `MM-MOB-08` to `Not Started` now that Plaid backend dependencies (`MM-BE-12/13/14`) are done and local frontend/mobile validation commands are available for active execution.

Update note (2026-02-24): Planner review promoted `MM-AI-08` and `MM-AI-09` to `Done` after focused verification (`MafFallbackGraphServiceTests`, `AgentNoteSummaryPolicyTests`, and `AgenticEvalReleaseGateTests`). A follow-on backlog item (`MM-AI-12`) is added to integrate official `.NET` and Foundry evaluator stacks with a source-linked research replay pack for reproducible future reruns.

### M3 Ingestion, Recurring, Reimbursements, and Projection Metadata
| ID | Domain | Task | Dependencies | Done Criteria | Status |
|---|---|---|---|---|---|
| MM-BE-07 | Backend | Recurring matcher (variance + date drift) | MM-BE-06 | Matching supports configurable amount variance and due-window drift; uncertain matches route to `NeedsReview`. | Done |
| MM-BE-08 | Backend | Reimbursement proposal + approval linking | MM-BE-05, MM-BE-06 | 1:N proposal model with approval-only persistence; no autonomous resolution. | Done |
| MM-BE-09 | Backend | Projection-support read metadata | MM-BE-04, MM-BE-07 | API returns raw truth plus projection metadata (`AmortizationMonths`, flags, recurring status) without ledger mutation. | Done |
| MM-FE-06 | Web | Business vs household isolation visuals | MM-FE-02, MM-BE-09 | Dashboard separates household budget burn from total liquidity views using backend truth. | Done |
| MM-FE-07 | Web | Recurring bills and safe-to-spend projection UI | MM-FE-06, MM-BE-07, MM-BE-09 | Projection view reflects recurring expectations and amortization as visual-only calculations. | Done |
| MM-MOB-06 | Mobile | Read-only projection dashboard | MM-MOB-01, MM-BE-09 | Mobile displays backend projection data without client-side ledger math mutations. | Done |

### M4 AI Escalation Pipeline (Deterministic -> Semantic -> MAF)
| ID | Domain | Task | Dependencies | Done Criteria | Status |
|---|---|---|---|---|---|
| MM-AI-03 | AI | Deterministic classification rules engine | MM-AI-01, MM-AI-02, MM-BE-06 | Rules run first and emit confidence + rationale code. | Done |
| MM-AI-04 | AI | Ambiguity policy gate to NeedsReview | MM-AI-03, MM-BE-05 | Low-confidence/conflicting outcomes are routed to `NeedsReview` reliably. | Done |
| MM-BE-10 | Backend | Async embeddings queue pipeline | MM-BE-03, MM-BE-06 | Embeddings are generated asynchronously from saved content and never block write requests. | Done |
| MM-AI-05 | AI | PostgreSQL semantic retrieval layer | MM-BE-10, MM-AI-02 | In-database semantic retrieval returns candidate matches with scores/provenance. | Done |
| MM-AI-06 | AI | Confidence fusion policy | MM-AI-03, MM-AI-04, MM-AI-05 | Deterministic precedence is explicit; semantic fallback bounded by confidence thresholds. | Done |
| MM-AI-07 | AI | MAF fallback graph execution | MM-AI-06 | MAF invoked only after stage 1+2 insufficiency and returns structured proposals. | Done |
| MM-AI-08 | AI | External messaging hard-stop guardrail | MM-AI-07 | Draft-only messaging enforced; send actions denied and auditable. | Done |
| MM-AI-09 | AI | AgentNote summarization enforcement | MM-AI-01, MM-AI-07 | Concise `AgentNote` summaries persisted; raw transcript storage suppressed. | Done |
| MM-AI-10 | AI | End-to-end orchestration flow | MM-AI-04, MM-AI-06, MM-AI-07, MM-AI-08, MM-AI-09 | Workflow outputs final categorized or `NeedsReview` state with traceable rationale. | Done |

### M5 Verification, Release Gates, and Dashboard Data Wiring
| ID | Domain | Task | Dependencies | Done Criteria | Status |
|---|---|---|---|---|---|
| MM-ASP-05 | DevOps | Local run reliability hardening | MM-ASP-04 | Deterministic startup with dependency waits and documented recovery paths. | Done |
| MM-ASP-06 | DevOps | Dashboard + MCP diagnostics flow | MM-ASP-05, MM-AI-10 | Team can inspect health/logs/traces for API, Worker, Web, and AI workflow traces in one standard workflow. | Done |
| MM-ASP-07 | DevOps | Orchestration policy gate checks | MM-ASP-03, MM-ASP-04, MM-ASP-06 | Checks reject `AddNpmApp`, hardcoded endpoints, and missing service-defaults patterns. | Done |
| MM-BE-11 | Backend | Financial correctness/regression tests | MM-BE-01, MM-BE-02, MM-BE-03, MM-BE-04, MM-BE-05, MM-BE-06, MM-BE-07, MM-BE-08, MM-BE-09, MM-BE-10 | Money/date/matching/review/reimbursement edge-case tests pass. | Done |
| MM-AI-11 | AI | Agentic eval release gate | MM-AI-10 | Measured criteria enforced for routing correctness, ambiguity fail-closed behavior, external messaging hard-stop denial, and `AgentNote` explainability with a go/no-go artifact output. | Done |
| MM-AI-12 | AI | Official evaluator stack adoption + research replay pack | MM-AI-11 | Integrate `.NET` evaluator libraries and Foundry evaluator/graders with source-linked rerun instructions, dataset mappings, and CI evidence artifacts. | Not Started |
| MM-FE-08 | Web | Playwright regression pack | MM-FE-04, MM-FE-05, MM-FE-06, MM-FE-07 | Desktop/mobile paths, review actions, and projection rendering are validated. | Done |
| MM-MOB-07 | Mobile | Mobile integration and offline behavior tests | MM-MOB-02, MM-MOB-03, MM-MOB-04, MM-MOB-05, MM-MOB-06 | Offline queue, sync recovery, and review workflows are validated on mobile. | Done |
| MM-BE-16 | Backend | Plaid Investments Ingestion & API | MM-BE-15 | Schema, ingestion worker, and read-only API for `/investments/holdings/get`. | Done |
| MM-BE-17 | Backend | Plaid Recurring Transactions Ingestion & API | MM-BE-15 | Schema, ingestion worker, and read-only API for `/transactions/recurring/get`. | Done |
| MM-BE-18 | Backend | Net Worth History Aggregation API | MM-BE-15 | API endpoint to aggregate historical balances across all account types. | Done |
| MM-FE-17 | Web | Wire M5 Dashboard UI to Backend APIs | MM-BE-16, MM-BE-17, MM-BE-18 | `page.jsx` fetches real data for Net Worth, Asset Allocation, Recent Transactions, Recurring, and Debt. | Done |

### M6 UI Redesign and Theming
| ID | Domain | Task | Dependencies | Done Criteria | Status |
|---|---|---|---|---|---|
| MM-FE-10 | Web | Global Layout & Theming | MM-FE-01 | Dark/Light mode toggle, CSS variable color system, distinctive typography, and main application shell implemented. | In Review |
| MM-FE-11 | Web | Dashboard Overview Screen | MM-FE-10 | Monthly spending chart, Net worth chart, Transactions to review, Top categories, and Next two weeks widgets implemented. | In Review |
| MM-FE-12 | Web | Accounts Screen | MM-FE-10 | Assets/Debts summary chart, grouped account lists with sparklines, and right detail panel implemented. | In Review |
| MM-FE-13 | Web | Transactions Screen | MM-FE-10 | Grouped transaction list with category tags and amounts, and right detail panel implemented. | In Review |
| MM-FE-14 | Web | Categories & Budgeting Screen | MM-FE-10 | Total spent vs budget donut chart, detailed progress bars, and right detail panel implemented. | In Review |
| MM-FE-15 | Web | Investments Screen | MM-FE-10 | Live balance estimate chart, top movers widget, account list with 1W balance change, and right detail panel implemented. | In Review |
| MM-FE-16 | Web | Recurrings Screen | MM-FE-10 | Left to pay vs paid so far donut chart, list of recurring transactions with status, and right detail panel implemented. | In Review |
| MM-FE-18 | Web | Semantic search and reranked dropdowns | MM-AI-05, MM-BE-10, MM-FE-17 | Search inputs and typeahead dropdowns use semantic retrieval + reranking so related intents (for example `utilities` and `water`) resolve together. | Not Started |
| MM-MOB-09 | Mobile | Semantic search and reranked pickers | MM-AI-05, MM-BE-10, MM-MOB-07.3 | Mobile search and picker flows use semantic retrieval + reranking with parity to web behavior and confidence-safe fallbacks. | Not Started |

## Suggested First Implementation Slice (Start Here)
Implement in this exact order to unlock all other streams quickly:
1. `MM-ASP-01` -> `MM-ASP-04`
2. `MM-BE-01` -> `MM-BE-04`
3. `MM-AI-01`, `MM-AI-02`
4. `MM-BE-05`, `MM-BE-06`
5. `MM-BE-15` (Plaid product capability mapping research gate)
6. `MM-FE-01` -> `MM-FE-05`
7. `MM-MOB-01` -> `MM-MOB-05`

This creates a complete human-reviewed transaction loop before advanced forecasting and AI fallback work.

## Governance Snapshot
- Risk: High overall (financial classification and reimbursement correctness).
- Decision: Allow with strict stage gates and mandatory human review for ambiguous or high-impact outcomes.
- Escalation: Any attempt to auto-resolve reimbursements, bypass `NeedsReview`, or send external messages must be blocked and reviewed.

## Team Delegation Map
- Backend tasks: `mosaic-money-backend`
- Web tasks: `mosaic-money-frontend`
- Mobile tasks: `mosaic-money-mobile`
- AI tasks: `mosaic-money-ai`
- Orchestration tasks: `mosaic-money-devops`

## Branch and Commit Slice Proposal
- Branch: `feature/spec-001-mvp-foundation-breakdown`
- Commit 1: `docs(spec): add spec 001 mvp foundation dependency breakdown`
- Commit 2: `docs(spec): add first implementation slice and verification gates`
