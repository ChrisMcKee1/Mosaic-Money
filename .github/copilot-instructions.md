# Mosaic Money Workspace Instructions

These instructions are always on for this repository.

## Architecture
- Backend: C# 14 + .NET 10 Minimal APIs.
- Orchestration: Aspire 13.2 with service discovery and `WithReference(...)` wiring.
- Web: Next.js 16 App Router, React 19, Tailwind.
- Data: PostgreSQL 18 + EF Core 10, with `azure_ai` and `pgvector` where specified.
- Copilot is a coding/UI assistant, not the runtime orchestration layer.

## Domain Guardrails
- Enforce single-entry ledger semantics. Never introduce double-entry accounting.
- Preserve dual-track notes as separate fields: `UserNote` and `AgentNote`.
- Keep amortization as projection logic only; never mutate raw ledger truth for projections.
- Route ambiguous financial classification to `NeedsReview`; require human approval before final resolution.
- Never allow autonomous external messaging or high-impact AI actions without human review.

## Integration Conventions
- In AppHost, prefer `Aspire.Hosting.*`; in services, prefer `Aspire.*` integration packages.
- Use `AddNpgsqlDbContext(...)` or `AddNpgsqlDataSource(...)` with reference-driven connection names.
- Prefer service discovery and `WithReference(...)` over hardcoded URLs/connection strings.
- For JavaScript resources, use `AddJavaScriptApp`, `AddViteApp`, or `AddNodeApp`.
- Do not introduce deprecated `AddNpmApp`.

## Secret And Configuration Conventions
- Define orchestration-level sensitive values in AppHost with `builder.AddParameter("<name>", secret: true)`.
- Store local secret values in AppHost user-secrets (`dotnet user-secrets`) instead of source-controlled files.
- When documenting or scripting local setup, include both flows: project-based AppHost uses `dotnet user-secrets init`, `dotnet user-secrets set "<Key>" "<Value>"`, `dotnet user-secrets list`; file-based AppHost adds `#:property UserSecretsId=<id>` and uses `dotnet user-secrets set "<Key>" "<Value>" --file apphost.cs` plus `dotnet user-secrets list --file apphost.cs`.
- Inject secrets and service endpoints using `WithReference(...)` and `WithEnvironment(...)`, not hardcoded literals.
- Commit template env files only (for example `.env.example` with placeholders); never commit `.env`, `.env.local`, or real credentials.
- Keep `appsettings*.json` for non-sensitive defaults; do not commit passwords, API keys, or full connection strings.
- Treat `NEXT_PUBLIC_*` variables as public and never place sensitive values in them.
- Redact secret values from logs, docs, screenshots, and sample command output.

## Build And Test
- This repository is currently docs-first (planning/agent governance) with no canonical app build pipeline checked in yet.
- Do not invent project commands. Prefer commands defined by repo files (`README`, manifests, CI, scripts) once present.
- When behavior changes, run the closest available validation for touched areas and report what was or was not runnable.
- Add focused tests for money, date, and classification edge cases when implementation code is introduced or modified.

## Source References
- `docs/agent-context/architecture-agentic-context.md`
- `docs/agent-context/prd-agentic-context.md`
- `docs/agent-context/aspire-dotnet-integration-policy.md`
- `docs/agent-context/aspire-javascript-frontend-policy.md`
- `docs/agent-context/secrets-and-configuration-playbook.md`
- `docs/agent-context/skills-catalog.md`
- `.github/instructions/*.instructions.md`

## Change Quality
- Prefer focused, incremental changes with explicit verification.
- If a request conflicts with architecture or PRD constraints, follow repository constraints and explain the trade-off.
- Keep policy/context docs synchronized when governance or behavior changes.
