# Spec 004: M3 Ingestion, Recurring, Reimbursements, and Projections

## Status
- Drafted: 2026-02-20
- Milestone: M3
- Depends on:
- `project-plan/specs/001-mvp-foundation-task-breakdown.md`
- `project-plan/specs/002-m1-platform-and-contract-foundation.md`
- `project-plan/specs/003-m2-ledger-truth-and-review-core.md`

## Subagent Inputs
- `mosaic-money-backend`: M3 backend gaps, recurring/reimbursement contracts, idempotency, and review routing.
- `mosaic-money-frontend`: M3 web projection/isolation UX and Playwright validation.
- `mosaic-money-mobile`: M3 mobile read-only projection dashboard scope and checks.
- `mosaic-money-ai`: M3 AI boundary guardrails and anti-leakage tests (defer AI escalation to M4).

## Objective
Implement deterministic recurring matching, human-approved reimbursement linking, and projection metadata required for web and mobile dashboards, while preserving immutable single-entry ledger truth.

## Gap Analysis vs Specs 002-003
- Specs 002-003 did not fully define recurring match policy details (variance, due-window drift, tie-break rules).
- Specs 002-003 did not define a complete reimbursement proposal/approval state contract for 1:N linking.
- Specs 002-003 did not define projection metadata payload shape for web/mobile rendering.
- M3 required explicit anti-leakage rules to prevent accidental introduction of M4 AI features.

## In Scope
- Deterministic recurring matching policy and execution.
- Reimbursement proposal model with mandatory human approval before link finalization.
- Projection metadata read contracts and query paths.
- Web: business vs household isolation visuals and recurring/safe-to-spend projection views.
- Mobile: read-only dashboard projection rendering with resilient states.
- Guardrail tests proving M3 remains non-autonomous for AI/external messaging.

## Out of Scope
- Embeddings queue and semantic retrieval.
- MAF fallback orchestration.
- Autonomous reimbursement resolution.
- Mutation of ledger amounts/dates for amortization or projections.

## Guardrails
- Single-entry ledger only.
- Ambiguous recurring/reimbursement outcomes route to `NeedsReview`.
- Reimbursement links require explicit human approval.
- `UserNote` and `AgentNote` remain separate fields.
- M3 must not invoke semantic retrieval, embeddings, LLM, or MAF runtime.
- External messaging remains draft-only; send actions are denied.

## Task Breakdown
| ID | Domain | Task | Dependencies | Deliverable | Status |
|---|---|---|---|---|---|
| MM-BE-07A | Backend | Recurring policy contract | MM-BE-03, MM-BE-04 | Configurable due window, variance rules, deterministic scoring/tie-break behavior. | Done |
| MM-BE-07B | Backend | Recurring matcher execution in ingestion | MM-BE-06, MM-BE-07A, MM-BE-05 | Confident matches link recurring items and advance next due date idempotently. | Not Started |
| MM-BE-07C | Backend | Recurring ambiguity routing | MM-BE-07B, MM-BE-05 | Ambiguous/competing recurring candidates create `NeedsReview` items with reason codes. | Not Started |
| MM-BE-08A | Backend | Reimbursement proposal model (1:N) | MM-BE-04, MM-BE-05, MM-BE-06 | Proposal lifecycle with explicit statuses and rationale/provenance fields. | Done |
| MM-BE-08B | Backend | HITL reimbursement approval/rejection APIs | MM-BE-08A, MM-BE-05 | Approval-only persistence with actor/time/audit metadata. | Not Started |
| MM-BE-08C | Backend | Reimbursement conflict routing | MM-BE-08A, MM-BE-05 | Over-allocation/duplicate/stale proposal conflicts route to `NeedsReview`. | Not Started |
| MM-BE-09A | Backend | Projection metadata read contract | MM-BE-04, MM-BE-07B, MM-BE-08B | API payload includes projection fields without changing ledger truth. | Not Started |
| MM-BE-09B | Backend | Projection-safe query path | MM-BE-03, MM-BE-09A | Read-optimized query/view exposing projection metadata only. | Not Started |
| MM-FE-06 | Web | Business vs household isolation visuals | MM-FE-02, MM-BE-09A | Dashboard clearly separates household budget burn from total liquidity. | Not Started |
| MM-FE-07 | Web | Recurring and safe-to-spend projection UI | MM-FE-06, MM-BE-07B, MM-BE-09A | Projection visuals and transparent safe-to-spend derivation. | Not Started |
| MM-MOB-06.1 | Mobile | Shared projection hooks | MM-MOB-01, MM-BE-09A | Shared hooks fetch/validate projection payloads for mobile read views. | Not Started |
| MM-MOB-06.2 | Mobile | Dashboard route scaffold | MM-MOB-06.1 | Mobile dashboard route for projection and balance presentation. | Not Started |
| MM-MOB-06.3 | Mobile | Read-only projection components | MM-MOB-06.2 | Touch-friendly projection UI with no ledger mutation actions. | Not Started |
| MM-MOB-06.4 | Mobile | Resilient loading/offline states | MM-MOB-06.3 | Skeleton/loading/retry/offline behaviors for projection views. | Not Started |
| MM-M3-GOV-01 | Cross-domain | AI boundary and autonomy-denial checks | MM-BE-08B, MM-BE-09A | Negative checks proving no M4 AI execution and no outbound send behavior in M3. | Not Started |

## Acceptance Criteria
- Recurring matching honors configured drift and variance thresholds and fails closed to `NeedsReview` for uncertainty.
- Reimbursement links are never finalized without explicit human approval.
- Projection endpoints expose metadata needed by web/mobile while preserving raw transaction amount/date truth.
- Web clearly distinguishes household budget burn, business isolation, and total liquidity.
- Mobile dashboard renders read-only projection data and handles loading/offline states safely.
- No M3 flow invokes semantic retrieval, embeddings, MAF, or outbound messaging send operations.

## Verification
- Backend unit tests for recurring boundary conditions and deterministic tie-break rules.
- Backend idempotency tests for repeated ingestion deltas and recurring due-date updates.
- Backend integration tests for reimbursement proposal lifecycle and approval conflict cases.
- Contract tests for projection metadata fields and immutability of ledger source values.
- Playwright tests for web context toggles, projection rendering, and safe-to-spend derivation display.
- Mobile tests for schema-validated projection rendering, offline state behavior, and no-mutation audit.
- Negative tests to verify M3 rejects autonomous send/auto-approve pathways.

## Risks and Mitigations
- Risk: False-positive recurring links.
- Mitigation: Deterministic scoring thresholds plus ambiguity routing to `NeedsReview`.
- Risk: Hidden projection logic mutating ledger truth.
- Mitigation: Read-only projection contracts with immutability tests.
- Risk: Reimbursement mis-linking under concurrency.
- Mitigation: Explicit proposal states, optimistic concurrency, stale-proposal rejection.
- Risk: AI scope leakage into M3.
- Mitigation: Explicit anti-leakage acceptance criteria and negative integration tests.

## Exit Criteria
M3 exits when recurring and reimbursement workflows are deterministic and review-safe, projection metadata is consumable by web/mobile, and anti-autonomy guardrail checks pass.