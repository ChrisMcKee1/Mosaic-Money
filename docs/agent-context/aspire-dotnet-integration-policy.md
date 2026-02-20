# Aspire .NET Integration Policy

This policy applies to any C#/.NET service that runs under Mosaic Money Aspire orchestration.

## Why this exists
- Avoid conflicts between hand-written .NET setup and Aspire resource injection.
- Keep service discovery, health, telemetry, and resilience consistent across all services.
- Prevent drift into non-Aspire wiring patterns that break local orchestration.

## Required defaults for .NET services
- Include and use the solution Service Defaults project.
- In service startup, call `builder.AddServiceDefaults()`.
- In web apps, call `app.MapDefaultEndpoints()` for `/health` and `/alive` in development.
- Prefer service discovery and `WithReference(...)` over hardcoded hostnames or URLs.

## Required package strategy
- AppHost orchestration packages: use `Aspire.Hosting.*` packages.
- Service client packages: use `Aspire.*` integration packages before direct provider packages.
- PostgreSQL + EF Core standard:
  - AppHost: `Aspire.Hosting.PostgreSQL`
  - Service (EF): `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL`
  - Service (raw data source): `Aspire.Npgsql`

## Required registration strategy
- In AppHost, define resources and pass them with `WithReference(...)`.
- For EF Core services, use `builder.AddNpgsqlDbContext<TDbContext>(connectionName: "...")`.
- For non-EF data access, use `builder.AddNpgsqlDataSource(connectionName: "...")`.
- Connection names must match the AppHost resource names.

## Avoid unless explicitly justified
- Manual `UseNpgsql(connectionString)` setup from literal connection strings.
- Direct provider-only NuGet setup that bypasses Aspire integration packages.
- Manually setting cross-service URLs when service discovery can resolve resources.

## Implementation notes
- If a special case requires direct provider APIs, document why in code comments.
- Prefer adding integrations via Aspire CLI (`aspire add <integration>`) to align versions.

## Source grounding
- Aspire service defaults: `https://aspire.dev/fundamentals/service-defaults/`
- Aspire PostgreSQL get started: `https://aspire.dev/integrations/databases/postgres/postgres-get-started/`
- Aspire PostgreSQL EF Core get started: `https://aspire.dev/integrations/databases/efcore/postgres/postgresql-get-started/`
