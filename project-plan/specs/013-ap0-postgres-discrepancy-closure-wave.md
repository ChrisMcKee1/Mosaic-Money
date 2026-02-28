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
| AP0-EPIC | Cross-Surface | PostgreSQL discrepancy closure umbrella | MM-BE-06, MM-AI-12 | Coordinated execution plan and status governance across AP0 backend/AI/web/mobile/ops/QA tracks. | Done |
| AP0-BE-01 | Backend | Taxonomy bootstrap seed and deterministic backfill | AP0-EPIC | Idempotent baseline taxonomy seed + deterministic backfill + before/after discrepancy metrics output. | Done |
| AP0-BE-02 | Backend | Scoped ownership model for user and shared categories | AP0-EPIC | Schema and migration path for `User`, `HouseholdShared`, and `Platform` ownership without breaking existing links. | Done |
| AP0-BE-03 | Backend | Category lifecycle API (CRUD, reorder, reparent, audit) | AP0-BE-02 | Scope-aware category/subcategory API contracts with authorization, ordering safety, and audit trails. | Done |
| AP0-FE-01 | Web | Web Settings categories management experience | AP0-BE-03 | Web settings surface for taxonomy create/edit/delete/reorder/reparent with scope-aware UX states. | Done |
| AP0-MOB-01 | Mobile | Mobile settings category management parity | AP0-BE-03, AP0-FE-01 | Mobile parity for taxonomy management with offline-safe mutation queue and sync reconciliation. | Done |
| AP0-OPS-01 | DevOps/Ops | Internal admin CRUD for platform-managed taxonomy tables | AP0-BE-03 | Operator-only admin lane with protected access, change provenance, and rollback-safe workflows. | Done |
| AP0-AI-01 | AI | Taxonomy readiness gates for ingestion and AI classification fill-rate | AP0-BE-01, AP0-BE-03 | Taxonomy readiness checks and evaluator scenarios improving fill-rates without policy regressions. | Done |
| AP0-QA-01 | QA | AP0 discrepancy closure release gate and evidence pack | AP0-BE-01, AP0-BE-02, AP0-BE-03, AP0-FE-01, AP0-MOB-01, AP0-OPS-01, AP0-AI-01 | SQL/API/web/mobile/AI evidence pack proving discrepancy closure and ownership/auth boundary compliance. | Done |

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

## Update Note (2026-02-27, AP0-BE-01 Kickoff)
- Planner moved `AP0-BE-01` (`#145`) to `In Progress` to begin backend execution for idempotent taxonomy seeding and deterministic backfill.

## Update Note (2026-02-27, AP0-BE-01 Closeout)
- Planner promoted `AP0-BE-01` (`#145`) to `Done` after implementing:
- `TaxonomyBootstrapBackfillService` wired into API startup migration flow.
- source-controlled `SystemTaxonomySeedManifest` with idempotent category/subcategory upsert behavior.
- deterministic backfill that only touches eligible uncategorized expense transactions and routes ambiguous rows fail-closed to `NeedsReview` with explicit reason codes.
- pre/post null-rate metrics snapshot logging for `EnrichedTransactions.SubcategoryId`, `TransactionClassificationOutcomes.ProposedSubcategoryId`, and `ClassificationStageOutputs.ProposedSubcategoryId`.
- Validation evidence:
- `dotnet test src/MosaicMoney.Api.Tests/MosaicMoney.Api.Tests.csproj --filter "TaxonomyBootstrapBackfillServiceTests|AccountAccessPolicyBackfillServiceTests"` (5 passed, 0 failed).
- `dotnet test src/MosaicMoney.Api.Tests/MosaicMoney.Api.Tests.csproj --filter "DeterministicClassificationOrchestratorTests|DeterministicClassificationEngineTests"` (13 passed, 0 failed).

## Update Note (2026-02-28, AP0-BE-02 Closeout)
- Planner promoted `AP0-BE-02` (`#146`) to `Done` after implementing scoped taxonomy ownership across backend model and schema:
- `CategoryOwnerType` with `Platform`, `HouseholdShared`, and `User` ownership lanes.
- new category ownership columns (`OwnerType`, `HouseholdId`, `OwnerUserId`) with check constraints enforcing scope consistency.
- filtered unique indexes by lane to prevent collisions while preserving platform taxonomy compatibility.
- ownership foreign keys to `Households` and `HouseholdUsers` plus navigation wiring.
- EF migration `20260228013646_AddScopedCategoryOwnershipModel` with model snapshot update.
- deterministic read-path updates for category discovery and classification scope filtering (`/search/categories`, deterministic orchestrator, taxonomy backfill platform seed lane).
- Validation evidence:
- `dotnet test src/MosaicMoney.Api.Tests/MosaicMoney.Api.Tests.csproj --filter "FullyQualifiedName~CategoryOwnershipModelContractTests|FullyQualifiedName~TaxonomyBootstrapBackfillServiceTests|FullyQualifiedName~IdentityMembershipModelContractTests"` (8 passed, 0 failed).
- `dotnet test src/MosaicMoney.Api.Tests/MosaicMoney.Api.Tests.csproj --filter "FullyQualifiedName~DeterministicClassificationOrchestratorTests|FullyQualifiedName~DeterministicClassificationEngineTests|FullyQualifiedName~TaxonomyBootstrapBackfillServiceTests"` (15 passed, 0 failed).

## Update Note (2026-02-28, AP0-BE-03 Closeout)
- Planner promoted `AP0-BE-03` (`#152`) to `Done` after implementing scope-aware category/subcategory lifecycle endpoints (`CRUD`, reorder, reparent, archive) plus taxonomy lifecycle audit persistence.
- Backend changes include archive-safe uniqueness/index filtering, audit entity + service wiring, and dedicated lifecycle endpoint coverage under `CategoryLifecycleEndpoints`.
- Validation evidence:
- `dotnet test src/MosaicMoney.Api.Tests/MosaicMoney.Api.Tests.csproj --filter "FullyQualifiedName~CategoryLifecycleEndpointsTests|FullyQualifiedName~CategoryOwnershipModelContractTests"` (10 passed, 0 failed).
- `dotnet test src/MosaicMoney.Api.Tests/MosaicMoney.Api.Tests.csproj --filter "FullyQualifiedName~DeterministicClassificationOrchestratorTests|FullyQualifiedName~DeterministicClassificationEngineTests|FullyQualifiedName~TaxonomyBootstrapBackfillServiceTests"` (15 passed, 0 failed).
- Azure schema application evidence:
- `dotnet ef database update --project src/MosaicMoney.Api/MosaicMoney.Api.csproj --startup-project src/MosaicMoney.Api/MosaicMoney.Api.csproj --connection <azure-mosaicmoneydb-connection>` applied migrations `20260228013646_AddScopedCategoryOwnershipModel` and `20260228015637_AddCategoryLifecycleArchiveAndAudit` to Azure PostgreSQL Flexible Server (`mosaicpostgres-t4s4nroixqd7c`).

## Update Note (2026-02-28, AP0-FE-01 Implementation)
- Planner moved `AP0-FE-01` (`#147`) to `In Review` after implementing the web settings taxonomy management lane at `/settings/categories`.
- Scope-aware UX now supports create/rename/archive/reorder for categories and create/rename/reparent/archive for subcategories in `User` and `HouseholdShared` scopes.
- `Platform` scope is explicitly rendered as read-only in web settings, preserving operator-only mutation boundaries.
- Validation evidence:
- `npm run build` in `src/MosaicMoney.Web` (pass).
- `npx playwright test tests/e2e/settings.spec.js tests/e2e/settings-categories.spec.js` in `src/MosaicMoney.Web` (4 passed, 0 failed).

## Update Note (2026-02-27, AP0-OPS-01 Implementation)
- Planner moved `AP0-OPS-01` (`#149`) to `In Review` after implementing operator-only platform taxonomy mutation controls in the backend lifecycle lane.
- Platform category/subcategory mutations now require both a valid `X-Mosaic-Operator-Key` and an allowlisted authenticated Clerk subject (`TaxonomyOperator:AllowedAuthSubjectsCsv`), preserving fail-closed behavior when operator credentials are absent.
- Change provenance and rollback safety remain enforced through existing taxonomy lifecycle audit entries and archive-first delete semantics.
- Configuration and secret contract updates were applied in AppHost/API placeholders and documented in `docs/agent-context/secrets-and-configuration-playbook.md`.
- Validation evidence:
- `dotnet test src/MosaicMoney.Api.Tests/MosaicMoney.Api.Tests.csproj --filter "FullyQualifiedName~CategoryLifecycleEndpointsTests"` (8 passed, 0 failed), including operator-authorized platform CRUD coverage and existing platform-deny regression checks.

## Update Note (2026-02-28, AP0-AI-01 Implementation)
- Planner moved `AP0-AI-01` (`#150`) to `In Review` after implementing taxonomy readiness gate enforcement for both ingestion and deterministic classification lanes.
- Added `TaxonomyReadinessGateService` with configurable thresholds for subcategory coverage and expense fill-rate sample checks, plus explicit fail-closed reason codes.
- Deterministic orchestrator now short-circuits to `NeedsReview` when readiness fails, persists deterministic stage-output evidence, and blocks semantic/MAF escalation.
- Plaid delta ingestion now evaluates readiness per account household and routes incoming transactions to `NeedsReview` with readiness reason codes when thresholds are not met.
- Validation evidence:
- `dotnet test src/MosaicMoney.Api.Tests/MosaicMoney.Api.Tests.csproj --filter FullyQualifiedName~DeterministicClassificationOrchestratorTests` (9 passed, 0 failed).
- `dotnet test src/MosaicMoney.Api.Tests/MosaicMoney.Api.Tests.csproj --filter FullyQualifiedName~PlaidDeltaIngestionServiceTests` (7 passed, 0 failed).
- `dotnet test src/MosaicMoney.Api.Tests/MosaicMoney.Api.Tests.csproj --filter FullyQualifiedName~TaxonomyReadinessGateServiceTests` (3 passed, 0 failed).

## Update Note (2026-02-28, AP0-FE-01/AP0-OPS-01/AP0-AI-01 Done Promotion)
- Planner promoted `AP0-FE-01` (`#147`), `AP0-OPS-01` (`#149`), and `AP0-AI-01` (`#150`) to `Done` after hardening review and regression validation.
- FE hardening added subcategory reorder support fallback and subcategory business-expense edit support in settings category actions/UI, plus stabilized category settings e2e coverage.
- OPS hardening tightened platform operator gate checks (single operator header enforcement, fixed-length key-hash compare, explicit empty-allowlist deny) and expanded denial/success coverage for platform reorder + subcategory lifecycle mutation paths.
- AI hardening fail-closed unsupported readiness lanes and compares raw fill-rate against thresholds to avoid rounded pass-through edge cases; ingestion readiness gate injection is now required at construction sites with explicit allow-all stubs in tests.
- Validation evidence:
- `dotnet test src/MosaicMoney.Api.Tests/MosaicMoney.Api.Tests.csproj --filter FullyQualifiedName~CategoryLifecycleEndpointsTests` (12 passed, 0 failed).
- `dotnet test src/MosaicMoney.Api.Tests/MosaicMoney.Api.Tests.csproj --filter FullyQualifiedName~TaxonomyReadinessGateServiceTests` (6 passed, 0 failed).
- `dotnet test src/MosaicMoney.Api.Tests/MosaicMoney.Api.Tests.csproj --filter FullyQualifiedName~PlaidDeltaIngestionServiceTests` (7 passed, 0 failed).
- `dotnet test src/MosaicMoney.Api.Tests/MosaicMoney.Api.Tests.csproj --filter FullyQualifiedName~PlaidTransactionsSyncProcessorTests` (3 passed, 0 failed).
- `dotnet test src/MosaicMoney.Api.Tests/MosaicMoney.Api.Tests.csproj --filter FullyQualifiedName~TransactionEmbeddingQueuePipelineTests` (4 passed, 0 failed).
- `npm run build` and `npx playwright test tests/e2e/settings-categories.spec.js` in `src/MosaicMoney.Web` (pass).

## Update Note (2026-02-28, AP0-MOB-01 Done Promotion)
- Planner promoted `AP0-MOB-01` (`#148`) to `Done` after replacing placeholder mobile category settings behavior with backend-parity taxonomy lifecycle flows and offline replay safety.
- Mobile now supports category/subcategory create, rename, archive, reorder, business-expense flag edit, and reparent actions in `User` and `HouseholdShared` scopes with explicit `Platform` read-only enforcement.
- Category mutation queue/recovery now stores backend-ready HTTP request envelopes, applies backoff retries for transient failures, records reconciliation notices for stale/non-retriable conflicts, and replays in app-state recovery loops.
- Validation evidence:
- `npm run typecheck` in `src/MosaicMoney.Mobile` (pass).
- `npx vitest run src/features/settings/offline/categoryMutationQueue.test.ts src/features/settings/offline/categoryMutationRecovery.test.ts` in `src/MosaicMoney.Mobile` (8 passed, 0 failed).

## Update Note (2026-02-28, AP0-QA-01 and AP0-EPIC Done Promotion)
- Planner promoted `AP0-QA-01` (`#151`) and `AP0-EPIC` (`#144`) to `Done` after producing the AP0 evidence pack at `artifacts/release-gates/ap0-qa-01/ap0-qa-01-evidence.md` and verifying all AP0 child tracks are complete.
- Cross-surface validation evidence recorded for backend/AI, web, and mobile lanes:
- `dotnet test src/MosaicMoney.Api.Tests/MosaicMoney.Api.Tests.csproj --filter "FullyQualifiedName~CategoryLifecycleEndpointsTests|FullyQualifiedName~TaxonomyReadinessGateServiceTests|FullyQualifiedName~PlaidDeltaIngestionServiceTests|FullyQualifiedName~PlaidTransactionsSyncProcessorTests|FullyQualifiedName~TransactionEmbeddingQueuePipelineTests"` (32 passed, 0 failed).
- `npm run build` and `npx playwright test tests/e2e/settings-categories.spec.js` in `src/MosaicMoney.Web` (6 passed, 0 failed; expected non-fatal API base URL warning during static build data collection).
- `npm run typecheck` and `npx vitest run src/features/settings/offline/categoryMutationQueue.test.ts src/features/settings/offline/categoryMutationRecovery.test.ts` in `src/MosaicMoney.Mobile` (8 passed, 0 failed).
