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
| AP0-BE-01 | Backend | Taxonomy bootstrap seed and deterministic backfill | AP0-EPIC | Idempotent baseline taxonomy seed + deterministic backfill + before/after discrepancy metrics output. | Done |
| AP0-BE-02 | Backend | Scoped ownership model for user and shared categories | AP0-EPIC | Schema and migration path for `User`, `HouseholdShared`, and `Platform` ownership without breaking existing links. | Done |
| AP0-BE-03 | Backend | Category lifecycle API (CRUD, reorder, reparent, audit) | AP0-BE-02 | Scope-aware category/subcategory API contracts with authorization, ordering safety, and audit trails. | Done |
| AP0-FE-01 | Web | Web Settings categories management experience | AP0-BE-03 | Web settings surface for taxonomy create/edit/delete/reorder/reparent with scope-aware UX states. | In Review |
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
