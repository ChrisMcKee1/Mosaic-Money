# Deployment Modes

## Mode A: External Connection String
Use when an existing PostgreSQL server is managed outside local AppHost provisioning.

- Configure `ConnectionStrings:mosaicmoneydb` for AppHost runtime.
- AppHost consumes the provided connection string and skips local server/container provisioning.

## Mode B: Provisioned Local/Cloud Graph
Use when AppHost should provision/own the PostgreSQL resource in the orchestration graph.

- AppHost defines `mosaic-postgres` and references `mosaicmoneydb`.
- Local full-stack uses `.RunAsContainer()` for developer workflows.
- Azure provisioning can use `src/apphost.database/apphost.cs` for DB-only rollout.

## Secret Handling
- Define secret inputs as AppHost parameters with `secret: true`.
- Store local values in user-secrets, not committed files.
- Inject through `WithReference(...)` and `WithEnvironment(...)` bindings.

## Operational Notes
- `aspire deploy` is AppHost-scoped; DB-only deployment uses DB-only AppHost project.
- Validate extension activation in target DB when required:
  - `vector`
  - `azure_ai`
