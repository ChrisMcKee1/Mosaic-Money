# Spec 002: M1 Platform and Contract Foundation

## Status
- Drafted: 2026-02-20
- Milestone: M1
- Depends on: `project-plan/specs/001-mvp-foundation-task-breakdown.md`

## Objective
Stand up the baseline distributed architecture and core contracts so all downstream implementation can proceed without rework.

## In Scope
- Aspire topology and service composition.
- JavaScript app hosting integration via Aspire 13+ APIs.
- Reference-driven service wiring (`WithReference(...)`) and discovery.
- Service defaults and health endpoint conventions.
- Backend baseline wiring for PostgreSQL resources.
- Initial domain contract shape for transactions, review, recurring, and reimbursement APIs.

## Out of Scope
- Plaid ingestion logic.
- Recurring matching behavior.
- Semantic embeddings and MAF fallback logic.
- Full dashboard and mobile workflows.

## Guardrails
- No hardcoded cross-service URLs or connection strings.
- AppHost uses `Aspire.Hosting.*` packages only.
- Service projects use Aspire client integration packages.
- No `AddNpmApp` usage.
- Single-entry ledger assumptions in API contracts.

## Task Breakdown
| ID | Task | Dependencies | Deliverable | Status |
|---|---|---|---|---|
| MM-ASP-01 | Bootstrap Aspire topology skeleton | None | AppHost, ServiceDefaults, API, Worker, Web resources build and run. | Done |
| MM-ASP-02 | Configure JS hosting API in AppHost | MM-ASP-01 | `AddJavaScriptApp` or equivalent modern API with external endpoint exposure. | Done |
| MM-ASP-03 | Add explicit `WithReference(...)` graph | MM-ASP-01, MM-ASP-02 | API, Worker, and Web dependencies wired by reference-driven discovery. | Done |
| MM-ASP-04 | Enforce service defaults and health endpoints | MM-ASP-03 | `.NET` services apply `AddServiceDefaults()` and API maps default endpoints. | Done |
| MM-BE-01 | Backend skeleton and Aspire DB wiring | MM-ASP-03, MM-ASP-04 | API/Worker wired to PostgreSQL by connection name via Aspire integrations. | Done |
| MM-BE-02 | Ledger domain model baseline | MM-BE-01 | Core single-entry entities with `UserNote` and `AgentNote` fields separated. | In Progress |
| MM-BE-03 | PostgreSQL schema and extension migration | MM-BE-02 | EF migration enables `pgvector` and `azure_ai` with required indexes. | Not Started |
| MM-BE-04 | Minimal API contract v1 | MM-BE-02 | Resource endpoints/DTOs and validation behavior for MVP surface. | Not Started |
| MM-AI-01 | Classification outcome contract | MM-BE-04 | Stage, confidence, rationale, and review routing structure (no transcript storage). | Not Started |
| MM-AI-02 | AI workflow integration checks | MM-BE-01, MM-BE-03 | AI paths conform to same DB integration and orchestration constraints. | Not Started |
| MM-FE-01 | Next.js App Router foundation | MM-ASP-02 | Web shell foundation with responsive and accessible primitives. | Done |
| MM-FE-02 | Server-side API fetch layer | MM-FE-01, MM-ASP-03 | Server boundary fetch utility uses injected service references only. | Done |
| MM-FE-03 | Responsive app shell and navigation | MM-FE-01 | Initial route shell for Dashboard, Transactions, NeedsReview. | In Progress |
| MM-MOB-01 | Shared domain contracts and API client | MM-BE-04 | Mobile consumes shared schema contracts aligned to backend payloads. | Not Started |

## Subagent Alignment and Handoffs (2026-02-22)

### Active now
| Agent | Task IDs | Start Condition | Handoff Condition |
|---|---|---|---|
| `mosaic-money-devops` | MM-ASP-01 | None | AppHost composes baseline resources (ServiceDefaults, API, Worker, Web) and builds/runs under .NET 10 + JS app stack. |

### Queued by dependency
| Agent | Task IDs | Unblock Condition | Verification Focus |
|---|---|---|---|
| `mosaic-money-devops` | MM-ASP-02, MM-ASP-03, MM-ASP-04 | MM-ASP-01 complete | `AddJavaScriptApp`/`AddViteApp`/`AddNodeApp` usage, `WithReference(...)` wiring, service defaults and health endpoints. |
| `mosaic-money-backend` | MM-BE-01, MM-BE-02, MM-BE-03, MM-BE-04 | MM-ASP-03 and MM-ASP-04 complete | Aspire-native PostgreSQL integration, single-entry entities, `UserNote`/`AgentNote`, migration extensions and API validation envelope. |
| `mosaic-money-ai` | MM-AI-01, MM-AI-02 | MM-BE-01, MM-BE-03, MM-BE-04 complete | Classification outcome contract, confidence/rationale persistence, orchestration-aligned DB access. |
| `mosaic-money-frontend` | MM-FE-01, MM-FE-02, MM-FE-03 | MM-ASP-02 complete for FE-01/03, MM-ASP-03 complete for FE-02 | App Router shell, server-boundary fetch layer, no hardcoded localhost service URLs. |
| `mosaic-money-mobile` | MM-MOB-01 | MM-BE-04 complete | Shared payload contracts and API client alignment without duplicating financial logic. |

## Acceptance Criteria
- AppHost can run and display all baseline resources with healthy startup.
- API and Worker resolve PostgreSQL without literal connection strings.
- API contract includes `UserNote`, `AgentNote`, `NeedsReview` fields/states, and validation envelope.
- Web and mobile clients can consume stubbed backend contract shapes.

## Verification
- Build and startup verification for AppHost and all registered services.
- Static scan for disallowed patterns (`AddNpmApp`, hardcoded localhost service URLs).
- API contract tests for required fields and status handling.

## Risks and Mitigations
- Risk: Early drift in connection strategy.
- Mitigation: Fail PRs that bypass `WithReference(...)` and service discovery.
- Risk: Contract drift between API, Web, and Mobile.
- Mitigation: Shared schema package and contract-first checks.

## Exit Criteria
M1 exits when baseline orchestration, DB wiring, domain model, and API/web/mobile contract foundations are in place and verified.