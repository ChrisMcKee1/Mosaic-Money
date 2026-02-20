---
name: aspire-mosaic-money
description: Aspire-first orchestration workflow for Mosaic Money. Use when creating, wiring, debugging, or reviewing AppHost resources, service discovery, or .NET and JavaScript integration packages.
---

# Aspire Mosaic Money

Use this skill before changing AppHost composition, service startup wiring, or cross-service connectivity.

## Required sequence
1. Read `docs/agent-context/aspire-dotnet-integration-policy.md`.
2. Read `docs/agent-context/aspire-javascript-frontend-policy.md`.
3. Confirm proposed changes use `WithReference(...)` and service discovery instead of hardcoded endpoints.
4. Confirm package choices follow Aspire-hosting and Aspire-client integration defaults.

## Guardrails
- Prefer `Aspire.Hosting.*` in AppHost and `Aspire.*` integration packages in services.
- For PostgreSQL EF paths, use `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` and `AddNpgsqlDbContext`.
- For non-EF PostgreSQL paths, use `Aspire.Npgsql` and `AddNpgsqlDataSource`.
- For JS apps, use `AddJavaScriptApp`, `AddViteApp`, or `AddNodeApp`.
- Do not introduce `AddNpmApp`.

## Validation checklist
- Resource dependencies are explicit in AppHost.
- Health and startup readiness are deterministic.
- No literal connection strings where references can provide configuration.
