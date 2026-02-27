# Architecture Agentic Context

This file is the planner-friendly architecture grounding summary.
Canonical source: [Full Architecture](../../project-plan/architecture.md)

Detailed architecture docs:
- [Architecture Docs Index](../architecture/README.md)
- [System Topology](../architecture/system-topology.md)
- [AI Orchestration Flow](../architecture/ai-orchestration-flow.md)
- [Deployment Modes](../architecture/deployment-modes.md)
- [Data Models Index](../data-models/README.md)

## Core Stack
- Backend: C# 14 with .NET 10 Minimal APIs.
- Orchestration: Aspire 13.3 preview AppHost with API, worker, and frontend composition.
- Orchestration database mode: use `AddAzurePostgresFlexibleServer` as the canonical Postgres resource; local full-stack can run via `.RunAsContainer()` and DB-only Azure rollout uses `src/apphost.database/apphost.cs`.
- Web: Next.js 16 with React 19 and Tailwind CSS.
- Mobile: React Native via Expo SDK 55 for mobile apps, with iPhone-first MVP release focus and Windows dev host + physical phone testing workflow.
- Dashboard/reporting visualization standard: web uses `react-apexcharts`; mobile uses `victory-native-xl`; avoid net-new `recharts` for new work.
- Data: PostgreSQL 18 with `azure_ai` extension and `pgvector`.
- Authentication: Clerk for web/mobile sign-in and session management, with API-side JWT validation and deny-by-default authorization policies.
- Azure PostgreSQL baseline from current Aspire publish: PostgreSQL 16, Burstable `Standard_B1ms`, 32 GB storage, HA disabled; tune via `ConfigureInfrastructure(...)` for production.
- Naming convention for PostgreSQL resources: server resource name `mosaic-postgres`, database/connection name `mosaicmoneydb`, and secret parameter keys prefixed with `mosaic-postgres-*`.
- AI: Microsoft Agent Framework 1.0 RC plus `Microsoft.Extensions.AI`.

## Architectural Rules
- Keep Copilot as UI and coding assistant, not runtime orchestration.
- Keep deterministic and in-database AI paths primary for cost and latency control.
- Treat API as resource server: validate Clerk JWTs, map `sub` to Mosaic identity, and enforce protected endpoints via authorization policy.
- Escalation order is deterministic -> semantic retrieval -> MAF fallback.
- For Azure rollout requiring only database provisioning, deploy `src/apphost.database/apphost.cs` with Aspire CLI because `aspire deploy` does not currently provide per-resource filtering.
- DB-only rollout recommendation: run `aspire do provision-mosaic-postgres-kv` first, tag the vault (`mosaic=true`, `workload=mosaic-money`), then continue database provisioning.
- Route low-confidence cases into `NeedsReview` instead of forced auto-resolution.
- Preserve source-of-truth ledger integrity. Projection logic stays in presentation layers.
- Aggregate/shape time-series buckets (day/week/month) before chart render boundaries to keep chart components presentation-only.

## Team Routing Map
- `mosaic-money-backend`: API, entities, migrations, ingestion.
- `mosaic-money-frontend`: web dashboards, projection UX, data visuals.
- `mosaic-money-mobile`: Expo flows and shared package integration.
- `mosaic-money-ai`: retrieval, classification confidence routing, HITL guardrails.
- `mosaic-money-devops`: Aspire AppHost, `AddJavaScriptApp`, containers, and MCP diagnostics.
