# Aspire Local Run Reliability (MM-ASP-05)

This runbook defines deterministic startup, diagnostics, and recovery paths for local Mosaic Money development on Aspire daily channel.

## Scope

- AppHost: `src/apphost.cs` (file-based AppHost)
- Resources: `postgres`, `mosaicmoneydb`, `api`, `worker`, `web`
- CLI channel: Aspire daily (`aspire agent ...` commands)

## Daily preflight

Run this before troubleshooting or changing local workflow scripts.

```powershell
aspire --version
aspire --help
aspire agent --help
aspire telemetry --help
aspire docs --help
aspire docs search "run detach isolated"
aspire docs search "resources logs wait command"
aspire docs search "configure the mcp server tools"
```

## Deterministic startup (recommended)

Run from repo root (`C:\Users\chrismckee\GitHub\Mosaic-Money`).

```powershell
dotnet build src/apphost.cs
aspire run --project src/apphost.cs --detach --isolated
aspire wait api --project src/apphost.cs --status healthy --timeout 180
aspire wait worker --project src/apphost.cs --status up --timeout 180
aspire wait web --project src/apphost.cs --status up --timeout 180
aspire resources --project src/apphost.cs
```

Why this path is deterministic:
- Uses explicit `--project` targeting from any working directory.
- Uses detached run so orchestration survives terminal lifecycle.
- Uses `wait` gates before developer traffic and diagnostics.

## Recovery workflow

Use this when resources are unhealthy, missing endpoints, or startup appears partial.

```powershell
aspire ps
aspire stop --project src/apphost.cs
dotnet build src/apphost.cs
aspire run --project src/apphost.cs --detach --isolated
aspire wait api --project src/apphost.cs --status healthy --timeout 180
aspire resources --project src/apphost.cs
```

If `dotnet build src/apphost.cs` fails with file lock errors (`MSB3027` or `MSB3021`), make sure all running AppHost instances are stopped first:

```powershell
aspire ps
aspire stop --project src/apphost.cs
```

If `web` exits and logs show `@next/swc-win32-x64-msvc ... is not a valid Win32 application` or `turbo.createProject` WASM binding errors, reset frontend dependencies and restart only the `web` resource:

```powershell
node -p "process.arch"
Remove-Item -Recurse -Force src/MosaicMoney.Web/node_modules
Remove-Item -Recurse -Force src/MosaicMoney.Web/.next
npm --prefix src/MosaicMoney.Web install
aspire restart web --project src/apphost.cs
```

Expected architecture in this workspace is `x64`.

## Dashboard + MCP diagnostics workflow (MM-ASP-06)

Use this as the single diagnostics workflow for API, Worker, Web, and AI-related traces available in the current milestone.

### 1) Establish reproducible runtime state

```powershell
dotnet build src/apphost.cs
aspire run --project src/apphost.cs --detach --isolated
aspire wait api --project src/apphost.cs --status healthy --timeout 180
aspire wait worker --project src/apphost.cs --status up --timeout 180
aspire wait web --project src/apphost.cs --status up --timeout 180
aspire resources --project src/apphost.cs
```

### 2) Dashboard-first triage

1. Open the dashboard URL emitted by `aspire run`.
2. Check `Resources` for unhealthy states and endpoint exposure.
3. Drill into each resource (`api`, `worker`, `web`) via `Structured logs` and `Traces`.
4. Use `Restart` from the resource actions menu only after root cause is identified.

### 3) CLI diagnostics snapshot (deterministic capture)

```powershell
aspire resources --project src/apphost.cs
aspire logs api --project src/apphost.cs --tail 100
aspire logs worker --project src/apphost.cs --tail 100
aspire logs web --project src/apphost.cs --tail 100
aspire telemetry logs api --project src/apphost.cs --limit 100
aspire telemetry logs worker --project src/apphost.cs --limit 100
aspire telemetry logs web --project src/apphost.cs --limit 100
aspire telemetry traces api --project src/apphost.cs --limit 50
aspire telemetry traces worker --project src/apphost.cs --limit 50
aspire telemetry traces web --project src/apphost.cs --limit 50
```

For error-focused trace triage:

```powershell
aspire telemetry traces api --project src/apphost.cs --limit 50 --has-error
aspire telemetry traces worker --project src/apphost.cs --limit 50 --has-error
aspire telemetry traces web --project src/apphost.cs --limit 50 --has-error
```

### 4) MCP diagnostics flow (agentic)

Prefer this tool order:
1. `list_apphosts`
2. `select_apphost` (only if multiple apphosts are running)
3. `list_resources`
4. `list_console_logs` for failing resource
5. `list_structured_logs` for correlated structured events
6. `list_traces` and `list_trace_structured_logs`
7. `execute_resource_command` for restart/start only after investigation

Current Aspire CLI in this workspace (`13.3.0-preview`) prefers `aspire agent init` and `aspire agent mcp`.
If docs output still shows legacy `aspire mcp init` / `aspire mcp start`, treat those as deprecated aliases and use `aspire agent ...` in new scripts and runbooks.

### 5) AI workflow traces at current milestone boundary

`MM-AI-10` is not complete, so expect AI telemetry to appear within `api` and `worker` traces/logs rather than a dedicated AI resource.

Use this bounded capture:

```powershell
aspire telemetry traces api --project src/apphost.cs --limit 100
aspire telemetry traces worker --project src/apphost.cs --limit 100
aspire telemetry logs api --project src/apphost.cs --limit 200 --severity Information
aspire telemetry logs worker --project src/apphost.cs --limit 200 --severity Information
```

When `MM-AI-10` introduces additional workflow resources/spans, extend this section with explicit resource-level commands.

### 6) VS Code task shortcuts

Use these task labels for consistent execution from VS Code:
- `Aspire: Stop AppHost`
- `Aspire: Build AppHost`
- `Aspire: Run Detached Isolated`
- `Aspire: Start Stack`
- `Aspire: Wait For API Healthy`
- `Aspire: Wait For Worker Up`
- `Aspire: Wait For Web Up`
- `Aspire: Show Resources`
- `Aspire: Logs Tail 100 (API)`
- `Aspire: Logs Tail 100 (Worker)`
- `Aspire: Logs Tail 100 (Web)`
- `Aspire: Telemetry Logs 100 (API)`
- `Aspire: Telemetry Logs 100 (Worker)`
- `Aspire: Telemetry Logs 100 (Web)`
- `Aspire: Telemetry Traces 50 (API)`
- `Aspire: Telemetry Traces 50 (Worker)`
- `Aspire: Telemetry Traces 50 (Web)`
- `Aspire: Diagnostics Snapshot (API/Worker/Web)`
- `Aspire: Recover Stack`

## MCP workflow (daily command surface)

Initialize and run MCP tooling with `aspire agent` commands.

```powershell
aspire agent init
```

MCP server command for local tooling:

```json
{
  "mcpServers": {
    "aspire": {
      "command": "aspire",
      "args": ["agent", "mcp"]
    }
  }
}
```

Do not use deprecated aliases (`aspire mcp init`, `aspire mcp start`) in new docs or scripts.

## Documented run paths

### Path A (preferred): full stack through Aspire

Use the deterministic startup and diagnostics sections above.

### Path B (standalone web fallback)

Only use this when you intentionally run the frontend outside AppHost orchestration.

```powershell
cd src/MosaicMoney.Web
npm install
$env:API_URL = "https://<api-host>:<api-port>"
npm run dev
```

Notes:
- `API_URL` is a fallback for standalone runs only.
- For normal local development, prefer Path A so service discovery and references remain orchestration-driven.

### Path C (mobile on Windows to physical phone)

Use this path for day-to-day mobile development when running Expo on Windows and validating on a physical phone.

```powershell
dotnet build src/apphost.cs
aspire run --project src/apphost.cs --detach --isolated
aspire wait api --project src/apphost.cs --status healthy --timeout 180

cd src/MosaicMoney.Mobile
copy .env.example .env.local
# Edit .env.local and set EXPO_PUBLIC_API_BASE_URL to your reachable API host, typically LAN IP.
npm install
npm run typecheck
npm run start:lan
```

If LAN mode cannot reach the dev server from your phone, use:

```powershell
npm run start:tunnel
```

Notes:
- `EXPO_PUBLIC_*` values are public and must not contain secrets.
- `127.0.0.1` works only from the same machine. Physical phones usually need the host LAN IP or a reachable endpoint.
- `npm run ios` requires a Mac-backed iOS simulator path.

## Secret setup reference (required command variants)

When AppHost secret parameters are introduced or renamed, use one of these flows.

Project-based AppHost flow:

```powershell
dotnet user-secrets init
dotnet user-secrets set "<Key>" "<Value>"
dotnet user-secrets list
```

File-based AppHost flow (`src/apphost.cs`):

```powershell
dotnet user-secrets set "<Key>" "<Value>" --file src/apphost.cs
dotnet user-secrets list --file src/apphost.cs
```

## Docker Postgres validation workflow (Plaid proof gate)

Use this only for local operator validation and troubleshooting. Runtime service connectivity must still rely on AppHost `WithReference(...)` wiring.

### 1) Discover container identity and credentials

```powershell
docker ps --format "{{.ID}} {{.Image}} {{.Names}}"
docker exec <container> printenv POSTGRES_USER
docker exec <container> printenv POSTGRES_PASSWORD
```

### 2) Connect with env-derived password (PowerShell)

```powershell
$env:PGPASSWORD = (docker exec <container> printenv POSTGRES_PASSWORD).Trim()
psql -h localhost -p <mapped-port> -U <postgres-user> -d <database> -c "SELECT now();"
```

Always clear the password variable after checks:

```powershell
Remove-Item Env:PGPASSWORD
```

### 3) Required Plaid data evidence SQL

```sql
SELECT extname FROM pg_extension WHERE extname IN ('vector','azure_ai');
```

```sql
SELECT 'PlaidItemCredentials' AS table_name, COUNT(*) AS row_count FROM "PlaidItemCredentials"
UNION ALL
SELECT 'PlaidItemSyncStates', COUNT(*) FROM "PlaidItemSyncStates"
UNION ALL
SELECT 'RawTransactionIngestionRecords', COUNT(*) FROM "RawTransactionIngestionRecords"
UNION ALL
SELECT 'EnrichedTransactions', COUNT(*) FROM "EnrichedTransactions";
```

### 4) Required API retrieval proof

After ingest/sync runs, validate queryability through API:

```powershell
Invoke-RestMethod -Method Get -Uri "http://localhost:5000/api/v1/transactions?page=1&pageSize=20"
```

Use the actual API host/port surfaced by Aspire if it differs.

## FE-08 Playwright regression path (backend-independent)

Use this path for deterministic web regression checks when backend resources are unavailable or when validating error-state rendering.

```powershell
cd src/MosaicMoney.Web
npm install
npm run test:e2e
```

Notes:
- The suite starts a local test-only mock API (`tests/e2e/mock-api-server.mjs`) and runs Next.js against it.
- No production secrets are required; browser-visible values remain non-sensitive.
- Aspire full-stack runs are still preferred for integrated service validation, but FE-08 gating does not require backend availability.

## Azure emulator guidance (Windows)

Use Azure emulators only when a feature under development depends on that Azure service. Mosaic Money's current default stack (AppHost + API + Worker + PostgreSQL) does not require Azure emulators for baseline local runs.

Recommended options on Windows:
- Azurite (Storage): primary local emulator for Blob/Queue/Table. Azure Storage Emulator is deprecated.
- Azure Cosmos DB Emulator: use local Windows installer or Docker variants for Cosmos-specific development.
- Azure Service Bus emulator: containerized local emulator; requires Docker Desktop and WSL on Windows.
- Azure Event Hubs emulator: containerized local emulator; requires Docker Desktop and WSL on Windows.

Windows prerequisites for Service Bus/Event Hubs emulators:
- Docker Desktop installed and running.
- WSL configured and Docker integrated with WSL.
- Use official installer repositories/scripts from Microsoft docs for setup.

Key caveats:
- Emulators are for development/test only, not production validation.
- Feature gaps vs cloud are expected (quotas, networking, security, management operations).
- Preserve production parity checks against real Azure resources before release.

References:
- https://learn.microsoft.com/azure/storage/common/storage-use-azurite
- https://learn.microsoft.com/azure/cosmos-db/how-to-develop-emulator
- https://learn.microsoft.com/azure/service-bus-messaging/test-locally-with-service-bus-emulator
- https://learn.microsoft.com/azure/event-hubs/test-locally-with-event-hub-emulator
