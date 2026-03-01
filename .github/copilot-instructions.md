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

## Foundry New Vs Classic Guardrails
- Always determine whether docs/examples target Foundry (new) or Foundry (classic) before implementation.
- For New Foundry Agent Service, prefer project endpoints in the form `https://<resource>.services.ai.azure.com/api/projects/<project>`.
- For New Foundry agent creation and tool execution, prefer `Authorization: Bearer <token>` from `az account get-access-token --resource https://ai.azure.com`.
- Treat `"Key-based authentication is not supported for this route"` as a route/auth mismatch and immediately retry with Entra bearer tokens.
- For New Foundry agent create/update, start with `POST {project_endpoint}/agents?api-version=v1`; only fallback to preview API versions if tenant compatibility requires it.
- Do not mix Azure OpenAI inference guidance (`openai.azure.com`, `cognitiveservices.azure.com` scope) into Foundry Agent Service routes unless the target API explicitly requires it.
- For MCP tools, use the New Foundry schema fields: `type=mcp`, `server_label`, `server_url`, optional `allowed_tools`, `require_approval`, and `project_connection_id`.
- For direct PostgreSQL MCP integration, include explicit tool parameter guidance in agent instructions for: `database`, `resource-group`, `server`, `subscription`, and `user` (container app identity).
- For Foundry IQ knowledge bases, use MCP endpoint format `https://<search>.search.windows.net/knowledgebases/<kb>/mcp?api-version=2025-11-01-preview` and `allowed_tools` containing `knowledge_base_retrieve`.
- For Search knowledge-base create/update on current tenant/API, prefer `outputMode=answerSynthesis`; if using `mcpTool` knowledge sources, set retrieval reasoning effort to `medium`.
- Treat memory attachment as capability-gated per model/tenant: verify `memory_search` support before combining memory and MCP tools on a new agent version.
- Project connection lifecycle can require two planes: ARM (`https://management.azure.com/.../connections/...`) for create/update and Foundry data-plane (`{project_endpoint}/connections`) for list/get. Verify the required plane from current docs before coding.
- When project resource IDs are difficult to resolve in New Foundry, derive from existing connection IDs returned by the data-plane and avoid hardcoded provider assumptions.

## Secret And Configuration Conventions
- Define orchestration-level sensitive values in AppHost with `builder.AddParameter("<name>", secret: true)`.
- Store local secret values in AppHost user-secrets (`dotnet user-secrets`) instead of source-controlled files.
- When documenting or scripting local setup, include both flows: project-based AppHost uses `dotnet user-secrets init`, `dotnet user-secrets set "<Key>" "<Value>"`, `dotnet user-secrets list`; file-based AppHost adds `#:property UserSecretsId=<id>` and uses `dotnet user-secrets set "<Key>" "<Value>" --file apphost.cs` plus `dotnet user-secrets list --file apphost.cs`.
- Inject secrets and service endpoints using `WithReference(...)` and `WithEnvironment(...)`, not hardcoded literals.
- Commit template env files only (for example `.env.example` with placeholders); never commit `.env`, `.env.local`, or real credentials.
- Keep `appsettings*.json` for non-sensitive defaults; do not commit passwords, API keys, or full connection strings.
- Treat `NEXT_PUBLIC_*` variables as public and never place sensitive values in them.
- Redact secret values from logs, docs, screenshots, and sample command output.
- Store Partner A / Partner B triage login credentials only in local ignored files under `src/MosaicMoney.Web`: `triage-partners.env.local` (for `MM_PARTNER_A_EMAIL`, `MM_PARTNER_A_PASSWORD`, `MM_PARTNER_B_EMAIL`, `MM_PARTNER_B_PASSWORD`) and optional operator notes in `partner-triage.credentials.local.md`.
- Playwright triage scripts under `src/MosaicMoney.Web/scripts` auto-load `src/MosaicMoney.Web/triage-partners.env.local` before reading `MM_PARTNER_*` values; use `MM_TRIAGE_ENV_FILE` only when intentionally loading an alternate local env path.
- Never commit partner triage credentials or include them in `artifacts/release-gates/**` outputs.

## Plaid Sandbox And Product Mapping
- Use Plaid Sandbox as the default non-production data source for local and CI validation; do not require real user banking credentials for development workflows.
- Local redirect URIs may use localhost HTTP in sandbox mode (for example `http://localhost:<port>/onboarding/plaid`), but production redirect URIs must be registered HTTPS URLs.
- Build and validate ingestion through standard API/worker pipelines and persisted contracts; avoid one-off scripts as the primary path for data population.
- Use sandbox-simulated financial scenarios to validate database schemas, ingestion contracts, and downstream agent workflows end-to-end before production integrations.
- Before implementing a Plaid product beyond `transactions`, complete a source-linked capability mapping from PRD requirements to Plaid products/endpoints/webhooks and document schema + contract impact.
- Explicitly classify each candidate Plaid product as `Adopt Now`, `Adopt Later`, or `Out of Scope` with rationale, sandbox coverage notes, and human-review implications.

## Subagent Delegation And Local Postgres Validation
- When delegating Plaid ingestion or retrieval tasks, include explicit Docker/Postgres connection instructions in the handoff prompt.
- Prefer AppHost `WithReference(...)` runtime wiring for services; only use direct `psql` Docker checks for operator validation and troubleshooting.
- Never hardcode database credentials in code, specs, or logs. Read container credentials from runtime environment variables.
- For local Docker validation, require this credential-discovery flow:
	- Identify the Postgres container: `docker ps --format "{{.ID}} {{.Image}} {{.Names}}"`
	- Read username/password from container env: `docker exec <container> printenv POSTGRES_USER` and `docker exec <container> printenv POSTGRES_PASSWORD`
	- Run SQL with env-based password (PowerShell): `$env:PGPASSWORD = (docker exec <container> printenv POSTGRES_PASSWORD).Trim(); psql -h localhost -p <mapped-port> -U <postgres-user> -d <database> -c "SELECT now();"`
	- Clear the local shell password variable after validation: `Remove-Item Env:PGPASSWORD`
- For Plaid evidence gates, require subagents to return command + output for:
	- `SELECT extname FROM pg_extension WHERE extname IN ('vector','azure_ai');`
	- Row counts for `PlaidItemCredentials`, `PlaidItemSyncStates`, `RawTransactionIngestionRecords`, and `EnrichedTransactions`.
	- API retrieval proof via `GET /api/v1/transactions` showing ingested rows are queryable.
- If a subagent needs another domain change (for example AppHost port/secret wiring, backend contract updates, frontend onboarding adjustments), they must report the exact file-level change request and acceptance criteria back to the planner for scoped re-delegation.

## Task Lifecycle And GitHub Projects
- Task status lives in two places and must stay synchronized: spec markdown tables and the GitHub Projects board.
- The master task breakdown is `project-plan/specs/001-mvp-foundation-task-breakdown.md`; milestone-specific specs are `002`–`006`.
- Allowed status values: `Not Started`, `In Progress`, `Blocked`, `Parked`, `In Review`, `Done`, `Cut`.
- Only the `mosaic-money-planner` agent may set a task to `Done` or `Cut`.
- When changing a task status, update the spec file(s) first, then set the matching status on the GitHub Projects board.
- The GitHub Projects skill (`.github/skills/github-projects/SKILL.md`) contains project IDs, field IDs, status option IDs, and GraphQL mutation templates needed to interact with the board.
- A batch sync script at `.github/scripts/sync-project-board.ps1` can re-sync all issues and statuses.
- The `gh` CLI must have the `project` scope (`gh auth refresh -s project --hostname github.com`).

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
- `docs/agent-context/semantic-search-pattern.md`
- `docs/agent-context/secrets-and-configuration-playbook.md`
- `docs/agent-context/skills-catalog.md`
- `.github/instructions/*.instructions.md`
- `.github/skills/github-projects/SKILL.md`

## Change Quality
- Prefer focused, incremental changes with explicit verification.
- If a request conflicts with architecture or PRD constraints, follow repository constraints and explain the trade-off.
- Keep policy/context docs synchronized when governance or behavior changes.

## Code Organization And Readability
- Optimize every change for readability and maintainability over short-term convenience.
- Keep entrypoint files thin. `Program.cs` (API/Worker/AppHost) should act as composition root and delegate feature logic to focused modules.
- Avoid oversized files. When a file becomes hard to scan, split by feature/domain into subfolders (for example `Apis/`, `Domain/`, `Services/`, `Components/`, `hooks/`, `lib/`).
- For Minimal APIs, prefer route-group extension files by resource/workflow instead of accumulating endpoint handlers in one file.
- Apply building-block composition across backend, frontend, and workers: small cohesive units that compose cleanly.
- Do not force a strict frontend methodology, but follow atomic-style decomposition concepts (smaller reusable building blocks, clear boundaries, low coupling).
- Extract shared contracts/helpers only when there is proven multi-project reuse; avoid premature shared packages that add coupling without active consumers.
