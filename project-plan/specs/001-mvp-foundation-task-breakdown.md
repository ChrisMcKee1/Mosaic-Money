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
| MM-FE-04 | Web | Read-only ledger transaction list | MM-FE-02, MM-FE-03, MM-BE-04 | Ledger truth rendered with distinct `UserNote` and `AgentNote`; no client mutation of source amounts/dates. | Done |
| MM-FE-05 | Web | NeedsReview queue and approval UI | MM-FE-04, MM-BE-05 | Approve/reject/reclassify actions call backend review endpoints with explicit user intent. | Done |
| MM-FE-09 | Web | Plaid Link onboarding flow | MM-FE-02, MM-BE-12, MM-BE-13 | Web launches Link with server-issued `link_token` and posts `public_token` + metadata for backend exchange. | Done |
| MM-MOB-02 | Mobile | Offline-safe state/caching foundation | MM-MOB-01 | Mobile handles offline read and queued sync states safely. | Done |
| MM-MOB-03 | Mobile | NeedsReview queue screen | MM-MOB-02, MM-BE-05 | Mobile queue lists pending review items with clear status and refresh behavior. | Blocked |
| MM-MOB-04 | Mobile | Transaction detail with dual notes | MM-MOB-01, MM-BE-04 | Distinct display for `UserNote` vs `AgentNote`; ledger values treated as read-only truth. | Blocked |
| MM-MOB-05 | Mobile | HITL approval actions | MM-MOB-03, MM-MOB-04, MM-BE-05 | Approve/reject actions route through backend and never bypass human approval requirements. | Not Started |
| MM-MOB-08 | Mobile | Plaid Link SDK onboarding flow | MM-MOB-01, MM-BE-12, MM-BE-13 | Mobile uses React Native Link SDK with backend-issued `link_token` and server-side token exchange. | Not Started |

Blocker note (2026-02-23): `MM-MOB-03` and `MM-MOB-04` are blocked because no mobile app project scaffold exists in-repo yet (no `src/MosaicMoney.Mobile`/Expo workspace). Shared contracts are present under `packages/shared`, but there is no executable mobile surface to implement or validate against.

### M3 Ingestion, Recurring, Reimbursements, and Projection Metadata
| ID | Domain | Task | Dependencies | Done Criteria | Status |
|---|---|---|---|---|---|
| MM-BE-07 | Backend | Recurring matcher (variance + date drift) | MM-BE-06 | Matching supports configurable amount variance and due-window drift; uncertain matches route to `NeedsReview`. | Done |
| MM-BE-08 | Backend | Reimbursement proposal + approval linking | MM-BE-05, MM-BE-06 | 1:N proposal model with approval-only persistence; no autonomous resolution. | Done |
| MM-BE-09 | Backend | Projection-support read metadata | MM-BE-04, MM-BE-07 | API returns raw truth plus projection metadata (`AmortizationMonths`, flags, recurring status) without ledger mutation. | Done |
| MM-FE-06 | Web | Business vs household isolation visuals | MM-FE-02, MM-BE-09 | Dashboard separates household budget burn from total liquidity views using backend truth. | Done |
| MM-FE-07 | Web | Recurring bills and safe-to-spend projection UI | MM-FE-06, MM-BE-07, MM-BE-09 | Projection view reflects recurring expectations and amortization as visual-only calculations. | Done |
| MM-MOB-06 | Mobile | Read-only projection dashboard | MM-MOB-01, MM-BE-09 | Mobile displays backend projection data without client-side ledger math mutations. | Not Started |

### M4 AI Escalation Pipeline (Deterministic -> Semantic -> MAF)
| ID | Domain | Task | Dependencies | Done Criteria | Status |
|---|---|---|---|---|---|
| MM-AI-03 | AI | Deterministic classification rules engine | MM-AI-01, MM-AI-02, MM-BE-06 | Rules run first and emit confidence + rationale code. | Done |
| MM-AI-04 | AI | Ambiguity policy gate to NeedsReview | MM-AI-03, MM-BE-05 | Low-confidence/conflicting outcomes are routed to `NeedsReview` reliably. | Done |
| MM-BE-10 | Backend | Async embeddings queue pipeline | MM-BE-03, MM-BE-06 | Embeddings are generated asynchronously from saved content and never block write requests. | In Progress |
| MM-AI-05 | AI | PostgreSQL semantic retrieval layer | MM-BE-10, MM-AI-02 | In-database semantic retrieval returns candidate matches with scores/provenance. | In Progress |
| MM-AI-06 | AI | Confidence fusion policy | MM-AI-03, MM-AI-04, MM-AI-05 | Deterministic precedence is explicit; semantic fallback bounded by confidence thresholds. | Not Started |
| MM-AI-07 | AI | MAF fallback graph execution | MM-AI-06 | MAF invoked only after stage 1+2 insufficiency and returns structured proposals. | Not Started |
| MM-AI-08 | AI | External messaging hard-stop guardrail | MM-AI-07 | Draft-only messaging enforced; send actions denied and auditable. | Not Started |
| MM-AI-09 | AI | AgentNote summarization enforcement | MM-AI-01, MM-AI-07 | Concise `AgentNote` summaries persisted; raw transcript storage suppressed. | Not Started |
| MM-AI-10 | AI | End-to-end orchestration flow | MM-AI-04, MM-AI-06, MM-AI-07, MM-AI-08, MM-AI-09 | Workflow outputs final categorized or `NeedsReview` state with traceable rationale. | Not Started |

### M5 Verification and Release Gates
| ID | Domain | Task | Dependencies | Done Criteria | Status |
|---|---|---|---|---|---|
| MM-ASP-05 | DevOps | Local run reliability hardening | MM-ASP-04 | Deterministic startup with dependency waits and documented recovery paths. | Done |
| MM-ASP-06 | DevOps | Dashboard + MCP diagnostics flow | MM-ASP-05, MM-AI-10 | Team can inspect health/logs/traces for API, Worker, Web, and AI workflow traces in one standard workflow. | In Progress |
| MM-ASP-07 | DevOps | Orchestration policy gate checks | MM-ASP-03, MM-ASP-04, MM-ASP-06 | Checks reject `AddNpmApp`, hardcoded endpoints, and missing service-defaults patterns. | Not Started |
| MM-BE-11 | Backend | Financial correctness/regression tests | MM-BE-01, MM-BE-02, MM-BE-03, MM-BE-04, MM-BE-05, MM-BE-06, MM-BE-07, MM-BE-08, MM-BE-09, MM-BE-10 | Money/date/matching/review/reimbursement edge-case tests pass. | Not Started |
| MM-AI-11 | AI | Agentic eval release gate | MM-AI-10 | Measured criteria enforced for routing correctness, ambiguity handling, and explainability. | Not Started |
| MM-FE-08 | Web | Playwright regression pack | MM-FE-04, MM-FE-05, MM-FE-06, MM-FE-07 | Desktop/mobile paths, review actions, and projection rendering are validated. | Done |
| MM-MOB-07 | Mobile | Mobile integration and offline behavior tests | MM-MOB-02, MM-MOB-03, MM-MOB-04, MM-MOB-05, MM-MOB-06 | Offline queue, sync recovery, and review workflows are validated on mobile. | Not Started |

## Suggested First Implementation Slice (Start Here)
Implement in this exact order to unlock all other streams quickly:
1. `MM-ASP-01` -> `MM-ASP-04`
2. `MM-BE-01` -> `MM-BE-04`
3. `MM-AI-01`, `MM-AI-02`
4. `MM-BE-05`, `MM-BE-06`
5. `MM-FE-01` -> `MM-FE-05`
6. `MM-MOB-01` -> `MM-MOB-05`

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
