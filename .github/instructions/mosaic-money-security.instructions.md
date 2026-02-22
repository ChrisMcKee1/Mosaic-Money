---
name: Mosaic Money Security
description: Security-first guidance aligned to OWASP-style risks for backend, frontend, and integration code.
applyTo: '**/*.{cs,ts,tsx,js,jsx,json,yml,yaml}'
---

# Mosaic Money Security Rules

## Core rules
- Default to least privilege and deny-by-default authorization decisions.
- Validate and sanitize all external input.
- Use parameterized queries and safe serialization practices.
- Do not hardcode secrets, tokens, or credentials.

## Web and API safety
- Enforce authentication and authorization for sensitive actions.
- Use secure session and token handling.
- Prevent SSRF and path traversal where user-controlled paths or URLs are involved.
- Return safe error payloads without leaking internals.

## Dependency and config hygiene
- Prefer stable, maintained dependencies.
- Keep secure defaults for production configuration.
- Review and update vulnerable packages during dependency changes.

## Aspire secret workflow
- Define orchestration-level secrets in AppHost using `AddParameter(..., secret: true)`.
- Store local secret values in AppHost user-secrets; never commit local secret values to the repo.
- For local setup docs and runbooks, include both command paths: project-based AppHost uses `dotnet user-secrets init`, `dotnet user-secrets set "<Key>" "<Value>"`, `dotnet user-secrets list`; file-based AppHost adds `#:property UserSecretsId=<id>` and uses `dotnet user-secrets set "<Key>" "<Value>" --file apphost.cs` plus `dotnet user-secrets list --file apphost.cs`.
- Pass secrets and internal endpoints through `WithReference(...)` and `WithEnvironment(...)`.
- Commit only placeholder templates such as `.env.example`; do not commit `.env` or `.env.local`.
- Maintain per-project configuration contracts in committed placeholders (for example `appsettings.json`, `.env.example`) so required keys are discoverable without exposing values.
- Keep sensitive values out of `appsettings*.json`; use runtime injection or managed secret stores.
- Never expose secrets in browser-visible variables like `NEXT_PUBLIC_*`.

## AI workflow safety
- For agentic flows, enforce explicit human review for high-impact operations.
- Do not allow autonomous external messaging execution.
