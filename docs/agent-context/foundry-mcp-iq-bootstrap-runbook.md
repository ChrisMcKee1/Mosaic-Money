# Foundry Agent Bootstrap Runbook (Minimal V1)

Last updated: 2026-03-01

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
- Pending: document Service Bus necessity recommendation for current milestone.
- Deferred after validation: investigate Streamable HTTP MCP migration and rollout plan.

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
