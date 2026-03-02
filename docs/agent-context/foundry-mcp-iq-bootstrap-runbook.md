# Foundry Agent Bootstrap Runbook (Minimal V1)

Last updated: 2026-03-01


## Agent Loading
- Load when: bootstrapping or updating the minimal Foundry hosted agent configuration.
- Apply with workspace policy: [.github/copilot-instructions.md](../../.github/copilot-instructions.md)

## Purpose
Create or update the hosted Foundry agent (`Mosaic`) with only:
- model deployment
- system prompt/instructions

This runbook intentionally does not attach MCP tools yet.

## Scope and guardrails
- Preserve single-entry ledger semantics in all instructions.
- Keep high-impact or ambiguous actions routed to `NeedsReview`.
- Do not commit credentials, tokens, or real secret values.
- Use Entra bearer auth for Foundry agent routes when possible.

## Active execution checklist
- Completed: simplify bootstrap script to minimal mode (no MCP tools, no memory tools).
- Completed: recreate `Mosaic` in Foundry using `gpt-5.3-codex` with prompt-only definition.
- Completed: validate direct Foundry `responses` call via `agent_reference` for `Mosaic`.
- Pending: validate app API runtime path end-to-end.
- Pending: validate web UI path end-to-end.
- Completed: document Service Bus necessity recommendation for current milestone.
- Deferred after API and UI validation: execute Streamable HTTP client-compatibility rollout and fallback checks.

## Prerequisites
- Azure CLI logged in to the target tenant/subscription.
- Foundry project endpoint:
  - `https://<resource>.services.ai.azure.com/api/projects/<project>`
- Model deployment name:
  - for current local usage: `gpt-5.3-codex`

## Script
Use `scripts/foundry/setup-foundry-agent.ps1`.

Current script behavior:
- Creates/updates one Foundry agent definition.
- Payload contains only `name` and `definition` (`kind`, `model`, `instructions`).
- No MCP tool configuration.
- No memory store configuration.

Example:

```powershell
pwsh ./scripts/foundry/setup-foundry-agent.ps1 `
  -ProjectEndpoint "https://<resource>.services.ai.azure.com/api/projects/<project>" `
  -ModelDeploymentName "gpt-5.3-codex" `
  -AgentName "Mosaic"
```

Optional instruction overrides:

```powershell
# Inline instructions text
-AgentInstructionsText "You are Mosaic ..."

# Or file-based instructions
-AgentInstructionsPath "./docs/agent-context/mosaic-system-prompt.txt"
```

## Validation commands

Direct agent fetch:

```powershell
$endpoint = "https://<resource>.services.ai.azure.com/api/projects/<project>"
$token = az account get-access-token --resource https://ai.azure.com --query accessToken -o tsv
$headers = @{ Authorization = "Bearer $token" }
Invoke-RestMethod -Method Get -Uri "$endpoint/agents/Mosaic?api-version=v1" -Headers $headers
```

Direct responses call:

```powershell
$endpoint = "https://<resource>.services.ai.azure.com/api/projects/<project>"
$token = az account get-access-token --resource https://ai.azure.com --query accessToken -o tsv
$headers = @{ Authorization = "Bearer $token"; 'Content-Type' = 'application/json' }
$body = @{
  agent_reference = @{ type = 'agent_reference'; name = 'Mosaic' }
  input = 'Hi Mosaic. One concise paragraph introducing your role.'
} | ConvertTo-Json -Depth 10
Invoke-RestMethod -Method Post -Uri "$endpoint/openai/v1/responses" -Headers $headers -Body $body
```

## Planned V2 architecture (not in this script yet)
- Keep two entry points in `MosaicMoney.Api`:
  - REST/Minimal API endpoints for frontend apps.
  - MCP endpoint for agents.
- Keep one shared business layer behind both entry points.
- Require authenticated user context for MCP calls before exposing data tools.
- Prefer Streamable HTTP transport for MCP after investigation and rollout validation.

## Service Bus necessity recommendation (current milestone)
Use Service Bus only where asynchronous decoupling is required. Keep interactive API/MCP calls synchronous.

Recommended now:
- Keep request/response reads, approvals, and deterministic classification entrypoints on direct REST or MCP HTTP calls.
- Keep user-scoped authorization checks in-process on the API path before any data access.
- Use Service Bus for asynchronous work that benefits from buffering, retries, and failure isolation.

Service Bus required cases:
- Long-running or retry-heavy background processing.
- Work that must survive downstream outages without failing the caller.
- Queue-based load leveling for bursty ingestion or orchestration events.

Service Bus not required cases:
- Tool calls that must return immediate user-visible results.
- High-trust policy checks that depend on the current authenticated user context.
- Simple request/response orchestration where queue semantics add latency without reliability gains.

## Streamable HTTP MCP findings and rollout plan
Research outcome:
- `ModelContextProtocol.AspNetCore` with `.WithHttpTransport()` is the preferred HTTP transport path and aligns with Streamable HTTP guidance in current Microsoft samples.
- `MosaicMoney.Api` is already configured with `.WithHttpTransport()` and `app.MapMcp("/api/mcp").RequireAuthorization();`.

Rollout steps after API/UI validation:
1. Confirm all MCP clients used by runtime and tooling support HTTP transport against `/api/mcp` over HTTPS.
2. Validate bearer-token header propagation on each client path.
3. Run side-by-side smoke checks (tool listing and one read/write-safe tool call) for each client surface.
4. Keep a temporary compatibility fallback only if a required client cannot use HTTP transport yet.

Rollback trigger:
- Any client path that cannot complete authenticated MCP calls over HTTP transport during validation.

Rollback action:
- Keep the existing server transport configuration unchanged and park client migration for that path until SDK compatibility is confirmed.

## OpenAPI renderer note
`MosaicMoney.Api` should expose runtime OpenAPI docs plus a local renderer in development.
Current target pattern:
- `builder.Services.AddOpenApi()`
- `app.MapOpenApi()` in development
- Scalar UI in development
- `launchSettings.json` `launchUrl` should point to the renderer path (`scalar`).

## Local secret commands
Project-based AppHost flow:

```powershell
cd src

dotnet user-secrets init --project apphost.cs

dotnet user-secrets set "foundry-agent-api-key" "<value>" --project apphost.cs

dotnet user-secrets set "foundry-agent-endpoint" "<value>" --project apphost.cs

dotnet user-secrets list --project apphost.cs
```

File-based AppHost flow (`#:property UserSecretsId=<id>` in `src/apphost.cs`):

```powershell
dotnet user-secrets set "foundry-agent-api-key" "<value>" --file src/apphost.cs

dotnet user-secrets set "foundry-agent-endpoint" "<value>" --file src/apphost.cs

dotnet user-secrets list --file src/apphost.cs
```

