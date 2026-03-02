# Mosaic Money Workspace Instructions

These instructions are always on for this repository.

## Scope
- This file is the strict policy surface for all tasks.
- Detailed procedures, runbooks, and implementation playbooks live under [docs/agent-context](../docs/agent-context/) and [docs/architecture](../docs/architecture/).

## Architecture
- Backend: C# 14 + .NET 10 Minimal APIs.
- Orchestration: Aspire AppHost (`src/apphost.cs`) with `WithReference(...)` and service discovery.
- Web: Next.js 16 App Router, React 19, Tailwind.
- Mobile: Expo SDK 55.
- Data: PostgreSQL + EF Core + `pgvector`/`azure_ai` where configured.
- Azure PostgreSQL Flexible Server (`mosaic-postgres` with `mosaicmoneydb`) is the default cloud database target.
- Runtime agent orchestration is worker-owned (`MosaicMoney.Worker`) with Foundry runtime integration and durable run provenance.
- Copilot is a coding/UI assistant, not the runtime orchestration engine.

## Operational Readiness (Azure Postgres)
- Azure database policy may stop PostgreSQL nightly to control spend.
- Before database-dependent work, verify Azure Postgres is running and start it if needed.
- For Azure operations, research current Azure guidance first and then execute using approved Azure tools/workflows.
- If Azure Postgres is intentionally unavailable for a task, use the documented local AppHost database path and call out the mode in your verification notes.

## Skill-First Workflow
- Always check available skills before planning or coding to improve quality and reduce rework.
- Start with [.github/skills/README.md](skills/README.md) and then inspect relevant skill folders under [.github/skills](skills/).
- Use [docs/agent-context/skills-catalog.md](../docs/agent-context/skills-catalog.md) to map tasks to the right skills and loading order.

## Research-First Workflow
- Research first before implementing non-trivial changes, integrations, or unfamiliar APIs.
- If implementation attempts fail two or three times, pause and re-research the specific blocker before continuing.
- When behavior-critical decisions are made, include source references in notes/PR summaries.

## Non-Negotiable Guardrails
- Keep single-entry ledger semantics. Never introduce double-entry accounting.
- Preserve `UserNote` and `AgentNote` as separate fields.
- Keep projections/amortization as derived logic. Never mutate raw ledger truth for projection output.
- Route ambiguous/high-impact actions to `NeedsReview` and require human approval.
- Never enable autonomous external messaging or high-impact autonomous actions.

## Integration Conventions
- AppHost uses `Aspire.Hosting.*`; service projects use `Aspire.*` integrations first.
- Prefer `AddNpgsqlDbContext(...)` / `AddNpgsqlDataSource(...)` with connection names from AppHost references.
- Prefer service discovery and `WithReference(...)` over hardcoded endpoints/connection strings.
- JavaScript resources in AppHost use `AddJavaScriptApp`, `AddViteApp`, or `AddNodeApp`.
- Do not add deprecated `AddNpmApp`.

## Assistant And Runtime Conventions
- Conversational API routes are `/api/v1/agent/conversations/*`.
- Worker command lane for assistant orchestration is `runtime-agent-message-posted`.
- REST and MCP must both resolve authenticated household-member scope server-side before account/transaction access.

## Secrets And Local Credential Files
- AppHost secrets must be declared with `AddParameter("<name>", secret: true)` and stored in local user-secrets.
- Never commit `.env`, `.env.local`, API keys, passwords, or full connection strings.
- Treat all `NEXT_PUBLIC_*` as public and non-secret.
- Partner triage credentials live only in ignored local files under `src/MosaicMoney.Web`:
	- `triage-partners.env.local`
	- `partner-triage.credentials.local.md` (optional notes)
- Clerk + Plaid sandbox triage credentials live only in ignored local:
	- `src/MosaicMoney.Web/triage-idp-plaid.env.local`

## Build And Validation Commands
- Prefer existing tasks/scripts; do not invent commands.
- Canonical startup/recovery/diagnostics command sets are maintained in [docs/agent-context/aspire-local-run-reliability.md](../docs/agent-context/aspire-local-run-reliability.md).
- Policy gate: `pwsh -File .github/scripts/test-orchestration-policy-gates.ps1`.
- Web validation: `npm --prefix src/MosaicMoney.Web run build`
- Mobile validation: `npm --prefix src/MosaicMoney.Mobile run typecheck`

## Known Pitfalls
- Treat [docs/agent-context/aspire-local-run-reliability.md](../docs/agent-context/aspire-local-run-reliability.md) as the source of truth for lock-file, startup, telemetry, and recovery pitfalls.
- Treat [src/MosaicMoney.Web/playwright.config.mjs](../src/MosaicMoney.Web/playwright.config.mjs) as the source of truth for local E2E web/mock-api port alignment.

## Critical References
- Entry points:
	- [docs/agent-context/README.md](../docs/agent-context/README.md)
	- [docs/architecture/README.md](../docs/architecture/README.md)
	- [docs/data-models/README.md](../docs/data-models/README.md)
- High-priority operational references:
	- [docs/agent-context/secrets-and-configuration-playbook.md](../docs/agent-context/secrets-and-configuration-playbook.md)
	- [docs/agent-context/aspire-local-run-reliability.md](../docs/agent-context/aspire-local-run-reliability.md)
	- [docs/agent-context/runtime-agentic-worker-runbook.md](../docs/agent-context/runtime-agentic-worker-runbook.md)
	- [docs/architecture/auth-scope-and-access-control-flow.md](../docs/architecture/auth-scope-and-access-control-flow.md)
- Skill and instruction discovery:
	- [docs/agent-context/skills-catalog.md](../docs/agent-context/skills-catalog.md)
	- [.github/skills/README.md](skills/README.md)
	- [.github/instructions/README.md](instructions/README.md)

## Change Quality
- Keep changes focused and incremental.
- When behavior/governance changes, sync the corresponding docs in [docs/agent-context](../docs/agent-context/) and [docs/architecture](../docs/architecture/).
- Favor readability/composability: thin entrypoints, feature-focused modules, and explicit boundaries.
