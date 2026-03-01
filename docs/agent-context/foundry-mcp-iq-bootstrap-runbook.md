# Foundry MCP and IQ Bootstrap Runbook

This runbook captures the current Mosaic Money setup path for provisioning a Microsoft Foundry agent with:
- Postgres MCP access
- API MCP access
- Foundry IQ knowledge-base MCP access

It is designed for local/dev operator setup and assumes Azure-side access for project and role assignment changes.

## Scope and guardrails
- Keep single-entry ledger semantics in agent instructions.
- Treat ambiguous/high-impact actions as `NeedsReview` and require human approval.
- Never commit real keys, connection strings, or credentials.
- Use AppHost parameters and user-secrets for local secret values.

## Prerequisites
- Azure CLI logged into the target tenant/subscription.
- Foundry project endpoint (`https://<resource>.services.ai.azure.com/api/projects/<project>`).
- Foundry project resource ID when you need ARM connection create/update. The script can derive this from existing project connections when available.
- Foundry model deployment name (for example `gpt-5.3-codex`).
- Azure AI Search knowledge base already created (for Foundry IQ grounding).
- Remote MCP endpoints available for Postgres and API tools.

## Script
Use `scripts/foundry/setup-foundry-agent.ps1` to create/update project `RemoteTool` connections and create the Foundry agent definition.

Example:

```powershell
pwsh ./scripts/foundry/setup-foundry-agent.ps1 `
  -ProjectEndpoint "https://<resource>.services.ai.azure.com/api/projects/<project>" `
  -ModelDeploymentName "gpt-5.3-codex" `
  -AgentName "Mosaic" `
  -ProjectResourceId "/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<account>/projects/<project>" `
  -ConnectionsApiVersion "2025-11-15-preview" `
  -DatabaseMcpLabel "mosaic-postgres" `
  -DatabaseMcpEndpoint "https://<postgres-mcp-endpoint>/mcp" `
  -DatabaseConnectionName "mosaic-postgres-connection" `
  -ApiMcpLabel "mosaic-api" `
  -ApiMcpEndpoint "https://<api-mcp-endpoint>/api/mcp" `
  -ApiConnectionName "mosaic-api-connection" `
  -SearchServiceEndpoint "https://<search-service>.search.windows.net" `
  -KnowledgeBaseName "<knowledge-base-name>" `
  -KnowledgeConnectionName "mosaic-iq-connection"
```

## New Foundry API and auth expectations
- Agent create/update and execution routes should use Foundry project endpoint with Entra bearer auth (`az account get-access-token --resource https://ai.azure.com`).
- Start with `api-version=v1` for agent routes and only fallback to preview versions when tenant compatibility requires it.
- If a request returns `Key-based authentication is not supported for this route`, switch to bearer auth immediately.
- Project connection create/update remains ARM-based (`management.azure.com`) while list/get can use Foundry data-plane (`{project_endpoint}/connections`).

## PostgreSQL MCP instruction template
When using direct PostgreSQL MCP integration (without Search knowledge-base routing), include explicit tool-parameter guidance in agent instructions:

```text
You are a helpful agent that can use MCP tools to assist users. Use the available MCP tools to answer questions and perform tasks.

Use these parameters when calling PostgreSQL MCP tools:
- database: <YOUR_DATABASE_NAME>
- resource-group: <YOUR_RESOURCE_GROUP>
- server: <YOUR_SERVER_NAME>
- subscription: <YOUR_SUBSCRIPTION_ID>
- user: <CONTAINER_APP_IDENTITY_NAME>
```

The bootstrap script now appends this block automatically when `-PostgresDatabase`, `-PostgresResourceGroup`, `-PostgresServer`, `-PostgresSubscription`, or `-PostgresUser` values are provided.

## Common pitfalls and fixes
- Pitfall: Mixing Azure OpenAI inference patterns (`openai.azure.com`, `cognitiveservices.azure.com` scope) into Foundry Agent Service routes.
  Fix: Use `https://ai.azure.com` token scope for Foundry Agent Service routes.
- Pitfall: Assuming only `Microsoft.MachineLearningServices` project IDs.
  Fix: Support `Microsoft.CognitiveServices/accounts/.../projects/...` IDs and derive from existing connection IDs when needed.
- Pitfall: Failing bootstrap when `ProjectResourceId` is unknown.
  Fix: Resolve from Foundry data-plane connection metadata and proceed.
- Pitfall: Creating MCP tools with legacy fields.
  Fix: Use `server_label`, `server_url`, `allowed_tools`, `require_approval`, and `project_connection_id`.
- Pitfall: Search knowledge-base create fails with `Requested value 'extractedData' was not found` or `...does not support minimal retrieval reasoning effort`.
  Fix: Use `outputMode=answerSynthesis` and set retrieval reasoning effort to `medium` for `mcpTool` knowledge sources on this tenant.
- Pitfall: Agent create/update returns `memory_search is not supported` when combining memory + MCP on `api-version=v1`.
  Fix: Treat memory attachment as capability-gated by model/tenant and validate support before forcing combined tool payloads.

## Local AppHost configuration keys
The AppHost now supports these Foundry agent keys for injection into API/Worker:
- `AiWorkflow__Agent__Foundry__McpDatabaseToolName`
- `AiWorkflow__Agent__Foundry__McpDatabaseToolEndpoint`
- `AiWorkflow__Agent__Foundry__McpDatabaseToolProjectConnectionId`
- `AiWorkflow__Agent__Foundry__McpDatabaseAllowedToolsCsv`
- `AiWorkflow__Agent__Foundry__McpDatabaseRequireApproval`
- `AiWorkflow__Agent__Foundry__McpApiToolName`
- `AiWorkflow__Agent__Foundry__McpApiToolEndpoint`
- `AiWorkflow__Agent__Foundry__McpApiToolProjectConnectionId`
- `AiWorkflow__Agent__Foundry__McpApiAllowedToolsCsv`
- `AiWorkflow__Agent__Foundry__McpApiRequireApproval`
- `AiWorkflow__Agent__Foundry__KnowledgeBaseMcpServerLabel`
- `AiWorkflow__Agent__Foundry__KnowledgeBaseMcpEndpoint`
- `AiWorkflow__Agent__Foundry__KnowledgeBaseProjectConnectionId`
- `AiWorkflow__Agent__Foundry__KnowledgeBaseAllowedToolsCsv`
- `AiWorkflow__Agent__Foundry__KnowledgeBaseRequireApproval`

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

## Required human-in-the-loop checks
- Verify project managed identity roles on Azure AI Search (`Search Index Data Reader`; add `Contributor` only if index writes are needed).
- Confirm project connection auth mode for each MCP endpoint.
- Validate non-prod data boundaries for Postgres MCP access and least-privilege table grants.
