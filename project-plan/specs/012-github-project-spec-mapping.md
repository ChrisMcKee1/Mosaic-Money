# Spec 012: GitHub Project and Spec Mapping

## Status
- Drafted: 2026-02-27
- Scope: canonical mapping between Mosaic Money GitHub Project work items and in-repo milestone specs.

## Project Metadata
- Owner: `ChrisMcKee1`
- Repository: `Mosaic-Money`
- Project: `Mosaic-Money` (`https://github.com/users/ChrisMcKee1/projects/1`)
- Project node ID: `PVT_kwHOAYj6Kc4BP962`

## Mapping Rules
- Every tracked work item must exist in both:
- an in-repo milestone spec task table, and
- a GitHub issue on Project 1.
- Task IDs in spec tables are the source of truth and must match issue titles.
- Parent/sub-issue relationships must represent milestone epic -> task decomposition.
- Status updates are dual-write: spec tables first, then Project 1 status field update.
- Issue labels must not carry status state. Status is tracked only via the Project 1 `Status` field.

## Milestone-to-Spec Mapping
| Milestone | Spec File | Primary Task ID Range | GitHub Milestone |
|---|---|---|---|
| M1 | `project-plan/specs/002-m1-platform-and-contract-foundation.md` | `MM-ASP-01` to `MM-MOB-01` | `M1: Platform & Contract Foundation` |
| M2 | `project-plan/specs/003-m2-ledger-truth-and-review-core.md` | `MM-BE-05` to `MM-MOB-08` | `M2: Ledger Truth & Review Core` |
| M3 | `project-plan/specs/004-m3-ingestion-recurring-reimbursements-projections.md` | `MM-BE-07` to `MM-MOB-06` | `M3: Ingestion, Recurring, Reimbursements & Projections` |
| M4 | `project-plan/specs/005-m4-ai-escalation-pipeline-deterministic-semantic-maf.md` | `MM-AI-03` to `MM-AI-10` | `M4: AI Escalation Pipeline` |
| M5 | `project-plan/specs/006-m5-ux-completion-and-release-gates.md` | `MM-ASP-05` to `MM-QA-03` and `MM-BE-16..18`, `MM-FE-17` | `M5: UX Completion & Release Gates` |
| M6 | `project-plan/specs/007-m6-ui-redesign-and-theming.md` | `MM-FE-10` to `MM-MOB-09` | Project-only tracking |
| M7 | `project-plan/specs/008-m7-identity-household-access-and-account-ownership.md` | `MM-BE-19` to `MM-MOB-12` | Project-only tracking |
| M8 | `project-plan/specs/009-m8-authentication-and-authorization-clerk.md` | `MM-ASP-10` to `MM-QA-04` | Project-only tracking |
| M9 | `project-plan/specs/010-m9-cross-surface-charting-framework-migration.md` | `MM-FE-25` to `MM-QA-05` and `MM-MOB-GAP-01` | Project-only tracking |
| M10 | `project-plan/specs/011-m10-runtime-agentic-orchestration-and-conversational-assistant.md` | `MM-ASP-12` to `MM-QA-06` | `M10: Runtime Agentic Orchestration` |
| AP0 | `project-plan/specs/013-ap0-postgres-discrepancy-closure-wave.md` | `AP0-EPIC` and `AP0-BE/FE/MOB/OPS/AI/QA` tasks | Project-only tracking |

## M10 GitHub Work Item Plan
| Work Item | Issue # | Issue Title Pattern | Labels |
|---|---|---|---|
| Epic | #126 | `[M10-EPIC] Runtime multi-agent orchestration and conversational assistant` | `domain:ai`, `touch:ai`, `touch:api`, `touch:web`, `touch:mobile`, `touch:infrastructure` |
| MM-ASP-12 | #127 | `[MM-ASP-12] Runtime messaging backbone in AppHost` | `domain:devops`, `touch:infrastructure`, `touch:api` |
| MM-BE-28 | #128 | `[MM-BE-28] Worker-owned orchestration command handlers` | `domain:backend`, `touch:api`, `touch:database`, `touch:infrastructure` |
| MM-BE-27 | #132 | `[MM-BE-27] Agent workflow lifecycle schema` | `domain:backend`, `touch:api`, `touch:database` |
| MM-FE-27 | #134 | `[MM-FE-27] Assistant shell and approval card UX` | `domain:web`, `touch:web`, `touch:api`, `touch:ai` |
| MM-AI-14 | #137 | `[MM-AI-14] Conversational orchestrator workflow contracts` | `domain:ai`, `touch:ai`, `touch:api`, `touch:web`, `touch:mobile` |
| MM-ASP-13 | #138 | `[MM-ASP-13] Worker orchestration runbooks and diagnostics` | `domain:devops`, `touch:infrastructure`, `touch:api` |
| MM-AI-13 | #139 | `[MM-AI-13] Specialist agent registry and routing policy` | `domain:ai`, `touch:ai`, `touch:api` |
| MM-AI-15 | #140 | `[MM-AI-15] Specialist evaluator packs and replay artifacts` | `domain:ai`, `domain:qa`, `touch:ai`, `touch:api` |
| MM-FE-28 | #141 | `[MM-FE-28] Agent provenance and explainability timeline` | `domain:web`, `touch:web`, `touch:api`, `touch:ai` |
| MM-MOB-16 | #142 | `[MM-MOB-16] Assistant parity with offline-safe queue` | `domain:mobile`, `touch:mobile`, `touch:api`, `touch:ai` |
| MM-QA-06 | #143 | `[MM-QA-06] Multi-agent runtime release gate` | `domain:qa`, `touch:web`, `touch:mobile`, `touch:api`, `touch:ai`, `touch:infrastructure` |

## AP0 GitHub Work Item Plan
| Work Item | Issue # | Issue Title Pattern | Labels |
|---|---|---|---|
| AP0-EPIC | #144 | `AP0: PostgreSQL data discrepancies audit follow-up ...` | `domain:backend`, `touch:database`, `touch:api`, `touch:web`, `touch:mobile`, `touch:ai`, `touch:infrastructure` |
| AP0-BE-01 | #145 | `[AP0-BE-01] Taxonomy bootstrap seed and deterministic backfill` | `domain:backend`, `touch:database`, `touch:api` |
| AP0-BE-02 | #146 | `[AP0-BE-02] Scoped ownership model for user and shared categories` | `domain:backend`, `touch:database`, `touch:api` |
| AP0-FE-01 | #147 | `[AP0-FE-01] Web Settings categories management experience` | `domain:web`, `touch:web`, `touch:api` |
| AP0-MOB-01 | #148 | `[AP0-MOB-01] Mobile settings category management parity` | `domain:mobile`, `touch:mobile`, `touch:api` |
| AP0-OPS-01 | #149 | `[AP0-OPS-01] Internal admin CRUD for platform-managed taxonomy tables` | `domain:devops`, `touch:infrastructure`, `touch:api`, `touch:database` |
| AP0-AI-01 | #150 | `[AP0-AI-01] Taxonomy readiness gates for ingestion and AI classification fill-rate` | `domain:ai`, `touch:ai`, `touch:api`, `touch:database` |
| AP0-QA-01 | #151 | `[AP0-QA-01] AP0 discrepancy closure release gate and evidence pack` | `domain:qa`, `touch:api`, `touch:web`, `touch:mobile`, `touch:ai`, `touch:database` |
| AP0-BE-03 | #152 | `[AP0-BE-03] Category lifecycle API (CRUD, reorder, reparent, audit)` | `domain:backend`, `touch:api`, `touch:database`, `touch:infrastructure` |

## Synchronization Procedure
1. Create or update spec tables first.
2. Create or update corresponding GitHub issues with matching task IDs.
3. Add issues to Project 1 and set `Status` to match the spec value.
4. Keep `.github/scripts/sync-project-board.ps1` aligned with any new issue IDs and statuses.
