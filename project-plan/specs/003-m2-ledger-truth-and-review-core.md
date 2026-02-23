# Spec 003: M2 Ledger Truth and Review Core

## Status
- Drafted: 2026-02-20
- Milestone: M2
- Depends on:
- `project-plan/specs/001-mvp-foundation-task-breakdown.md`
- `project-plan/specs/002-m1-platform-and-contract-foundation.md`

## Objective
Deliver the first complete human-reviewed ledger loop: Plaid account-link onboarding, ingest-to-review-to-approval, and strict single-entry integrity.

## In Scope
- Plaid Link token creation and server-side public-token exchange lifecycle.
- Plaid Item and webhook recovery contracts for relink/update-mode flows.
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

## Guardrails
- Every ambiguous or low-confidence item must route to `NeedsReview`.
- Ledger amounts/dates remain source-of-truth and read-only from web/mobile clients.
- `UserNote` and `AgentNote` remain distinct and separately rendered.

## Task Breakdown
| ID | Task | Dependencies | Deliverable | Status |
|---|---|---|---|---|
| MM-BE-05 | NeedsReview state machine and transitions | MM-BE-04, MM-AI-01 | Explicit transition rules with fail-closed behavior for ambiguity. | Done |
| MM-BE-06 | Idempotent ingestion pipeline (`raw -> enriched`) | MM-BE-03, MM-BE-05 | Duplicate-safe upsert with note preservation and review routing. | Done |
| MM-BE-12 | Plaid Link token lifecycle endpoints | MM-BE-04, MM-ASP-03 | Backend issues OAuth-capable Link token configs and records link session metadata for diagnostics/support. | Done |
| MM-BE-13 | Public token exchange and Item credential storage | MM-BE-12 | Backend exchanges `public_token` to `access_token` + `item_id` and stores credentials in secure backend storage paths only. | Done |
| MM-BE-14 | Plaid webhook and Item recovery contract | MM-BE-13, MM-BE-05 | Item/webhook errors (including OAuth expiry/revocation) produce explicit relink/update-mode actions and review-safe routing. | Done |
| MM-FE-04 | Read-only ledger transaction list | MM-FE-02, MM-FE-03, MM-BE-04 | Web list view with separated dual notes and no ledger mutation controls. | Done |
| MM-FE-05 | NeedsReview queue and approval UI | MM-FE-04, MM-BE-05 | Approve/reject/reclassify user actions integrated with backend review endpoints. | Done |
| MM-FE-09 | Plaid Link onboarding flow (web) | MM-FE-02, MM-BE-12, MM-BE-13 | Web launches Link with server-issued token and posts `public_token` + metadata to backend exchange endpoint. | Done |
| MM-MOB-02 | Offline-safe state/caching foundation | MM-MOB-01 | Queue-friendly offline cache and sync baseline. | Done |
| MM-MOB-03 | NeedsReview queue screen | MM-MOB-02, MM-BE-05 | Mobile review inbox with clear pending statuses. | In Progress |
| MM-MOB-04 | Transaction detail with dual notes | MM-MOB-01, MM-BE-04 | Clear distinction of `UserNote` and `AgentNote` in mobile detail view. | In Progress |
| MM-MOB-05 | Human-in-the-loop approval actions | MM-MOB-03, MM-MOB-04, MM-BE-05 | Mobile approve/reject actions with explicit user confirmation and sync behavior. | Not Started |
| MM-MOB-08 | Plaid Link onboarding flow (mobile) | MM-MOB-01, MM-BE-12, MM-BE-13 | React Native Link SDK flow submits `public_token` for backend exchange and does not expose Plaid secrets on-device. | Not Started |

Update note (2026-02-23): `src/MosaicMoney.Mobile` scaffold now exists, so `MM-MOB-03` and `MM-MOB-04` are unblocked and moved to `In Progress`.

## Acceptance Criteria
- Plaid onboarding uses server-generated `link_token` and server-side `public_token` exchange only.
- Plaid credentials (`client_id`, `secret`, `access_token`) remain off client surfaces and out of committed files/log output.
- OAuth/item error conditions route to explicit update-mode/relink actions and never auto-resolve high-impact outcomes.
- Ambiguous transactions consistently land in `NeedsReview`.
- Approvals/rejections are audit-friendly and cannot be auto-completed by the system.
- Ingestion remains idempotent and does not duplicate transaction truth.
- Web and mobile can process review workflows end-to-end using the same API contract.

## Verification
- Contract tests for Plaid Link token issuance and `public_token` exchange endpoint behavior.
- Integration tests for webhook error routes (e.g., OAuth invalid/expired/revoked) to update-mode and review-safe states.
- Transition tests for valid/invalid review-state changes.
- Idempotency tests using repeated inbound transaction payloads.
- Web interaction tests for review queue actions.
- Mobile flow tests for offline pending actions and later synchronization.

## Risks and Mitigations
- Risk: State-transition bugs enabling bypass of review.
- Mitigation: Exhaustive transition testing and fail-closed defaults.
- Risk: UX confusion between user and AI notes.
- Mitigation: Explicit labels, visual separation, and contract-level naming consistency.

## Exit Criteria
M2 exits when users can review and resolve ambiguous transactions from both web and mobile while preserving immutable ledger truth semantics.