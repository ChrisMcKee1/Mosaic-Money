# Spec 003: M2 Ledger Truth and Review Core

## Status
- Drafted: 2026-02-20
- Milestone: M2
- Depends on:
- `project-plan/specs/001-mvp-foundation-task-breakdown.md`
- `project-plan/specs/002-m1-platform-and-contract-foundation.md`

## Objective
Deliver the first complete human-reviewed ledger loop: Plaid account-link onboarding, ingest-to-review-to-approval, strict single-entry integrity, and a documented Plaid capability-to-PRD product map before broadening product scope.

## In Scope
- Plaid Link token creation and server-side public-token exchange lifecycle.
- Plaid Item and webhook recovery contracts for relink/update-mode flows.
- Plaid product capability research that maps PRD scenarios to Plaid APIs, webhooks, and sandbox simulation coverage.
- `NeedsReview` state machine and approval transitions.
- Idempotent ingestion from raw payload to enriched ledger records.
- Web transaction list and review queue UX.
- Mobile review queue, transaction detail, and approval actions.

## Out of Scope
- Recurring matching logic.
- Reimbursement candidate-linking model.
- Semantic embedding jobs and MAF fallback.
- Safe-to-spend and projection dashboards.
- Any client-side handling of Plaid secrets or direct token exchange from web/mobile.
- Shipping additional Plaid product ingestion lanes before capability mapping and schema impacts are approved.

## Guardrails
- Every ambiguous or low-confidence item must route to `NeedsReview`.
- Ledger amounts/dates remain source-of-truth and read-only from web/mobile clients.
- `UserNote` and `AgentNote` remain distinct and separately rendered.

## Task Breakdown
| ID | Task | Dependencies | Deliverable | Status |
|---|---|---|---|---|
| MM-BE-05 | NeedsReview state machine and transitions | MM-BE-04, MM-AI-01 | Explicit transition rules with fail-closed behavior for ambiguity. | Done |
| MM-BE-06 | Idempotent ingestion pipeline (`raw -> enriched`) | MM-BE-03, MM-BE-05 | Duplicate-safe upsert with note preservation and review routing. | In Review |
| MM-BE-12 | Plaid Link token lifecycle endpoints | MM-BE-04, MM-ASP-03 | Backend issues OAuth-capable Link token configs and records link session metadata for diagnostics/support. | Done |
| MM-BE-13 | Public token exchange and Item credential storage | MM-BE-12 | Backend exchanges `public_token` to `access_token` + `item_id` and stores credentials in secure backend storage paths only. | Done |
| MM-BE-14 | Plaid webhook and Item recovery contract | MM-BE-13, MM-BE-05 | Item/webhook errors (including OAuth expiry/revocation) produce explicit relink/update-mode actions and review-safe routing. | Done |
| MM-BE-15 | Plaid product capability mapping research | MM-BE-12, MM-BE-13, MM-BE-14 | Cross-reference PRD capabilities to Plaid products/endpoints/webhooks, validate sandbox simulation coverage, and publish MVP product decisions with schema + ingestion implications. | Done |
| MM-FE-04 | Read-only ledger transaction list | MM-FE-02, MM-FE-03, MM-BE-04 | Web list view with separated dual notes and no ledger mutation controls. | Done |
| MM-FE-05 | NeedsReview queue and approval UI | MM-FE-04, MM-BE-05 | Approve/reject/reclassify user actions integrated with backend review endpoints. | Done |
| MM-FE-09 | Plaid Link onboarding flow (web) | MM-FE-02, MM-BE-12, MM-BE-13 | Web launches Link with server-issued token and posts `public_token` + metadata to backend exchange endpoint. | Blocked |
| MM-MOB-02 | Offline-safe state/caching foundation | MM-MOB-01 | Queue-friendly offline cache and sync baseline. | Done |
| MM-MOB-03 | NeedsReview queue screen | MM-MOB-02, MM-BE-05 | Mobile review inbox with clear pending statuses. | Done |
| MM-MOB-04 | Transaction detail with dual notes | MM-MOB-01, MM-BE-04 | Clear distinction of `UserNote` and `AgentNote` in mobile detail view. | Done |
| MM-MOB-05 | Human-in-the-loop approval actions | MM-MOB-03, MM-MOB-04, MM-BE-05 | Mobile approve/reject actions with explicit user confirmation and sync behavior. | Done |
| MM-MOB-08 | Plaid Link onboarding flow (mobile) | MM-MOB-01, MM-BE-12, MM-BE-13 | React Native Link SDK flow submits `public_token` for backend exchange and does not expose Plaid secrets on-device. | Blocked |

Update note (2026-02-23): `src/MosaicMoney.Mobile` scaffold now exists, so `MM-MOB-03` and `MM-MOB-04` are unblocked and now in `In Review` after delegated implementation and typecheck pass.

Update note (2026-02-23): Plaid onboarding statuses were rolled back from `Done` pending end-to-end runtime validation (API startup health + migration parity + live provider wiring). Product expansion beyond `transactions` now requires completing `MM-BE-15` first.

Update note (2026-02-23): The source-linked `MM-BE-15` capability matrix is published at `project-plan/specs/003a-mm-be-15-plaid-product-capability-matrix.md`, and planner review is complete (`MM-BE-15` is `Done`) as the gating artifact for non-`transactions` product-lane decisions.

Update note (2026-02-23): First delegated `transactions` backend slice is complete with durable per-item sync state (`PlaidItemSyncStates`) and `SYNC_UPDATES_AVAILABLE` webhook ingestion at `POST /api/v1/plaid/webhooks/transactions`; cursor pull execution worker remains the next follow-on implementation slice.

Update note (2026-02-23): Runtime validation confirmed local Plaid onboarding infrastructure is stable (`web` endpoint fixed to `http://localhost:53832`, `pgvector/pgvector:pg17` image in AppHost, and successful startup migration application), but end-to-end Plaid Sandbox data persistence is still pending because API wiring currently uses deterministic token simulation. `MM-BE-12/13/14` are returned to `In Progress` until real Plaid Sandbox provider wiring and persisted transaction sync evidence are complete. `MM-MOB-08` is `Parked` until backend provider readiness is restored.

Update note (2026-02-23): Plaid backend now defaults to real provider wiring for `/link/token/create` and `/item/public_token/exchange`, with `/transactions/sync` bootstrap executed during exchange to initialize per-item durable cursor state. Deterministic simulation remains an explicit local/test fallback behind `Plaid:UseDeterministicProvider=true`.

Update note (2026-02-23): Delegated execution checkpoint validated the new hosted Plaid sync processor path (paged `/transactions/sync` pull from stored credentials into ingestion + embedding pipelines) and captured non-empty persistence/API retrieval evidence for local sandbox validation. A follow-on fail-closed guard now blocks silent success when no local account mapping exists for incoming Plaid deltas. `MM-BE-12/13/14` remain `In Progress` until the full sandbox webhook-to-ingestion loop is rerun as the primary automated path without manual fallback steps.

Update note (2026-02-24): Planner reran sandbox happy-path validation end-to-end using real Plaid provider wiring and verified non-empty persistence through the primary pipeline (`PlaidItemCredentials`, `PlaidItemSyncStates`, `RawTransactionIngestionRecords`, `EnrichedTransactions`) plus API retrieval via `GET /api/v1/transactions`. With this runtime proof gate satisfied, `MM-BE-12`, `MM-BE-13`, and `MM-BE-14` are promoted to `Done`.

Update note (2026-02-24): Frontend execution is intentionally paused due frontend-agent model unavailability; `MM-FE-09` is moved to `Parked` pending frontend capacity restoration.

Update note (2026-02-24): Planner backlog sweep unparked `MM-FE-09` and `MM-MOB-08` to `Not Started` after backend Plaid readiness gates were closed and local execution capacity was re-verified.

Update note (2026-02-25): Delegation attempts for `MM-FE-09` and `MM-MOB-08` are blocked by upstream specialist provider-capacity errors; tasks remain blocked pending frontend/mobile specialist availability or a manual planner fallback implementation.

Update note (2026-02-25): `MM-BE-06` is reopened for a targeted Plaid transactions-history enhancement to request and ingest the maximum available historical range (up to 24 months) during onboarding/sync initialization while maintaining idempotent raw-to-enriched behavior.

Update note (2026-02-25): `MM-BE-06` implementation now includes explicit `days_requested` request wiring for Plaid transactions initialization (`/link/token/create` and bootstrap `/transactions/sync` path), plus bounded configuration (`30..730`, default `730`) and passing focused backend tests. Status advanced to `In Review`.

## Plaid Product Capability Research Gate (`MM-BE-15`)
Complete this gate before implementing non-`transactions` product ingestion lanes.

1. Cross-reference PRD user outcomes with Plaid product capabilities and endpoint families (for example: Transactions Sync, Auth, Identity, Investments, Liabilities, Income, Statements).
2. Confirm sandbox coverage per target product, including institution availability, test users, webhook behavior, and known limitations.
3. Produce a source-linked matrix with columns for PRD need, Plaid product, endpoint/webhook contract, required backend schema fields, required API contracts, and review/HITL implications.
4. Define MVP product decisions explicitly as `Adopt Now`, `Adopt Later`, or `Out of Scope`, with rationale and risk notes.
5. Document implementation order and migration plan so schema and ingestion changes are incremental and verifiable.

Research artifact: `project-plan/specs/003a-mm-be-15-plaid-product-capability-matrix.md`

## Acceptance Criteria
- Plaid onboarding uses server-generated `link_token` and server-side `public_token` exchange only.
- Plaid credentials (`client_id`, `secret`, `access_token`) remain off client surfaces and out of committed files/log output.
- OAuth/item error conditions route to explicit update-mode/relink actions and never auto-resolve high-impact outcomes.
- `MM-BE-15` produces a reviewed, source-linked product capability map that is approved before adding product lanes beyond `transactions`.
- Ambiguous transactions consistently land in `NeedsReview`.
- Approvals/rejections are audit-friendly and cannot be auto-completed by the system.
- Ingestion remains idempotent and does not duplicate transaction truth.
- Web and mobile can process review workflows end-to-end using the same API contract.

## Verification
- Contract tests for Plaid Link token issuance and `public_token` exchange endpoint behavior.
- Integration tests for webhook error routes (e.g., OAuth invalid/expired/revoked) to update-mode and review-safe states.
- Transition tests for valid/invalid review-state changes.
- Idempotency tests using repeated inbound transaction payloads.
- Research artifact review that verifies PRD-to-Plaid capability mapping, sandbox support, and schema/API impact decisions.
- Web interaction tests for review queue actions.
- Mobile flow tests for offline pending actions and later synchronization.

## Risks and Mitigations
- Risk: State-transition bugs enabling bypass of review.
- Mitigation: Exhaustive transition testing and fail-closed defaults.
- Risk: UX confusion between user and AI notes.
- Mitigation: Explicit labels, visual separation, and contract-level naming consistency.

## Exit Criteria
M2 exits when users can review and resolve ambiguous transactions from both web and mobile while preserving immutable ledger truth semantics.