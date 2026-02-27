# Spec 013: AP0 PostgreSQL Discrepancy Closure Wave

## Status
- Drafted: 2026-02-27
- Wave: AP0
- GitHub tracking umbrella: `#144`
- GitHub task issues: `#145`, `#146`, `#147`, `#148`, `#149`, `#150`, `#151`, `#152`
- Depends on:
- `project-plan/specs/001-mvp-foundation-task-breakdown.md`
- `project-plan/specs/003-m2-ledger-truth-and-review-core.md`
- `project-plan/specs/005-m4-ai-escalation-pipeline-deterministic-semantic-maf.md`
- `project-plan/specs/009-m8-authentication-and-authorization-clerk.md`
- `docs/agent-context/postgres-data-audit-2026-02-27.md`

## Objective
Close PostgreSQL taxonomy and linkage discrepancies by shipping deterministic bootstrap + scoped category model + lifecycle APIs + web/mobile/admin management lanes + AI readiness gates + QA evidence pack.

## Guardrails
- Preserve single-entry ledger semantics and never rewrite raw ledger truth for projections.
- Keep ambiguous classification outcomes fail-closed to `NeedsReview`.
- Enforce household and ownership boundaries for every taxonomy mutation path.
- No autonomous external messaging or high-impact action bypass.

## Task Breakdown
| ID | Domain | Task | Dependencies | Deliverable | Status |
|---|---|---|---|---|---|
| AP0-EPIC | Cross-Surface | PostgreSQL discrepancy closure umbrella | MM-BE-06, MM-AI-12 | Coordinated execution plan and status governance across AP0 backend/AI/web/mobile/ops/QA tracks. | In Progress |
| AP0-BE-01 | Backend | Taxonomy bootstrap seed and deterministic backfill | AP0-EPIC | Idempotent baseline taxonomy seed + deterministic backfill + before/after discrepancy metrics output. | Not Started |
| AP0-BE-02 | Backend | Scoped ownership model for user and shared categories | AP0-EPIC | Schema and migration path for `User`, `HouseholdShared`, and `Platform` ownership without breaking existing links. | Not Started |
| AP0-BE-03 | Backend | Category lifecycle API (CRUD, reorder, reparent, audit) | AP0-BE-02 | Scope-aware category/subcategory API contracts with authorization, ordering safety, and audit trails. | Not Started |
| AP0-FE-01 | Web | Web Settings categories management experience | AP0-BE-03 | Web settings surface for taxonomy create/edit/delete/reorder/reparent with scope-aware UX states. | Not Started |
| AP0-MOB-01 | Mobile | Mobile settings category management parity | AP0-BE-03, AP0-FE-01 | Mobile parity for taxonomy management with offline-safe mutation queue and sync reconciliation. | Not Started |
| AP0-OPS-01 | DevOps/Ops | Internal admin CRUD for platform-managed taxonomy tables | AP0-BE-03 | Operator-only admin lane with protected access, change provenance, and rollback-safe workflows. | Not Started |
| AP0-AI-01 | AI | Taxonomy readiness gates for ingestion and AI classification fill-rate | AP0-BE-01, AP0-BE-03 | Taxonomy readiness checks and evaluator scenarios improving fill-rates without policy regressions. | Not Started |
| AP0-QA-01 | QA | AP0 discrepancy closure release gate and evidence pack | AP0-BE-01, AP0-BE-02, AP0-BE-03, AP0-FE-01, AP0-MOB-01, AP0-OPS-01, AP0-AI-01 | SQL/API/web/mobile/AI evidence pack proving discrepancy closure and ownership/auth boundary compliance. | Not Started |

## Acceptance Criteria
- `Categories` and `Subcategories` are populated and remain idempotent under reruns.
- Category assignment null-rates improve on validated benchmark datasets with explicit thresholds.
- Category management flows exist for web/mobile and operator lanes with authorization guardrails.
- API lifecycle contracts support safe CRUD/reorder/reparent operations with auditability.
- QA release gate artifacts demonstrate discrepancy closure and no regression to fail-closed behavior.

## Update Note (2026-02-27)
- Planner synchronized AP0 issues already on Project 1 into spec governance.
- `AP0-EPIC` (`#144`) is promoted to `In Progress` for active orchestration.
- Child AP0 tasks remain `Not Started` pending implementation sequencing after current M10 closeout.
