# Spec 003: M2 Ledger Truth and Review Core

## Status
- Drafted: 2026-02-20
- Milestone: M2
- Depends on:
- `project-plan/specs/001-mvp-foundation-task-breakdown.md`
- `project-plan/specs/002-m1-platform-and-contract-foundation.md`

## Objective
Deliver the first complete human-reviewed ledger loop: ingest-to-review-to-approval with strict single-entry integrity.

## In Scope
- `NeedsReview` state machine and approval transitions.
- Idempotent ingestion from raw payload to enriched ledger records.
- Web transaction list and review queue UX.
- Mobile review queue, transaction detail, and approval actions.

## Out of Scope
- Recurring matching logic.
- Reimbursement candidate-linking model.
- Semantic embedding jobs and MAF fallback.
- Safe-to-spend and projection dashboards.

## Guardrails
- Every ambiguous or low-confidence item must route to `NeedsReview`.
- Ledger amounts/dates remain source-of-truth and read-only from web/mobile clients.
- `UserNote` and `AgentNote` remain distinct and separately rendered.

## Task Breakdown
| ID | Task | Dependencies | Deliverable | Status |
|---|---|---|---|---|
| MM-BE-05 | NeedsReview state machine and transitions | MM-BE-04, MM-AI-01 | Explicit transition rules with fail-closed behavior for ambiguity. | Not Started |
| MM-BE-06 | Idempotent ingestion pipeline (`raw -> enriched`) | MM-BE-03, MM-BE-05 | Duplicate-safe upsert with note preservation and review routing. | Not Started |
| MM-FE-04 | Read-only ledger transaction list | MM-FE-02, MM-FE-03, MM-BE-04 | Web list view with separated dual notes and no ledger mutation controls. | Not Started |
| MM-FE-05 | NeedsReview queue and approval UI | MM-FE-04, MM-BE-05 | Approve/reject/reclassify user actions integrated with backend review endpoints. | Not Started |
| MM-MOB-02 | Offline-safe state/caching foundation | MM-MOB-01 | Queue-friendly offline cache and sync baseline. | Not Started |
| MM-MOB-03 | NeedsReview queue screen | MM-MOB-02, MM-BE-05 | Mobile review inbox with clear pending statuses. | Not Started |
| MM-MOB-04 | Transaction detail with dual notes | MM-MOB-01, MM-BE-04 | Clear distinction of `UserNote` and `AgentNote` in mobile detail view. | Not Started |
| MM-MOB-05 | Human-in-the-loop approval actions | MM-MOB-03, MM-MOB-04, MM-BE-05 | Mobile approve/reject actions with explicit user confirmation and sync behavior. | Not Started |

## Acceptance Criteria
- Ambiguous transactions consistently land in `NeedsReview`.
- Approvals/rejections are audit-friendly and cannot be auto-completed by the system.
- Ingestion remains idempotent and does not duplicate transaction truth.
- Web and mobile can process review workflows end-to-end using the same API contract.

## Verification
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