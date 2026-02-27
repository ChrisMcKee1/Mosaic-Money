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
- **UI Data Gap Analysis (2026-02-24, resolved 2026-02-25)**:
  - **Net Worth History**: addressed by `MM-BE-18` and wired in `MM-FE-17`.
  - **Asset Allocation**: addressed by `MM-BE-16` and wired in `MM-FE-17`.
  - **Recurring Subscriptions**: addressed by `MM-BE-17` and wired in `MM-FE-17`.
  - **Debt / Liabilities + Recent Transactions**: wired for dashboard usage in `MM-FE-17` using current backend API surface; future expansion remains possible if additional liability-specific contract depth is required.

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
| MM-ASP-06 | DevOps | Dashboard + MCP diagnostics flow | MM-ASP-05, MM-AI-10 | Standardized diagnostics for API, Worker, Web, and AI workflow traces. | Done |
| MM-ASP-07 | DevOps | Orchestration policy gate checks | MM-ASP-03, MM-ASP-04, MM-ASP-06 | Automated checks for disallowed patterns and missing orchestration conventions. | Done |
| MM-BE-11 | Backend | Financial correctness regression suite | MM-BE-10 | Money/date/matching/review/reimbursement/AI-routing regression matrix with pass gates. | Done |
| MM-AI-11 | AI | Agentic eval release gate | MM-AI-10 | Thresholded routing correctness and safety report for release blocking decisions. | Done |
| MM-AI-12 | AI | Official evaluator stack adoption + research replay pack | MM-AI-11 | Integrate official `.NET` + Foundry evaluators/graders with source-linked replay instructions and reproducible evidence artifacts. | In Progress |
| MM-FE-08 | Web | Playwright regression pack | MM-FE-07, MM-BE-09, MM-BE-05 | Desktop/mobile web critical journey regression and error-state validation. | Done |
| MM-MOB-07.1 | Mobile | Offline mutation queue hardening | MM-MOB-06, MM-BE-05 | Schema-validated offline queue for review/transaction actions. | Done |
| MM-MOB-07.2 | Mobile | Sync recovery engine validation | MM-MOB-07.1 | Background retry/reconciliation behavior with stale conflict handling. | Done |
| MM-MOB-07.3 | Mobile | Review/projection flow integration tests | MM-MOB-07.2, MM-BE-09 | End-to-end mobile validation for review and projection workflows. | Done |
| MM-QA-01 | QA | Cross-surface UAT and defect triage | MM-BE-11, MM-AI-11, MM-FE-08, MM-MOB-07.3 | Unified pass/fail matrix and defect severity disposition. | Blocked |
| MM-QA-02 | QA | Security/config and dependency gate | MM-ASP-07, MM-QA-01 | No unresolved high-severity config/security findings. | Done |
| MM-QA-03 | QA | Release rehearsal and rollback drill | MM-QA-01, MM-QA-02 | Rehearsed release with validated rollback path and go/no-go artifact. | Done |
| MM-BE-16 | Backend | Plaid Investments Ingestion & API | MM-BE-15 | Schema, ingestion worker, and read-only API for `/investments/holdings/get`. | Done |
| MM-BE-17 | Backend | Plaid Recurring Transactions Ingestion & API | MM-BE-15 | Schema, ingestion worker, and read-only API for `/transactions/recurring/get`. | Done |
| MM-BE-18 | Backend | Net Worth History Aggregation API | MM-BE-15 | API endpoint to aggregate historical balances across all account types. | Done |
| MM-FE-17 | Web | Wire M5 Dashboard UI to Backend APIs | MM-BE-16, MM-BE-17, MM-BE-18 | `page.jsx` fetches real data for Net Worth, Asset Allocation, Recent Transactions, Recurring, and Debt. | Done |

Implementation note (2026-02-24): `MM-AI-11` now has a concrete release-blocker evaluation gate in `src/MosaicMoney.Api.Tests/AgenticEvalReleaseGate.cs` with executable checks in `src/MosaicMoney.Api.Tests/AgenticEvalReleaseGateTests.cs`.
- Routing correctness threshold: `>= 95%` labeled staged-routing scenarios.
- Ambiguity fail-closed threshold: `100%` route-to-`NeedsReview` compliance.
- External messaging hard-stop threshold: `100%` deny for outbound `send_*`/`notify_external_system` actions while preserving draft-only behavior.
- Explainability threshold: `>= 95%` `AgentNote` summary policy compliance (concise, bounded, transcript-safe).
- Evidence command (tests only): `dotnet test src/MosaicMoney.Api.Tests/MosaicMoney.Api.Tests.csproj --filter "FullyQualifiedName~AgenticEvalReleaseGateTests"`.
- Evidence command (planner-ready artifact): `pwsh .github/scripts/run-mm-ai-11-release-gate.ps1`.
- Artifact output: `artifacts/release-gates/mm-ai-11/latest.json` with `result` (`GO` or `NO_GO`), release-ready boolean, and per-criterion scores/evidence.
- Planner review completed: `MM-AI-11` promoted to `Done` after focused verification passed.

Implementation note (2026-02-24): `MM-AI-12` is added as a follow-on modernization task to adopt official evaluator frameworks (`Microsoft.Extensions.AI.Evaluation` and Azure AI Foundry evaluator/graders) with a source-linked replay pack in `project-plan/specs/005-m4-ai-escalation-pipeline-deterministic-semantic-maf.md`.

Implementation note (2026-02-25): `MM-AI-12` is now `In Review` and extends the existing release-gate workflow with official evaluator stack replay outputs.
- Script update: `.github/scripts/run-mm-ai-11-release-gate.ps1` now also writes an official evaluator replay artifact (`-OfficialEvaluatorOutputPath`, default `artifacts/release-gates/mm-ai-12/latest.json`).
- Evidence output includes source-linked evaluator references, criterion-to-evaluator mappings, dataset field mappings, and explicit fail-closed readiness when cloud evaluator prerequisites are absent.
- Focused validation is tracked in `src/MosaicMoney.Api.Tests/AgenticEvalOfficialEvaluatorStackTests.cs` and `src/MosaicMoney.Api.Tests/AgenticEvalReleaseGateTests.cs`.

Implementation note (2026-02-27): Daily kickoff status reconciliation returned `MM-AI-12` to `In Progress` for completion follow-through. Specialist audit evidence shows missing replay-pack artifacts (`artifacts/release-gates/mm-ai-12/criteria-dataset-mapping.json`, `artifacts/release-gates/mm-ai-12/replay-pack.md`) and no cloud evaluator execution evidence in latest outputs. Task remains open until these are generated and validated.

Implementation note (2026-02-24): Planner review promoted `MM-BE-11` to `Done` after full backend regression execution passed (`dotnet test src/MosaicMoney.Api.Tests/MosaicMoney.Api.Tests.csproj`: 136 passed, 0 failed).

Implementation note (2026-02-24): Planner review promoted `MM-MOB-07.2` to `Done` after sync-recovery validation passed (`cd src/MosaicMoney.Mobile; npm run test:sync-recovery`: 4 passed, 0 failed).

Implementation note (2026-02-24): Planner review promoted `MM-MOB-07.3` to `Done` after review/projection integration validation passed on mobile (`cd src/MosaicMoney.Mobile; npm run test:sync-recovery`: 4 passed, 0 failed; `cd src/MosaicMoney.Mobile; npm run test:review-projection`: 2 passed, 0 failed).

Implementation note (2026-02-25): `MM-QA-01` triage matrix execution completed and moved to `In Review`.
- Pass: Aspire startup gate (`dotnet build src/apphost.cs`, detached run, wait for API/worker/web), orchestration policy gate (`npm run policy:orchestration`), API regression suite (`dotnet test src/MosaicMoney.Api.Tests/MosaicMoney.Api.Tests.csproj`), AI release-gate script (`pwsh .github/scripts/run-mm-ai-11-release-gate.ps1`), and mobile key checks (`npm run typecheck`, `npm run test:sync-recovery`, `npm run test:review-projection`).
- Failures captured for triage: web Playwright regression pack (`cd src/MosaicMoney.Web; npm run test:e2e`) and API runtime stability under diagnostics where `api` transitioned to `Finished` with database timeout symptoms during migration startup.
- Priority defects identified: `P1` web selector/navigation contract failures and `P1` API startup DB connectivity instability; `P2` AppHost/web lock contention during Playwright execution.

Implementation note (2026-02-24): Planner review promoted `MM-ASP-06` to `Done` after standardizing diagnostics workflow on detached non-isolated startup (`aspire run --project src/apphost.cs --detach`) and validating trace/log capture (`aspire telemetry traces --project src/apphost.cs --limit <n>` plus resource-filtered API traces). Current CLI nuance is documented: `aspire telemetry traces web ...` may return `Resource 'web' not found` for JavaScript executable resources, so cross-resource trace capture uses unfiltered traces command.

Implementation note (2026-02-26): `MM-QA-02` is now `Blocked` and `MM-QA-03` is blocked by dependency after running release gates. Evidence:
- Security/config/dependency artifact: `artifacts/release-gates/mm-qa-02/latest.md`
- Rehearsal/rollback artifact: `artifacts/release-gates/mm-qa-03/latest.md`
- Blocking finding: `src/MosaicMoney.Web` dependency audit reports critical `next` advisories (`npm audit --audit-level=high --omit=dev` exit code 1).

Implementation note (2026-02-26): Planner reran QA gates after remediation and promoted both QA tasks to `Done`.
- Dependency remediation: upgraded `src/MosaicMoney.Web` dependency `next` to `16.1.6` and re-ran `npm audit --audit-level=high --omit=dev` with `0 vulnerabilities`.
- QA-02 evidence refresh: `artifacts/release-gates/mm-qa-02/latest.md`.
- QA-03 evidence refresh: full stop/build/start/wait/resources release rehearsal and rollback restart sequence passed again with healthy API/worker/web resources (`artifacts/release-gates/mm-qa-03/latest.md`).

Implementation note (2026-02-26): `MM-QA-01` is moved to `Blocked` after latest rerun because full web Playwright regression still reports unresolved failures (dashboard/navigation/needs-review selector and interaction contract mismatches), and orchestration policy gate currently errors on wildcard path handling in `.github/scripts/test-orchestration-policy-gates.ps1`.

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