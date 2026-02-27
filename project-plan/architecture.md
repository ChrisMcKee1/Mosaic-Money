# Mosaic Money Architecture

## 1. Scope
This file is the canonical architecture index and decision summary. Detailed diagrams and model artifacts are now split into dedicated docs:

- `docs/architecture/README.md`
- `docs/architecture/master-platform-architecture.md`
- `docs/architecture/architecture-decisions.md`
- `docs/architecture/unified-api-mcp-entrypoints.md`
- `docs/architecture/system-topology.md`
- `docs/architecture/ai-orchestration-flow.md`
- `docs/architecture/deployment-modes.md`
- `docs/data-models/README.md`
- `docs/data-models/ledger-core-model.md`
- `docs/data-models/identity-access-model.md`

## 2. Current Stack (2026-02)
- Backend: C# 14 / .NET 10 Minimal APIs.
- Unified API surface: REST Minimal API endpoints and MCP endpoints in one ASP.NET Core host with shared business logic services.
- Orchestration: Aspire 13.3 preview (daily channel) with AppHost composition and MCP diagnostics.
- Web: Next.js 16 + React 19 + Tailwind.
- Mobile: Expo SDK 55 + React Native.
- Data visualization: Web `react-apexcharts` (reporting/time-series), Mobile `victory-native-xl` (native charts).
- Data: PostgreSQL with `pgvector` and `azure_ai` extension path.
- AI: Deterministic + semantic retrieval + MAF fallback with release-gate governance.

## 3. Core Architecture Rules
- Single-entry ledger semantics are immutable source truth.
- `UserNote` and `AgentNote` remain separate storage and UI lanes.
- Escalation order is fixed: deterministic -> semantic -> MAF.
- Ambiguous/high-impact outcomes route to `NeedsReview`.
- External messaging actions are deny-by-default.
- Aspire `WithReference(...)` and service discovery are required over hardcoded endpoints.
- Minimal API handlers and MCP tools must remain thin presentation wrappers over shared core use-case services.

## 3.1 Dashboard and Reporting Component Framework Standard (2026-02-26)
- Web default: `react-apexcharts` for dashboard/reporting visuals that require time-series interactions (zoom, pan, interval switching, annotations, mixed-series).
- Mobile default: `victory-native-xl` for native, high-performance chart rendering aligned to Expo/React Native workflows.
- Existing `recharts` visuals are considered transitional legacy. No new `recharts` usage should be introduced in net-new dashboard/reporting features.
- Time-bucket aggregation (`day`, `week`, `month`) must happen before render in view-model selectors/hooks (not inside chart components) using a lightweight date utility.
- Keep chart styling tokenized and semantic (theme variables/tokens), not hardcoded per-chart values.

Rationale:
- Current M6 experience has quality issues in dashboarding/reporting controls and interval interactions.
- `react-apexcharts` provides stronger financial/time-series UX for the current web redesign target.
- `victory-native-xl` provides a composable native path for mobile parity without forcing web-oriented wrappers.

## 4. Runtime Topology (Current)
```mermaid
flowchart TB
    subgraph Client
        Web[Next.js 16 Web]
        Mobile[Expo 55 Mobile]
    end

    subgraph AppHost[AppHost Aspire 13.3 preview]
        Api[API .NET 10]
        Worker[Worker .NET 10]
        Mcp[MCP Diagnostics]
    end

    subgraph DataAndAI[Data and AI]
        Pg[(PostgreSQL)]
        MAF[MAF Fallback]
    end

    Web <--> Api
    Mobile <--> Api
    Api <--> Pg
    Worker <--> Pg
    Api <--> MAF
    Worker --> Plaid[Plaid APIs]
    Mcp -.observability.-> Api
    Mcp -.observability.-> Worker
```

## 5. Database Connectivity Modes
AppHost supports two explicit modes:

1. External connection-string mode:
`ConnectionStrings:mosaicmoneydb` is supplied and AppHost consumes it directly.
2. Provisioned local mode:
AppHost provisions `mosaic-postgres` with `AddAzurePostgresFlexibleServer(...).RunAsContainer()` and references `mosaicmoneydb`.

See `docs/architecture/deployment-modes.md` for operational details and troubleshooting paths.

## 6. AI Governance Notes
- Release blocking is defined by MM-AI-11.
- Official evaluator replay and artifact mapping is MM-AI-12 (`In Review` until cloud evidence is captured).
- Architecture details for evaluation/replay are documented in `docs/architecture/ai-orchestration-flow.md`.

## 7. Decision Log Snapshot
- Canonical PostgreSQL resource: `mosaic-postgres`.
- Canonical DB/connection name: `mosaicmoneydb`.
- DB-only Azure provisioning uses `src/apphost.database/apphost.cs`.
- Aspire deploy remains AppHost-scoped; DB-only rollout is achieved by deploying the DB-only AppHost project.
- Unified REST + MCP hosting is adopted for `MosaicMoney.Api`; explicit MCP tools are required (no auto-conversion from Minimal API routes).