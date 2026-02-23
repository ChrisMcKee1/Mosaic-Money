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

## Diagnostics workflow (API, Worker, Web)

Use this standard sequence for local triage.

```powershell
aspire resources --project src/apphost.cs
aspire logs --project src/apphost.cs --tail 100
aspire logs api --project src/apphost.cs --tail 100
aspire logs worker --project src/apphost.cs --tail 100
aspire logs web --project src/apphost.cs --tail 100
```

For a live stream on one resource:

```powershell
aspire logs api --project src/apphost.cs --follow
```

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
