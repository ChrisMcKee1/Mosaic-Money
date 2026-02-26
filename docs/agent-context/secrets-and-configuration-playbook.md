# Secrets and Configuration Playbook

This playbook defines how Mosaic Money manages secrets, connection strings, and environment variables across distributed applications.

## Core model

Secret management is not a single-file model. It is a layered model:

1. Configuration contract per project (committed placeholders)
2. Local secret values for development (not committed)
3. Aspire orchestration injection at runtime
4. Production secret source (for example Key Vault, Foundry-managed config, platform secrets)

The goal is to keep values secure while keeping required keys discoverable for every engineer.

## Required artifacts by app type

| App type | Committed contract artifact | Local secret source | Runtime injection path |
| --- | --- | --- | --- |
| AppHost (project-based) | AppHost code + docs | `dotnet user-secrets` on project | `WithReference(...)` / `WithEnvironment(...)` |
| AppHost (file-based) | `apphost.cs` with `#:property UserSecretsId=<id>` + docs | `dotnet user-secrets --file apphost.cs` | `WithReference(...)` / `WithEnvironment(...)` |
| .NET API/worker | `appsettings.json` with placeholders only | AppHost user-secrets and optional service project user-secrets | Aspire connection/env injection |
| Next.js web | `.env.example` with placeholders only | `.env.local` (not committed) and Aspire-injected vars | AppHost env injection and server-side config |
| Mobile app | documented env key contract placeholders | local untracked env files or secure platform settings | backend/API mediated access |

## Rules for placeholders and docs

- Keep `appsettings*.json` committed for non-sensitive defaults and placeholder keys only.
- Keep `.env.example` committed for JavaScript projects as key templates only.
- Never commit `.env`, `.env.local`, credentials, tokens, or full connection strings.
- Every newly introduced secret/env key must be documented with:
1. key name
2. consuming project(s)
3. local setup command
4. production source
5. whether the value is public (`NEXT_PUBLIC_*`) or private

## Local setup commands

Project-based AppHost:

```bash
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:mosaicmoneydb" "<value>"
dotnet user-secrets list
```

File-based AppHost (`apphost.cs`):

```bash
# Add once in apphost.cs:
#:property UserSecretsId=<id>

dotnet user-secrets set "ConnectionStrings:mosaicmoneydb" "<value>" --file apphost.cs
dotnet user-secrets list --file apphost.cs
```

Azure PostgreSQL AppHost admin credentials (file-based AppHost):

```bash
dotnet user-secrets set "Parameters:mosaic-postgres-admin-username" "<admin-user>" --file src/apphost.cs
dotnet user-secrets set "Parameters:mosaic-postgres-admin-password" "<admin-password>" --file src/apphost.cs
dotnet user-secrets list --file src/apphost.cs
```

DB-only AppHost deployment variant:

```bash
dotnet user-secrets set "Parameters:mosaic-postgres-admin-username" "<admin-user>" --file src/apphost.database/apphost.cs
dotnet user-secrets set "Parameters:mosaic-postgres-admin-password" "<admin-password>" --file src/apphost.database/apphost.cs
dotnet user-secrets list --file src/apphost.database/apphost.cs
```

Azure PostgreSQL authentication and RBAC bootstrap (developer mode):

```bash
# Enable dual auth during migration/dev verification
az postgres flexible-server update \
  --resource-group mosaic-money-db-centralus \
  --name <server-name> \
  --microsoft-entra-auth Enabled \
  --password-auth Enabled

# Set current Entra user as server admin
az postgres flexible-server microsoft-entra-admin create \
  --resource-group mosaic-money-db-centralus \
  --server-name <server-name> \
  --display-name <user-upn> \
  --object-id <user-object-id> \
  --type User

# Broad developer RBAC (temporary; tighten before production)
az role assignment create --assignee-object-id <user-object-id> --role "Contributor" --scope "/subscriptions/<sub>/resourceGroups/mosaic-money-db-centralus"
az role assignment create --assignee-object-id <user-object-id> --role "User Access Administrator" --scope "/subscriptions/<sub>/resourceGroups/mosaic-money-db-centralus"
az role assignment create --assignee-object-id <user-object-id> --role "Key Vault Administrator" --scope "/subscriptions/<sub>/resourceGroups/mosaic-money-db-centralus/providers/Microsoft.KeyVault/vaults/<vault-name>"
```

Verification commands:

```bash
az postgres flexible-server microsoft-entra-admin list --resource-group mosaic-money-db-centralus --server-name <server-name>
az role assignment list --assignee-object-id <user-object-id> --all
```

## Contract examples

Backend `appsettings.json` example (placeholders only):

```json
{
  "ConnectionStrings": {
    "mosaicmoneydb": ""
  },
  "AI": {
    "EmbeddingModel": "text-embedding-3-small",
    "Dimensions": 1536
  }
}
```

Frontend `.env.example` example (template only):

```env
NEXT_PUBLIC_API_URL=https://localhost:5001
OTEL_SERVICE_NAME=nextjs-web
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4318
AUTH_SECRET=<set-in-.env.local-or-injected-by-orchestration>
```

## Review checklist for new config keys

When a PR adds or changes a secret/config key, verify all items:

1. Key defined in the right runtime layer (`AddParameter(..., secret: true)` for orchestration-level secrets).
2. Local setup path documented with exact command(s).
3. Placeholder contract updated (`appsettings.json` and/or `.env.example`).
4. Production source documented.
5. No real secret committed.
6. No private values exposed via `NEXT_PUBLIC_*`.

## Identity member-context mapping keys (MM-ASP-08)

These keys support deterministic local and CI mapping for APIs that require household-member identity context.

Required behavior notes:

1. Values must be valid active `HouseholdUsers.Id` GUIDs when set.
2. Values are environment-scoped and must not be hardcoded in source files.
3. These are not credentials, but they are still operational identifiers and should be managed through local env files or CI secret/variable stores.

Web key:

1. `MOSAIC_HOUSEHOLD_USER_ID` (server-side env, optional)

Mobile key:

1. `EXPO_PUBLIC_MOSAIC_HOUSEHOLD_USER_ID` (optional)

Notes:

1. `EXPO_PUBLIC_*` values are public and must never carry secret material.
2. Keep committed templates in `.env.example`; keep real values in untracked `.env.local` or CI environment configuration.

## Plaid local setup contract (Sandbox and Production)

Plaid keys are always private and must be configured via AppHost parameters and user-secrets.

Required keys:

1. `Parameters:plaid-client-id` (secret)
2. `Parameters:plaid-secret` (secret)
3. `Plaid:Environment` (`sandbox` or `production`, non-secret)

Optional non-secret keys:

1. `Plaid:RedirectUri` (OAuth redirect URI used for Link token issuance)
2. `Plaid:CountryCodes` (for example `US`)
3. `Plaid:ClientName` (Link display name, max 30 chars)
4. `Plaid:Language` (Link locale, for example `en`)
5. `Plaid:WebhookUrl` (server endpoint for Plaid webhook delivery)
6. `Plaid:TransactionsSyncBootstrapCursor` (default bootstrap cursor, `now` by default)
7. `Plaid:TransactionsSyncBootstrapCount` (initial `/transactions/sync` page size, range `1..500`)
8. `Plaid:UseDeterministicProvider` (local-only override for deterministic simulation; keep `false` for sandbox/provider validation)

File-based AppHost commands:

```bash
dotnet user-secrets set "Parameters:plaid-client-id" "<plaid-client-id>" --file src/apphost.cs
dotnet user-secrets set "Parameters:plaid-secret" "<plaid-secret>" --file src/apphost.cs
dotnet user-secrets list --file src/apphost.cs
```

Notes:

- Never commit Plaid `client_id`, `secret`, `access_token`, or `public_token` values.
- Plaid Link `public_token` is ephemeral and must be exchanged server-side.
- Persist `access_token` and `item_id` only in backend secure storage paths.

## Clerk local setup contract (M8 authentication)

Clerk values are defined at AppHost level and injected into API/Web at runtime.

Required AppHost parameter keys:

1. `Parameters:clerk-publishable-key` (public)
2. `Parameters:clerk-secret-key` (secret)
3. `Parameters:clerk-issuer` (public configuration)

Injected runtime keys:

1. API:
  - `Authentication__Clerk__Issuer`
  - `Authentication__Clerk__SecretKey`
2. Web:
  - `CLERK_PUBLISHABLE_KEY`
  - `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY`
  - `CLERK_SECRET_KEY`
3. Mobile:
  - Mobile is not currently AppHost-launched in this repo. Keep `EXPO_PUBLIC_CLERK_PUBLISHABLE_KEY` in local/untracked mobile env files based on `src/MosaicMoney.Mobile/.env.example`.

Project-based AppHost commands:

```bash
dotnet user-secrets init --project <path-to-apphost-csproj>
dotnet user-secrets set "Parameters:clerk-publishable-key" "<pk_test_...>" --project <path-to-apphost-csproj>
dotnet user-secrets set "Parameters:clerk-secret-key" "<sk_test_...>" --project <path-to-apphost-csproj>
dotnet user-secrets set "Parameters:clerk-issuer" "https://<your-instance>" --project <path-to-apphost-csproj>
dotnet user-secrets list --project <path-to-apphost-csproj>
```

File-based AppHost commands (this repository):

```bash
dotnet user-secrets set "Parameters:clerk-publishable-key" "<pk_test_...>" --file src/apphost.cs
dotnet user-secrets set "Parameters:clerk-secret-key" "<sk_test_...>" --file src/apphost.cs
dotnet user-secrets set "Parameters:clerk-issuer" "https://<your-instance>" --file src/apphost.cs
dotnet user-secrets list --file src/apphost.cs
```

Required contract placeholders:

1. `src/MosaicMoney.Api/appsettings.json`
  - `Authentication:Clerk:Issuer`
  - `Authentication:Clerk:SecretKey`
  - `Authentication:Clerk:Audience`
2. `src/MosaicMoney.Web/.env.example`
  - `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY`
  - `CLERK_SECRET_KEY`
3. `src/MosaicMoney.Mobile/.env.example`
  - `EXPO_PUBLIC_CLERK_PUBLISHABLE_KEY`

Notes:

- Do not commit Clerk secret key values.
- Treat `NEXT_PUBLIC_*` and `EXPO_PUBLIC_*` values as public.
- For complete tenant/provider steps (Microsoft SSO and passkeys), follow `docs/agent-context/clerk-tenant-provider-runbook.md`.

## Azure AI Foundry and Azure OpenAI local setup contract

Mosaic Money keeps AI model routing keys in AppHost user-secrets and injects them into API/worker through `WithEnvironment(...)`.

Required keys:

1. `Parameters:azure-openai-endpoint` (private endpoint URI; can be a Foundry/OpenAI endpoint)
2. `Parameters:azure-openai-api-key` (secret)
3. `Parameters:azure-openai-embedding-deployment` (non-secret deployment/model name)
4. `Parameters:azure-openai-chat-deployment` (non-secret deployment/model name)

API contract placeholders:

1. `AiWorkflow:Embeddings:Provider` (`deterministic` or `azure-openai`)
2. `AiWorkflow:Embeddings:AzureOpenAI:Endpoint`
3. `AiWorkflow:Embeddings:AzureOpenAI:ApiKey`
4. `AiWorkflow:Embeddings:AzureOpenAI:Deployment`
5. `AiWorkflow:Embeddings:AzureOpenAI:ApiVersion`
6. `AiWorkflow:Chat:AzureOpenAI:Endpoint`
7. `AiWorkflow:Chat:AzureOpenAI:ApiKey`
8. `AiWorkflow:Chat:AzureOpenAI:Deployment`

File-based AppHost commands:

```bash
dotnet user-secrets set "Parameters:azure-openai-endpoint" "https://<resource>.openai.azure.com/" --file src/apphost.cs
dotnet user-secrets set "Parameters:azure-openai-api-key" "<azure-openai-api-key>" --file src/apphost.cs
dotnet user-secrets set "Parameters:azure-openai-embedding-deployment" "text-embedding-3-small" --file src/apphost.cs
dotnet user-secrets set "Parameters:azure-openai-chat-deployment" "gpt-5.2" --file src/apphost.cs
dotnet user-secrets list --file src/apphost.cs

# Enable Azure embeddings in API runtime (project-based user-secrets)
dotnet user-secrets set "AiWorkflow:Embeddings:Provider" "azure-openai" --project src/MosaicMoney.Api/MosaicMoney.Api.csproj
dotnet user-secrets list --project src/MosaicMoney.Api/MosaicMoney.Api.csproj
```

Notes:

- Keep `AiWorkflow:Embeddings:Provider=deterministic` until endpoint/key/deployment values are set.
- Do not commit endpoint keys, API keys, or project-specific identifiers in `appsettings*.json`.