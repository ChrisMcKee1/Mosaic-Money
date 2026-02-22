---
name: Mosaic Money .NET API
description: Mosaic Money backend rules for C# 14, .NET 10 Minimal APIs, EF Core 10, PostgreSQL 18, and Aspire integration patterns.
applyTo: '**/*.{cs,csproj}'
---

# Mosaic Money .NET API Rules

## Framework and API style
- Use .NET 10 with Minimal APIs as the default API surface.
- Use controllers only when explicitly requested for a specific integration need.
- Keep endpoints resource-oriented and return clear HTTP status codes.

## Data and persistence
- Use EF Core 10 patterns and strongly typed domain models.
- Design for single-entry transactions and maintain stable idempotency keys.
- Keep query behavior explicit and avoid hidden N+1 patterns.

## Aspire-specific requirements
- Follow `docs/agent-context/aspire-dotnet-integration-policy.md` before package or registration changes.
- Prefer Aspire integration packages over provider-only packages.
- Use `AddNpgsqlDbContext` or `AddNpgsqlDataSource` with reference-driven configuration.
- Avoid literal connection strings where `WithReference(...)` can provide configuration.
- Define orchestration-level secrets in AppHost with `AddParameter(..., secret: true)` and source local values from AppHost user-secrets.
- Keep committed `ConnectionStrings` entries non-sensitive (empty or placeholders) and rely on runtime injection.
- When adding config keys, update project-level `appsettings.json` placeholders so required keys remain visible and reviewable.

## Security and correctness
- Validate and sanitize external input.
- Use parameterized data access.
- Enforce authorization checks on sensitive operations.
- Prefer fail-closed behavior for ambiguous or invalid state transitions.

## Tests and verification
- Add focused tests for critical financial logic and edge cases.
- Include regression checks when changing matching, categorization, or posting behavior.
