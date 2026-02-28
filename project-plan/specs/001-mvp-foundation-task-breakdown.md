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
5. M5 Verification, Release Gates, and Dashboard Data Wiring
6. M6 UI Redesign and Theming
7. M7 Identity, Household Access Control, and Account Ownership
8. M8 Authentication and Authorization (Clerk)
9. M9 Cross-Surface Charting Framework Migration
10. M10 Runtime Agentic Orchestration and Conversational Assistant

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
| MM-FE-09 | Web | Plaid Link onboarding flow | MM-FE-02, MM-BE-12, MM-BE-13 | Web launches Link with server-issued `link_token` and posts `public_token` + metadata for backend exchange. | Done |
| MM-MOB-02 | Mobile | Offline-safe state/caching foundation | MM-MOB-01 | Mobile handles offline read and queued sync states safely. | Done |
| MM-MOB-03 | Mobile | NeedsReview queue screen | MM-MOB-02, MM-BE-05 | Mobile queue lists pending review items with clear status and refresh behavior. | Done |
| MM-MOB-04 | Mobile | Transaction detail with dual notes | MM-MOB-01, MM-BE-04 | Distinct display for `UserNote` vs `AgentNote`; ledger values treated as read-only truth. | Done |
| MM-MOB-05 | Mobile | HITL approval actions | MM-MOB-03, MM-MOB-04, MM-BE-05 | Approve/reject actions route through backend and never bypass human approval requirements. | Done |
| MM-MOB-08 | Mobile | Plaid Link SDK onboarding flow | MM-MOB-01, MM-BE-12, MM-BE-13 | Mobile uses React Native Link SDK with backend-issued `link_token` and server-side token exchange. | Blocked |

Update note (2026-02-23): Mobile scaffold created at `src/MosaicMoney.Mobile` (Expo TypeScript). `MM-MOB-03` and `MM-MOB-04` are unblocked and now in `In Review` after delegated implementation and typecheck pass.

Update note (2026-02-23): `MM-BE-15` research artifact is published at `project-plan/specs/003a-mm-be-15-plaid-product-capability-matrix.md`; planner review is complete and the task is now `Done`. Non-`transactions` product-lane implementation remains gated on future spec promotion of each `Adopt Later` lane.

Update note (2026-02-23): First delegated `transactions` implementation slice is merged in backend scope (`PlaidItemSyncStates` durability + `POST /api/v1/plaid/webhooks/transactions` for `SYNC_UPDATES_AVAILABLE`). Full cursor-pull worker orchestration remains a follow-on slice.

Update note (2026-02-23): Local runtime alignment pass completed for Plaid onboarding infrastructure. AppHost now pins web redirect host port to `http://localhost:53832`, local PostgreSQL runs on `pgvector/pgvector:pg17`, API startup migrations apply successfully, and schema tables are present. Remaining gate to close M2 Plaid onboarding tasks: switch from deterministic token simulation to real Plaid Sandbox provider wiring and execute end-to-end sandbox transaction sync proving persisted rows in `PlaidItemCredentials`, `PlaidItemSyncStates`, `RawTransactionIngestionRecords`, and `EnrichedTransactions`. `MM-BE-12/13/14` moved to `In Progress` and `MM-MOB-08` moved to `Parked` pending backend readiness.

Update note (2026-02-23): API provider wiring now defaults to real Plaid environment endpoints for `/link/token/create` and `/item/public_token/exchange`, and public-token exchange now bootstraps `/transactions/sync` cursor state into `PlaidItemSyncStates`. Deterministic token simulation remains available only behind `Plaid:UseDeterministicProvider=true` for controlled local/test fallback.

Update note (2026-02-23): Delegated backend/devops execution checkpoint completed for M2 Plaid proof gate. Backend now includes a hosted Plaid sync processor/background service that pulls paged `/transactions/sync` deltas from stored Item credentials and routes data into existing ingestion + embedding pipelines. Runtime evidence captured non-empty persistence and API retrieval (`PlaidItemCredentials`, `PlaidItemSyncStates`, `RawTransactionIngestionRecords`, `EnrichedTransactions`, and `GET /api/v1/transactions`), and a follow-on fail-closed guard now prevents silent cursor advancement when account mapping is missing. `MM-BE-12/13/14` remain `In Progress` pending rerun of full sandbox proof with automatic webhook-to-ingestion flow as the primary path (no manual fallback).

Update note (2026-02-24): Planner reran sandbox happy-path validation end-to-end using real Plaid provider wiring and verified non-empty persistence through the primary pipeline (`PlaidItemCredentials`, `PlaidItemSyncStates`, `RawTransactionIngestionRecords`, `EnrichedTransactions`) plus API retrieval via `GET /api/v1/transactions`. With this runtime proof gate satisfied, `MM-BE-12`, `MM-BE-13`, and `MM-BE-14` are promoted to `Done`.

Update note (2026-02-24): Frontend execution is intentionally paused due frontend-agent model unavailability; `MM-FE-09` is moved to `Parked` until frontend capacity is restored.

Update note (2026-02-24): Planner backlog sweep unparked `MM-FE-09` and `MM-MOB-08` to `Not Started` now that Plaid backend dependencies (`MM-BE-12/13/14`) are done and local frontend/mobile validation commands are available for active execution.

Update note (2026-02-25): Delegation attempts for `MM-FE-09` and `MM-MOB-08` to specialist subagents are currently blocked by upstream provider-capacity errors; tasks remain blocked pending specialist availability or a manual planner fallback implementation.

Update note (2026-02-25): Planner resumed both onboarding tasks (`MM-FE-09`, `MM-MOB-08`) in an active parallel execution wave using frontend/mobile specialist delegation and manual fallback review paths where needed.

Update note (2026-02-25): Initial implementation and local validation completed for `MM-FE-09` and `MM-MOB-08` (web build + targeted onboarding Playwright spec; mobile typecheck). Both tasks are promoted to `In Review` pending final sandbox interaction acceptance.

Update note (2026-02-24): Planner review promoted `MM-AI-08` and `MM-AI-09` to `Done` after focused verification (`MafFallbackGraphServiceTests`, `AgentNoteSummaryPolicyTests`, and `AgenticEvalReleaseGateTests`). A follow-on backlog item (`MM-AI-12`) is added to integrate official `.NET` and Foundry evaluator stacks with a source-linked research replay pack for reproducible future reruns.

Update note (2026-02-25): `MM-AI-12` implementation now emits an additional official evaluator replay artifact (`artifacts/release-gates/mm-ai-12/latest.json`) from the existing release-gate script while preserving deterministic `MM-AI-11` release-blocking criteria. Task is promoted to `In Review` after focused offline validation; cloud evaluator evidence is still required before `Done`.

Update note (2026-02-25): `MM-BE-06` is reopened to implement a Plaid historical backfill intake enhancement that explicitly requests and ingests up to two years of available transactions (24 months), replacing implicit defaults and preserving idempotent cursor semantics.

Update note (2026-02-25): `MM-BE-06` implementation now wires explicit Plaid transaction history depth (`days_requested`) at both Link-token initialization and sync-bootstrap initialization paths with bounded configuration (`30..730`, default `730`) and focused unit-test coverage. Task promoted to `In Review` pending integrated AppHost runtime validation.

Update note (2026-02-25): New M7 identity/access milestone is added and documented in `project-plan/specs/008-m7-identity-household-access-and-account-ownership.md` to cover first-class app identity, household membership lifecycle, account-level ACL visibility (mine-only/spouse-only/joint/read-only), and migration edge cases. Tasks `MM-BE-19..24`, `MM-ASP-08..09`, `MM-FE-19..21`, and `MM-MOB-10..12` are now tracked as `Not Started` and synced to the GitHub Project board.

Update note (2026-02-25): Planner kickoff delegated `MM-FE-18` (web semantic search/typeahead) and `MM-MOB-09` (mobile semantic search/pickers). After focused build/typecheck/test validation, both tasks are promoted to `In Review` pending final acceptance criteria check.

Update note (2026-02-25): Planner promoted `MM-BE-19..24` to `Done` after focused M7 identity/ACL verification passed (`dotnet test ... --filter "IdentityMembershipModelContractTests|AccountAccessPolicyBackfillServiceTests|AccountAccessPolicyReviewQueueModelContractTests|AccountMemberAccessModelContractTests|TransactionProjectionMetadataQueryServiceTests"`: 15 passed, 0 failed).

Update note (2026-02-25): Planner promoted `MM-MOB-10` to `Done` after verifying mobile member lifecycle and invite flows across settings screens with clean validation (`npm run typecheck` in `src/MosaicMoney.Mobile`).

Update note (2026-02-25): Planner promoted `MM-FE-20` and `MM-FE-21` to `In Review` after adding confirmation UX for web visibility changes and validating with a clean `npm run build` in `src/MosaicMoney.Web`.

Update note (2026-02-26): Planner promoted `MM-MOB-11` and `MM-MOB-12` to `In Review` after implementing household account-sharing API endpoints and replacing mobile deterministic ACL policy mocks with API-backed account access summaries plus sharing preset updates. Validation evidence: targeted household endpoint tests, API build, and mobile typecheck.

Update note (2026-02-26): `MM-QA-02` and `MM-QA-03` are now `Done` after web dependency remediation and gate reruns. Security/config/dependency evidence is captured in `artifacts/release-gates/mm-qa-02/latest.md` (including `next` upgrade to `16.1.6` and clean audits), and release rehearsal/rollback evidence is captured in `artifacts/release-gates/mm-qa-03/latest.md` with healthy API/worker/web restart verification.

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
| MM-AI-12 | AI | Official evaluator stack adoption + research replay pack | MM-AI-11 | Integrate `.NET` evaluator libraries and Foundry evaluator/graders with source-linked rerun instructions, dataset mappings, and CI evidence artifacts. | Done |
| MM-FE-08 | Web | Playwright regression pack | MM-FE-04, MM-FE-05, MM-FE-06, MM-FE-07 | Desktop/mobile paths, review actions, and projection rendering are validated. | Done |
| MM-MOB-07 | Mobile | Mobile integration and offline behavior tests | MM-MOB-02, MM-MOB-03, MM-MOB-04, MM-MOB-05, MM-MOB-06 | Offline queue, sync recovery, and review workflows are validated on mobile. | Done |
| MM-BE-16 | Backend | Plaid Investments Ingestion & API | MM-BE-15 | Schema, ingestion worker, and read-only API for `/investments/holdings/get`. | Done |
| MM-BE-17 | Backend | Plaid Recurring Transactions Ingestion & API | MM-BE-15 | Schema, ingestion worker, and read-only API for `/transactions/recurring/get`. | Done |
| MM-BE-18 | Backend | Net Worth History Aggregation API | MM-BE-15 | API endpoint to aggregate historical balances across all account types. | Done |
| MM-FE-17 | Web | Wire M5 Dashboard UI to Backend APIs | MM-BE-16, MM-BE-17, MM-BE-18 | `page.jsx` fetches real data for Net Worth, Asset Allocation, Recent Transactions, Recurring, and Debt. | Done |

### M6 UI Redesign and Theming
| ID | Domain | Task | Dependencies | Done Criteria | Status |
|---|---|---|---|---|---|
| MM-FE-10 | Web | Global Layout & Theming | MM-FE-01 | Dark/Light mode toggle, CSS variable color system, distinctive typography, and main application shell implemented. | Done |
| MM-FE-11 | Web | Dashboard Overview Screen | MM-FE-10 | Monthly spending chart, Net worth chart, Transactions to review, Top categories, and Next two weeks widgets implemented. | Done |
| MM-FE-12 | Web | Accounts Screen | MM-FE-10 | Assets/Debts summary chart, grouped account lists with sparklines, and right detail panel implemented. | Done |
| MM-FE-13 | Web | Transactions Screen | MM-FE-10 | Grouped transaction list with category tags and amounts, and right detail panel implemented. | Done |
| MM-FE-14 | Web | Categories & Budgeting Screen | MM-FE-10 | Total spent vs budget donut chart, detailed progress bars, and right detail panel implemented. | Done |
| MM-FE-15 | Web | Investments Screen | MM-FE-10 | Live balance estimate chart, top movers widget, account list with 1W balance change, and right detail panel implemented. | Done |
| MM-FE-16 | Web | Recurrings Screen | MM-FE-10 | Left to pay vs paid so far donut chart, list of recurring transactions with status, and right detail panel implemented. | Done |
| MM-FE-18 | Web | Semantic search and reranked dropdowns | MM-AI-05, MM-BE-10, MM-FE-17 | Search inputs and typeahead dropdowns use semantic retrieval + reranking so related intents (for example `utilities` and `water`) resolve together. | Done |
| MM-MOB-09 | Mobile | Semantic search and reranked pickers | MM-AI-05, MM-BE-10, MM-MOB-07.3 | Mobile search and picker flows use semantic retrieval + reranking with parity to web behavior and confidence-safe fallbacks. | Done |

### M7 Identity, Household Access Control, and Account Ownership
| ID | Domain | Task | Dependencies | Done Criteria | Status |
|---|---|---|---|---|---|
| MM-BE-19 | Backend | Add Mosaic user identity model | MM-BE-02, MM-BE-03 | `MosaicUsers` introduced with unique auth-subject mapping; household membership can bind to first-class app identities. | Done |
| MM-BE-20 | Backend | Evolve household membership model | MM-BE-19 | `HouseholdUsers` evolves into membership lifecycle (active/invited/removed) without breaking existing `NeedsReviewByUserId` references. | Done |
| MM-BE-21 | Backend | Add account ACL model | MM-BE-20 | Account-level access table supports `Owner`, `ReadOnly`, and hidden/no-access states for each household member. | Done |
| MM-BE-22 | Backend | Add Plaid account link mapping table | MM-BE-06, MM-BE-21 | Plaid item/account linkage is durable and unique, supports unlink/relink, and avoids duplicate account materialization. | Done |
| MM-BE-23 | Backend | Enforce membership-aware API authorization | MM-BE-21, MM-BE-04 | Transaction/account queries are filtered by member ACL visibility; direct account-id access without membership is denied. | Done |
| MM-BE-24 | Backend | Identity and ACL migration/backfill | MM-BE-19, MM-BE-20, MM-BE-21, MM-BE-22 | Existing households/accounts are backfilled fail-closed with explicit review queue for ambiguous sharing defaults. | Done |
| MM-ASP-08 | DevOps | Identity claim mapping configuration | MM-BE-19 | AppHost/API/Web/Mobile identity claim mapping is documented and reproducible across local and CI environments. | Done |
| MM-ASP-09 | DevOps | Migration rollout and rollback playbook | MM-BE-24 | Step-by-step rollout, reconciliation checks, and rollback paths documented for account access migration. | Done |
| MM-FE-19 | Web | Household member and invite management UI | MM-BE-20, MM-FE-10 | Web supports invite/accept/remove member workflows and membership status visibility. | Done |
| MM-FE-20 | Web | Account sharing controls UI | MM-BE-21, MM-FE-19 | Web supports per-account visibility/role assignment (mine-only, spouse-only, joint, read-only) with confirmation UX. | Done |
| MM-FE-21 | Web | Account visibility filters and badges | MM-BE-23, MM-FE-20 | Account/transaction views expose `mine`, `joint`, `shared` filters and clear hidden/read-only badges. | Done |
| MM-MOB-10 | Mobile | Membership and invite parity | MM-BE-20, MM-MOB-07 | Mobile supports member lifecycle views and invite acceptance with parity to web semantics. | Done |
| MM-MOB-11 | Mobile | Account sharing controls parity | MM-BE-21, MM-MOB-10 | Mobile supports per-account sharing roles and visibility controls with safe defaults. | Done |
| MM-MOB-12 | Mobile | ACL-aware account and transaction views | MM-BE-23, MM-MOB-11 | Mobile renders member-scoped account/transaction lists with hidden/read-only behavior matching backend authorization. | Done |

### M8 Authentication and Authorization (Clerk)
| ID | Domain | Task | Dependencies | Done Criteria | Status |
|---|---|---|---|---|---|
| MM-ASP-10 | DevOps | Clerk runtime secret and issuer wiring | MM-ASP-03, MM-ASP-04 | AppHost/API/Web/Mobile receive Clerk keys/issuer via env + user-secrets contract with no hardcoded credentials. | Done |
| MM-ASP-11 | DevOps | Clerk tenant/provider configuration runbook | MM-ASP-10 | Source-linked runbook covers Clerk app creation, Microsoft SSO setup, and passkey enablement for local/CI use. | Done |
| MM-BE-25 | Backend | API JWT validation and fallback auth policy | MM-ASP-10, MM-BE-19 | API validates Clerk JWTs and applies deny-by-default authorization for protected routes. | Done |
| MM-BE-26 | Backend | Auth subject to Mosaic identity mapping | MM-BE-25, MM-BE-19, MM-BE-20 | `sub` claim maps to `MosaicUsers` and household membership context used for ACL-scoped reads/writes. | Done |
| MM-FE-22 | Web | Clerk web integration and protected routes | MM-ASP-10 | Web app uses Clerk provider, sign-in/sign-up routes, and guarded app-shell navigation. | Done |
| MM-FE-23 | Web | Accounts add-account CTA and Plaid entry path | MM-FE-22, MM-FE-12, MM-FE-09 | Accounts screen exposes clear Add Account/Plaid link entry point (top-level CTA) into onboarding flow. | Done |
| MM-FE-24 | Web | Settings IA for appearance and security | MM-FE-10, MM-FE-22 | Settings includes Appearance/Theming and Security sections, preserving existing design language. | Done |
| MM-MOB-13 | Mobile | Clerk Expo integration and sign-in flow | MM-ASP-10 | Mobile uses `@clerk/clerk-expo` + `expo-secure-store` token cache and custom sign-in flow with Microsoft option. | Blocked |
| MM-MOB-14 | Mobile | Settings and account-link entrypoint parity | MM-MOB-13, MM-MOB-10 | Mobile settings includes appearance/security entry points and Add Account path parity with web intent. | Done |
| MM-QA-04 | QA | Auth and access regression gate | MM-BE-26, MM-FE-24, MM-MOB-14 | Auth flows, protected endpoint behavior, and account-link navigation pass web/mobile/API validation matrix. | Blocked |

### M9 Cross-Surface Charting Framework Migration
| ID | Domain | Task | Dependencies | Done Criteria | Status |
|---|---|---|---|---|---|
| MM-FE-25 | Web | ApexCharts foundation and shared config layer | MM-FE-10 | Shared chart config/time-bucket selector modules exist and power migrated web chart surfaces. | Done |
| MM-FE-26 | Web | M6 chart migration to ApexCharts | MM-FE-25, MM-FE-11, MM-FE-12, MM-FE-14, MM-FE-15, MM-FE-16 | M6 dashboard/reporting surfaces use ApexCharts with token-consistent light/dark behavior. | Done |
| MM-MOB-15 | Mobile | Victory Native XL chart parity widgets | MM-MOB-09, MM-MOB-11, MM-MOB-12 | Mobile surfaces provide parity KPI charts using Victory Native XL primitives and interval-aware inputs. | Done |
| MM-QA-05 | QA | Chart interaction and theme parity gate | MM-FE-26, MM-MOB-15 | Playwright evidence verifies interactive web charts in light/dark themes and documents residual gaps with backlog items. | Done |

### M10 Runtime Agentic Orchestration and Conversational Assistant
| ID | Domain | Task | Dependencies | Done Criteria | Status |
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
| MM-MOB-16 | Mobile | Assistant parity with offline-safe queue | MM-AI-14, MM-MOB-14 | Mobile assistant screen with queued outbound prompts, async update handling, and parity approval interactions. | Not Started |
| MM-QA-06 | QA | Multi-agent runtime release gate | MM-ASP-13, MM-AI-15, MM-FE-28, MM-MOB-16 | Cross-surface gate validating routing correctness, policy denials, replay safety, and assistant UX acceptance. | Not Started |

### AP0 PostgreSQL Discrepancy Closure Wave
| ID | Domain | Task | Dependencies | Done Criteria | Status |
|---|---|---|---|---|---|
| AP0-EPIC | Cross-Surface | PostgreSQL discrepancy closure umbrella | MM-BE-06, MM-AI-12 | Taxonomy/data-discrepancy closure plan and execution are coordinated across backend, AI, web, mobile, ops, and QA with evidence artifacts. | In Progress |
| AP0-BE-01 | Backend | Taxonomy bootstrap seed and deterministic backfill | AP0-EPIC | Baseline categories/subcategories are seeded idempotently; deterministic backfill reduces null-heavy category assignment rates while ambiguous rows route to `NeedsReview`. | Not Started |
| AP0-BE-02 | Backend | Scoped ownership model for user/shared categories | AP0-EPIC | Schema supports `User`, `HouseholdShared`, and `Platform` taxonomy ownership without breaking existing transaction links. | Not Started |
| AP0-BE-03 | Backend | Category lifecycle API (CRUD/reorder/reparent/audit) | AP0-BE-02 | Scope-aware endpoints exist for category/subcategory lifecycle operations with auditability, idempotent ordering, and fail-closed authorization. | Not Started |
| AP0-FE-01 | Web | Settings categories management experience | AP0-BE-03 | Web settings supports category/subcategory create/edit/delete/reorder/reparent with scope-aware UX and error handling. | Not Started |
| AP0-MOB-01 | Mobile | Mobile settings category management parity | AP0-BE-03, AP0-FE-01 | Mobile supports parity category management flows with offline-safe mutation queue and sync recovery behavior. | Not Started |
| AP0-OPS-01 | DevOps/Ops | Internal admin CRUD for platform taxonomy tables | AP0-BE-03 | Operator-only admin lane manages platform taxonomy with auditable provenance, role protection, and rollback-safe workflows. | Not Started |
| AP0-AI-01 | AI | Taxonomy readiness gates for ingestion/classification | AP0-BE-01, AP0-BE-03 | Taxonomy readiness checks gate routing behavior and improve fill-rates without violating fail-closed policy constraints. | Not Started |
| AP0-QA-01 | QA | AP0 discrepancy closure release gate and evidence pack | AP0-BE-01, AP0-BE-02, AP0-BE-03, AP0-FE-01, AP0-MOB-01, AP0-OPS-01, AP0-AI-01 | End-to-end SQL/API/web/mobile/AI evidence confirms discrepancy closure and ownership/security boundaries. | Not Started |

Update note (2026-02-25): Planner kickoff for M8 is created from Clerk authentication requirements. Initial implementation slice tasks (`MM-ASP-10`, `MM-ASP-11`, `MM-BE-25`, `MM-FE-22`, `MM-FE-23`, `MM-MOB-13`) are set to `In Progress` prior to specialist delegation.

Update note (2026-02-25): `MM-ASP-10` and `MM-ASP-11` implementation slice now includes AppHost Clerk parameter/env wiring for API and web, placeholder contract updates (`appsettings.json`, web/mobile `.env.example`), and a new source-linked Clerk tenant/provider runbook at `docs/agent-context/clerk-tenant-provider-runbook.md`. Task statuses remain `In Progress` pending planner review.

Update note (2026-02-25): `MM-BE-26` moved to `In Review` after implementing shared household member context resolution that maps authenticated subject claims to `MosaicUsers` and active `HouseholdUsers` membership context, with ambiguity and subject-mismatch guards covered by `ApiAuthorizationTests`.

Update note (2026-02-26): `MM-FE-24` and `MM-MOB-14` moved to `In Progress` for coordinated web/mobile settings and account-link parity implementation.

Update note (2026-02-26): `MM-FE-24` and `MM-MOB-14` are now `Done` after shipping web/mobile settings parity and account-link CTAs with validation evidence (`npm run build`, targeted web Playwright auth/account/settings specs, mobile typecheck, and focused mobile vitest suites).

Update note (2026-02-26): `MM-QA-04` moved to `In Review` after running API auth regression (`ApiAuthorizationTests`), web auth/account-link route checks, and mobile compile/regression validations.

Update note (2026-02-26): Planner revalidation wave completed with refreshed backend/auth and Playwright evidence. Tasks moved to `Blocked` now include explicit blockers:
- `MM-ASP-10`: AppHost runtime missing required Clerk parameter values (`clerk-issuer`, `clerk-publishable-key`, `clerk-secret-key`) and `web-installer` dependency conflict for `@clerk/nextjs` with React 19.2.0 during orchestration startup.
- `MM-FE-10..16`, `MM-FE-18`: Full web Playwright regression currently fails (`dashboard-and-transactions`, `navigation-responsive`, `needs-review`) and requires selector/interaction contract reconciliation before promotion.
- `MM-MOB-08`, `MM-MOB-09`, `MM-MOB-13`: mobile device-level Clerk/Plaid/semantic-flow validation remains pending in this environment.
- `MM-QA-04`: blocked by unresolved auth runtime prerequisites and failing full web regression pack.

Update note (2026-02-27): Planner evidence pass promoted `MM-ASP-10`, `MM-ASP-11`, `MM-BE-25`, `MM-BE-26`, `MM-FE-22`, and `MM-FE-23` to `Done` after:
- Healthy AppHost Clerk runtime configuration with active web auth sessions.
- Two-persona Clerk validation with DB-mapped `MosaicUsers`/`HouseholdUsers` rows and ACL coverage.
- Authenticated API proof (`/api/v1/households`, `/api/v1/transactions`) plus protected-route behavior verification.
- Web Accounts Add Account CTA confirmation and post-sign-in dashboard load verification.

`MM-QA-04` remains `Blocked` only on pending mobile end-to-end auth/sign-in execution under `MM-MOB-13`.

Update note (2026-02-27): Planner applied a mobile auth transport remediation for M8 by wiring Clerk bearer token forwarding into mobile API requests (`src/MosaicMoney.Mobile/app/_layout.tsx` and `src/MosaicMoney.Mobile/src/shared/services/mobileApiClient.ts`) while preserving `X-Mosaic-Household-User-Id` mapping behavior. Focused validation evidence is captured in `artifacts/release-gates/mm-qa-04/mobile-clerk-token-forwarding-validation.md` (mobile typecheck + focused regression suites pass). `MM-MOB-13` and `MM-QA-04` remain `Blocked` pending device-level OAuth sign-in execution and full matrix rerun.

Update note (2026-02-27): Planner reran live two-persona web auth triage and captured refreshed evidence in `artifacts/release-gates/mm-qa-04/live-triage/summary.json` plus `artifacts/release-gates/mm-qa-04/live-triage/triage-findings-2026-02-27.md`:
- Partner B signs in successfully and all tested routes return `200` (including `/dashboard` resolving to `/`, no `404`).
- Partner A remains blocked at Clerk factor-one; Clerk sign-in API response includes `form_password_incorrect`.

`MM-QA-04` remains `Blocked` pending Partner A credential/account recovery and one final full auth/access matrix rerun. `MM-MOB-13` remains `Blocked` pending device-level OAuth sign-in evidence.

Update note (2026-02-27): Planner closed the Partner A auth blocker and delivered explicit sign-out UX with refreshed live evidence:
- Web shell now exposes a dedicated `Sign Out` button wired to Clerk `signOut()` (`src/MosaicMoney.Web/components/layout/Shell.jsx`).
- Sign-out roundtrip evidence (`artifacts/release-gates/mm-qa-04/live-triage/signout-roundtrip-summary.json`) confirms A sign-in -> sign-out -> B sign-in flow succeeds.
- Full live partner triage rerun (`artifacts/release-gates/mm-qa-04/live-triage/summary.json`) now shows both Partner A and Partner B reach `/` and all tested routes return `200`.
- ACL visibility proof (`artifacts/release-gates/mm-qa-04/live-triage/partner-acl-api-validation-summary.json`) confirms `A only` / `B only` / `Joint` separation through protected API checks.

`MM-QA-04` remains `Blocked` only on pending mobile device-level OAuth evidence under `MM-MOB-13`.

Update note (2026-02-26): Planner resumed cross-surface completion wave. Tasks moved to `In Progress`: `MM-BE-06`, `MM-FE-09`, `MM-AI-12`, `MM-FE-10..16`, `MM-FE-18`, `MM-MOB-09`, `MM-FE-20`, `MM-FE-21`, `MM-MOB-11`, and `MM-MOB-12`. `MM-MOB-13` and `MM-QA-04` remain excluded/blocked for the current wave per explicit mobile-auth deferral.

Update note (2026-02-27): Planner final acceptance promoted `MM-BE-06` to `Done` after focused backend verification passed for duplicate-safe raw/enriched persistence and Plaid history-depth wiring (`days_requested` bounded to 24 months): `PlaidDeltaIngestionServiceTests`, `PlaidHttpTokenProviderTests`, and `PlaidTransactionsSyncProcessorTests` (15 passed, 0 failed).

Update note (2026-02-26): Planner created M9 charting migration scope (`project-plan/specs/010-m9-cross-surface-charting-framework-migration.md`) and started tasks `MM-FE-25`, `MM-FE-26`, `MM-MOB-15`, and `MM-QA-05` in `In Progress` for delegated implementation and validation.

Update note (2026-02-26): Planner validation closeout promoted `MM-FE-09`, `MM-FE-10..16`, `MM-FE-18`, `MM-FE-20`, `MM-FE-21`, `MM-MOB-09`, `MM-MOB-11`, `MM-MOB-12`, `MM-FE-25`, `MM-FE-26`, and `MM-QA-05` to `Done` after web/mobile verification evidence:
- Web: `npm run build`, `npm run test:e2e`, and focused chart parity coverage in `tests/e2e/chart-theme-parity.spec.js` (desktop light/dark interaction + responsive chart-route rendering).
- Mobile: `npm run typecheck`, `npm run test:sync-recovery`, `npm run test:review-projection`, and `npx expo install --check` with SDK-compatibility updates.

Update note (2026-02-26): `MM-MOB-15` is moved to `In Review`. Victory Native XL chart primitives are integrated, but investments trend data still uses temporary mock history pending backend historical-series wiring. Gap is tracked as GitHub issue `#125` (`MM-MOB-GAP-01`).

Update note (2026-02-27): `MM-MOB-GAP-01` (`#125`) is now closed by replacing mobile `InvestmentsOverviewScreen` mock trend history with API-backed `/api/v1/net-worth/history` investment series mapping via `mobileInvestmentsHistoryApi`. Validation evidence: `npm --prefix src/MosaicMoney.Mobile run typecheck`, `npm --prefix src/MosaicMoney.Mobile run test:sync-recovery`, `npm --prefix src/MosaicMoney.Mobile run test:review-projection`, and focused `mobileInvestmentsHistoryApi.test.ts` all pass. With the residual gap resolved, `MM-MOB-15` is promoted to `Done`.

Update note (2026-02-27): Daily kickoff AI audit and specialist delegation review confirms `MM-AI-12` still has outstanding replay-pack evidence gaps (`artifacts/release-gates/mm-ai-12/criteria-dataset-mapping.json` and `artifacts/release-gates/mm-ai-12/replay-pack.md`) plus pending cloud evaluator execution evidence. `MM-AI-12` remains `In Progress` and will not be promoted until those artifacts are produced and validated.

Update note (2026-02-27): Planner closeout promoted `MM-AI-12` (`#78`) to `Done` after replay-pack completion and validation reruns:
- Regenerated evaluator artifacts via `pwsh .github/scripts/run-mm-ai-11-release-gate.ps1`.
- Verified official and specialist artifact tests (`AgenticEvalOfficialEvaluatorArtifactsTests`, `AgenticEvalSpecialistEvaluatorArtifactsTests`) and release-gate checks passed (`14` passed, `0` failed in focused suite).
- Updated source-linked replay references to current `view=foundry` documentation and added a documentation review log section in `artifacts/release-gates/mm-ai-12/replay-pack.md`.
- Cloud evaluator execution remains an optional configured lane; fail-closed deterministic release-blocking behavior is preserved when cloud prerequisites are unavailable.

Update note (2026-02-27): Planner created M10 runtime-agentic milestone planning artifacts and synchronized GitHub work tracking (`#126` epic and `#127`, `#128`, `#132`, `#134`, `#137`, `#138`, `#139`, `#140`, `#141`, `#142`, `#143`) to Project 1 with `Not Started` status and parent/sub-issue relationships.

Update note (2026-02-27): Planner started active M10 implementation and moved `MM-ASP-12` (`#127`), `MM-BE-27` (`#132`), and `MM-AI-13` (`#139`) to `In Progress` for delegated DevOps/Backend/AI execution.

Update note (2026-02-27): Planner verification pass moved `MM-ASP-12` (`#127`), `MM-BE-27` (`#132`), and `MM-AI-13` (`#139`) to `In Review` after integrated compile/test checks passed for runtime messaging contracts, lifecycle schema constraints, and specialist routing policy (`19` focused tests passed, `0` failed).

Update note (2026-02-27): Planner final review promoted `MM-ASP-12` (`#127`), `MM-BE-27` (`#132`), and `MM-AI-13` (`#139`) to `Done` after additional integrated verification: AppHost/API/Worker builds succeed with Aspire-native runtime messaging wiring and focused lifecycle/routing/runtime tests pass (`21` passed, `0` failed across `RuntimeMessagingBackboneOptionsTests`, `AgentWorkflowLifecycleModelContractTests`, `ClassificationSpecialistRoutingPolicyTests`, and `DeterministicClassificationOrchestratorTests`).

Update note (2026-02-27): Planner closeout promoted `MM-ASP-13` (`#138`), `MM-BE-28` (`#128`), `MM-AI-14` (`#137`), `MM-AI-15` (`#140`), `MM-FE-27` (`#134`), and `MM-FE-28` (`#141`) to `Done` after integrated validation evidence:
- Runtime verification: AppHost/API/Worker/Web healthy with runtime messaging and telemetry resources green (`runtime-messaging`, `runtime-ingestion-completed`, `runtime-assistant-message-posted`, `runtime-nightly-anomaly-sweep`, `runtime-telemetry`, `runtime-telemetry-stream`, `mosaic-money-runtime`).
- Backend/AI tests: focused suite passed (`22` passed, `0` failed) including `AgentWorkflowLifecycleModelContractTests`, `RuntimeMessagingBackboneOptionsTests`, `ClassificationSpecialistRoutingPolicyTests`, `AgenticEvalReleaseGateTests`, `AgenticEvalSpecialistEvaluatorPackTests`, and `AgenticEvalSpecialistEvaluatorArtifactsTests`.
- Web build: `npm --prefix src/MosaicMoney.Web run build` succeeded for assistant/provenance surfaces (expected non-blocking local API base URL warning outside Aspire-injected runtime).
- Mobile auth exception preserved per planner direction: `MM-MOB-13` remains `Blocked` and excluded from this closeout wave.

Update note (2026-02-27): Planner backfill sync added AP0 discrepancy-closure work items already tracked on Project 1 (`#144`-`#152`) into spec governance. `AP0-EPIC` (`#144`) is now `In Progress` for active planning/execution orchestration; child work items remain `Not Started` pending implementation sequencing.

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



