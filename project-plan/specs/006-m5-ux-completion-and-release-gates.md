# Spec 006: M5 UX Completion and Release Gates

## Status
- Drafted: 2026-02-20
- Milestone: M5
- Depends on:
- `project-plan/specs/001-mvp-foundation-task-breakdown.md`
- `project-plan/specs/002-m1-platform-and-contract-foundation.md`
- `project-plan/specs/003-m2-ledger-truth-and-review-core.md`
- `project-plan/specs/004-m3-ingestion-recurring-reimbursements-projections.md`
- `project-plan/specs/005-m4-ai-escalation-pipeline-deterministic-semantic-maf.md`

## Subagent Inputs
- `mosaic-money-devops`: release-hardening and orchestration gate sequencing.
- `mosaic-money-backend`: financial correctness regression matrix and rollout/rollback controls.
- `mosaic-money-frontend`: web completion and Playwright matrix.
- `mosaic-money-mobile`: mobile offline/sync reliability and integration validation.

## Objective
Finalize MVP release readiness with measurable, auditable release gates across backend, AI routing safety, web UX, mobile reliability, and Aspire orchestration compliance.

## Gap Analysis vs Spec 005
- Spec 005 defines M4 AI pipeline behavior but does not finalize cross-surface release hardening.
- M5 needed unified go/no-go criteria tying backend correctness, AI safety, web/mobile regressions, and orchestration policy checks.
- M5 needed explicit release rehearsal and rollback criteria before production promotion.
- **UI Data Gap Analysis (2026-02-24)**: The new M5 Dashboard UI requires data that is not currently exposed by the backend or ingested from Plaid.
  - **Net Worth History**: Requires historical balances. Needs a backend API to aggregate historical snapshots.
  - **Asset Allocation**: Requires Plaid Investments API (`/investments/holdings/get`).
  - **Recurring Subscriptions**: Requires Plaid Recurring Transactions API (`/transactions/recurring/get`).
  - **Debt / Liabilities**: Requires Plaid Liabilities API (`/liabilities/get`) - backend ingestion is done, but needs UI API endpoints.
  - **Recent Transactions**: Requires Plaid Transactions API (`/transactions/sync`) - backend ingestion is done, but needs UI API endpoints.

## In Scope
- DevOps release-hardening gates (`MM-ASP-05`, `MM-ASP-06`, `MM-ASP-07`).
- Backend financial correctness/regression suite (`MM-BE-11`).
- AI release-eval gate (`MM-AI-11`) as deployment blocker signal.
- Web Playwright regression completion (`MM-FE-08`).
- Mobile integration/offline reliability validation (`MM-MOB-07`).
- Cross-domain QA gates, release rehearsal, and rollback readiness.
- **M5 Dashboard Data Wiring**: Implementing Plaid integrations and backend APIs for Investments, Recurring, and Net Worth History to support the new UI (`MM-BE-16`, `MM-BE-17`, `MM-BE-18`, `MM-FE-17`).

## Out of Scope
- New product feature scope beyond M1-M4.
- Any change weakening single-entry ledger constraints.
- Any bypass of `NeedsReview` for ambiguous/high-impact actions.
- Any autonomous outbound messaging behavior.

## Guardrails
- Single-entry ledger truth is immutable and authoritative.
- Ambiguous outcomes route to `NeedsReview`.
- Outbound messaging execution remains denied.
- Aspire policies remain mandatory (`WithReference(...)`, service defaults, no `AddNpmApp`).

## Task Breakdown
| ID | Domain | Task | Dependencies | Deliverable | Status |
|---|---|---|---|---|---|
| MM-ASP-05 | DevOps | Local run reliability hardening | MM-ASP-04 | Deterministic startup/recovery behavior with documented run paths. | Done |
| MM-ASP-06 | DevOps | Dashboard + MCP diagnostics flow | MM-ASP-05, MM-AI-10 | Standardized diagnostics for API, Worker, Web, and AI workflow traces. | In Progress |
| MM-ASP-07 | DevOps | Orchestration policy gate checks | MM-ASP-03, MM-ASP-04, MM-ASP-06 | Automated checks for disallowed patterns and missing orchestration conventions. | Not Started |
| MM-BE-11 | Backend | Financial correctness regression suite | MM-BE-10 | Money/date/matching/review/reimbursement/AI-routing regression matrix with pass gates. | In Review |
| MM-AI-11 | AI | Agentic eval release gate | MM-AI-10 | Thresholded routing correctness and safety report for release blocking decisions. | Not Started |
| MM-FE-08 | Web | Playwright regression pack | MM-FE-07, MM-BE-09, MM-BE-05 | Desktop/mobile web critical journey regression and error-state validation. | Done |
| MM-MOB-07.1 | Mobile | Offline mutation queue hardening | MM-MOB-06, MM-BE-05 | Schema-validated offline queue for review/transaction actions. | Done |
| MM-MOB-07.2 | Mobile | Sync recovery engine validation | MM-MOB-07.1 | Background retry/reconciliation behavior with stale conflict handling. | In Review |
| MM-MOB-07.3 | Mobile | Review/projection flow integration tests | MM-MOB-07.2, MM-BE-09 | End-to-end mobile validation for review and projection workflows. | In Progress |
| MM-QA-01 | QA | Cross-surface UAT and defect triage | MM-BE-11, MM-AI-11, MM-FE-08, MM-MOB-07.3 | Unified pass/fail matrix and defect severity disposition. | Not Started |
| MM-QA-02 | QA | Security/config and dependency gate | MM-ASP-07, MM-QA-01 | No unresolved high-severity config/security findings. | Not Started |
| MM-QA-03 | QA | Release rehearsal and rollback drill | MM-QA-01, MM-QA-02 | Rehearsed release with validated rollback path and go/no-go artifact. | Not Started |
| MM-BE-16 | Backend | Plaid Investments Ingestion & API | MM-BE-15 | Schema, ingestion worker, and read-only API for `/investments/holdings/get`. | Done |
| MM-BE-17 | Backend | Plaid Recurring Transactions Ingestion & API | MM-BE-15 | Schema, ingestion worker, and read-only API for `/transactions/recurring/get`. | Done |
| MM-BE-18 | Backend | Net Worth History Aggregation API | MM-BE-15 | API endpoint to aggregate historical balances across all account types. | Done |
| MM-FE-17 | Web | Wire M5 Dashboard UI to Backend APIs | MM-BE-16, MM-BE-17, MM-BE-18 | `page.jsx` fetches real data for Net Worth, Asset Allocation, Recent Transactions, Recurring, and Debt. | Done |

## Verification Matrix
| Area | Validation | Pass Criteria |
|---|---|---|
| Financial correctness | Money/date/property and integration suites | 100% pass with no ledger-truth mutation regressions. |
| AI safety/routing | Labeled routing and fail-closed checks | Routing threshold met; 100% ambiguity-to-review and messaging hard-stop enforcement. |
| Web journeys | Playwright desktop/mobile web | Critical flows pass with stable loading/error states and projection immutability. |
| Mobile reliability | Offline queue, sync recovery, conflicts | Offline actions recover correctly, no duplicate finalization, predictable reconciliation. |
| Orchestration compliance | Policy checks and startup reliability | Service defaults, reference wiring, and JS hosting policies pass with no disallowed patterns. |
| Release operations | Rehearsal + rollback proof | Dry-run release and rollback complete with documented evidence. |

## Release Gates
| Gate | Pass Criteria | Blocker Condition |
|---|---|---|
| G1 Functional completion | All M1-M4 scenarios pass on web/mobile | Any unresolved P0 or P1 defect. |
| G2 Financial correctness | MM-BE-11 matrix passes | Any money/date/review/reimbursement correctness failure. |
| G3 AI safety | MM-AI-11 passes and hard-stop policies enforced | Any `NeedsReview` bypass or outbound send execution path. |
| G4 Orchestration policy | MM-ASP-07 checks pass | Any hardcoded endpoint/connection misuse or policy violation. |
| G5 UX regression | MM-FE-08 and MM-MOB-07 suites pass | Any critical user-journey failure in supported matrix. |
| G6 Release rehearsal | Rollback tested and documented | Rehearsal/rollback not validated. |

## Risks and Mitigations
- Risk: Late cross-surface regressions.
- Mitigation: Daily triage and release freeze on new scope until blockers are resolved.
- Risk: Mobile offline divergence from backend truth.
- Mitigation: Strict queue schema validation, stale conflict handling, and reconciliation tests.
- Risk: AI gate pass in isolation but not in integrated flow.
- Mitigation: Tie `MM-AI-11` evidence to end-to-end scenarios and release gate checks.
- Risk: Orchestration drift during final integration.
- Mitigation: Merge-blocking policy checks plus deterministic startup validation.

## Exit Criteria
M5 exits when all release gates pass, critical defects are closed, and release rehearsal plus rollback evidence is documented and approved.