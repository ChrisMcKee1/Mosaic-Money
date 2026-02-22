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
dotnet user-secrets set "ConnectionStrings:PostgresDB" "<value>"
dotnet user-secrets list
```

File-based AppHost (`apphost.cs`):

```bash
# Add once in apphost.cs:
#:property UserSecretsId=<id>

dotnet user-secrets set "ConnectionStrings:PostgresDB" "<value>" --file apphost.cs
dotnet user-secrets list --file apphost.cs
```

## Contract examples

Backend `appsettings.json` example (placeholders only):

```json
{
  "ConnectionStrings": {
    "PostgresDB": ""
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