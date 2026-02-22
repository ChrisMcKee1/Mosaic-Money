---
name: aspire
description: 'Aspire skill covering the Aspire CLI, AppHost orchestration, service discovery, integrations, MCP server, VS Code extension, Dev Containers, GitHub Codespaces, templates, dashboard, and deployment. Use when the user asks to create, run, debug, configure, deploy, or troubleshoot an Aspire distributed application.'
---

# Aspire Skill (Daily Channel)

Use this skill for all Aspire orchestration, debugging, MCP, and integration work in this repository.

## Workspace baseline

- The repo is using Aspire daily channel and file-based AppHost (`src/apphost.cs`) with `Aspire.AppHost.Sdk@13.3.0-preview`.
- Prefer Aspire CLI over ad hoc scripts when managing AppHost lifecycle.
- Keep wiring reference-driven (`WithReference(...)`, service discovery), not hardcoded URLs/connection strings.

## CLI reality check (validated in this workspace)

Validated against `aspire --version` = `13.3.0-preview.1.26119.3+...`.

### Core command groups

- `aspire run`, `stop`, `start`, `restart`, `wait`, `ps`, `resources`, `logs`
- `aspire telemetry` (`logs`, `spans`, `traces` subcommands)
- `aspire doctor`, `aspire docs`, `aspire agent`, `aspire add`, `aspire update`

### MCP command migration in 13.3 daily

- Preferred commands:
    - `aspire agent init`
    - `aspire agent mcp`
- Legacy commands still exist but are deprecated aliases:
    - `aspire mcp init` (deprecated)
    - `aspire mcp start` (deprecated)

## Running Aspire in agent environments

Use detached mode so the orchestration process survives terminal task boundaries.

```bash
aspire run --detach --isolated
```

Notes:
- `--detach` runs in background and returns control immediately.
- `--isolated` randomizes ports and isolates user-secrets context for parallel runs.
- Use `aspire run --format Json --detach` for machine-readable startup output.

Stop behavior:

```bash
# Stop entire AppHost
aspire stop

# Stop specific resource
aspire stop <resource>
```

## Operational checks

Before and after AppHost changes (`apphost.cs`), use:

```bash
aspire ps
aspire resources
aspire logs --tail 200
```

Useful variants:

```bash
aspire resources --watch --format Json
aspire logs <resource> --follow
aspire telemetry traces <resource>
aspire telemetry logs <resource>
```

## MCP workflow

1. Initialize agent integration from repo root:

```bash
aspire agent init
```

2. Ensure MCP server entry in `.vscode/mcp.json` is:

```json
"aspire": {
    "type": "stdio",
    "command": "aspire",
    "args": ["agent", "mcp"]
}
```

3. Restart/reload the agent host so new MCP settings are picked up.

## MCP tools and call order (operational usage)

This section is the default execution order when using Aspire MCP tools.

### Tool inventory (validated from `configure-the-mcp-server` docs)

- Resource state:
    - `list_resources`
    - `execute_resource_command`
- Logs and traces:
    - `list_console_logs`
    - `list_structured_logs`
    - `list_traces`
    - `list_trace_structured_logs`
- AppHost context:
    - `list_apphosts`
    - `select_apphost`
- Integration discovery:
    - `list_integrations`
    - `get_integration_docs`

### Workflow A: establish correct AppHost context

1. Call `list_apphosts`.
2. If multiple AppHosts are running, call `select_apphost` first.
3. Call `list_resources` to establish current health and available commands.

### Workflow B: diagnose a failing resource

1. `list_resources` to identify unhealthy/failed resources.
2. `list_console_logs(<resource>)` for startup/runtime errors.
3. `list_structured_logs(<resource>)` for correlated structured events.
4. `list_traces(<resource>)` to find request-level failures and latency spikes.
5. `list_trace_structured_logs(<trace>)` to inspect logs in trace context.
6. `execute_resource_command(<resource>, <command>)` only after root cause is clear (for example restart).

### Workflow C: add an integration correctly

1. `list_integrations` to discover current hosting packages.
2. `get_integration_docs(<package>)` to retrieve exact setup guidance.
3. Implement AppHost + service wiring with `WithReference(...)` and integration packages.
4. Re-run `list_resources` and logs/traces checks to validate startup and connectivity.

### Workflow D: documentation lookup when MCP docs lag daily builds

1. Prefer CLI docs tooling first on daily channel:
     - `aspire docs search "<query>"`
     - `aspire docs get <slug>`
2. If MCP exposes doc tools in your environment, use:
     - `search_docs` then `get_doc`
3. If neither has the needed daily update, verify behavior directly with CLI help:
     - `aspire --help`
     - `aspire agent --help`
     - `aspire docs --help`

### MCP visibility control

- Use `.ExcludeFromMcp()` in AppHost when a resource should not be exposed to MCP results.

## Documentation tools strategy

- Always prefer official docs via `aspire docs` tooling first.
- MCP doc-search tools can vary by build/environment; do not assume they are always present.
- If official Aspire docs tools are not sufficient for a daily change, use Context7 fallback against `/microsoft/aspire.dev` and then confirm via CLI `--help` output.

## Daily build provenance workflow (branches and PRs)

When running daily builds, do not rely only on mainline docs pages. Trace behavior from your exact build commit and related PRs.

1. Capture exact build identity:
    - `aspire --version`
    - Extract commit SHA after `+` in version output.
2. Resolve commit in `dotnet/aspire`:
    - `GET /repos/dotnet/aspire/commits/<sha>`
    - `GET /repos/dotnet/aspire/commits/<sha>/pulls`
3. Review MCP/agent/docs PR history in `dotnet/aspire`:
    - Search PRs for `aspire agent`, `aspire agent mcp`, `aspire docs`, `mcp`.
4. Review docs-track PRs in `microsoft/aspire.dev`:
    - Look for CLI rename/behavior updates and open MCP doc gaps.
5. Validate behavior locally (source of truth for current machine):
    - `aspire --help`
    - `aspire agent --help`
    - `aspire docs --help`
    - `aspire docs get configure-the-mcp-server --section Tools`

### Current MCP/agent PR watchlist (validated)

- Runtime repo (`dotnet/aspire`):
  - `#14180` rename `aspire mcp` -> `aspire agent`
  - `#14217` VS Code extension support for `aspire agent`
  - `#14223` VS Code extension support for `aspire agent mcp`
  - `#14310` resource lifecycle CLI commands
  - `#14315` `aspire docs` command (`list/search/get`)
  - `#14537` malformed MCP JSON handling (open)
  - `#14569` Playwright MCP replacement flow (open)
- Docs repo (`microsoft/aspire.dev`):
  - `#415` rename CLI reference docs from `aspire mcp` to `aspire agent`
  - `#457` MCP security/troubleshooting/deployment docs (open)

## Integration workflow (required)

When adding resources/integrations:

1. Use `list_integrations` first.
2. Use `get_integration_docs` for the chosen package.
3. Align integration versions with AppHost SDK track.
4. Preserve Mosaic Money constraints:
     - `Aspire.Hosting.*` in AppHost
     - `Aspire.*` client packages in services
     - `AddJavaScriptApp` / `AddViteApp` / `AddNodeApp` only (no `AddNpmApp`)

## Troubleshooting order

1. `aspire doctor`
2. `aspire resources`
3. `aspire logs` and `aspire telemetry logs`
4. `aspire telemetry traces` / `spans`
5. MCP tools (`list_resources`, `list_structured_logs`, `list_traces`, `execute_resource_command`)

## References

- Aspire docs: `https://aspire.dev`
- Runtime repo: `https://github.com/dotnet/aspire`
- Docs repo: `https://github.com/microsoft/aspire.dev`
- Community integrations: `https://github.com/CommunityToolkit/Aspire`
