# Architecture Decision Log

Last updated: 2026-03-01

This log captures active architecture decisions for Mosaic Money and links to the canonical docs that explain implementation details.

## AD-001: Single-entry ledger semantics
- Status: Accepted
- Date: 2026-02-27
- Decision: The platform remains single-entry for ledger truth; no double-entry debit/credit model is introduced.
- Rationale: Matches product model and avoids introducing parallel accounting abstractions that conflict with existing data contracts.
- Sources: `docs/agent-context/prd-agentic-context.md`, `project-plan/PRD.md`

## AD-002: Human-in-the-loop for ambiguous or high-impact outcomes
- Status: Accepted
- Date: 2026-02-27
- Decision: Ambiguous financial classification and high-impact actions route to `NeedsReview` and require explicit human approval.
- Rationale: Enforces policy safety and prevents unreviewed irreversible user-impacting decisions.
- Sources: `docs/architecture/ai-orchestration-flow.md`, `docs/agent-context/prd-agentic-context.md`

## AD-003: Escalation ladder for AI decisions
- Status: Accepted
- Date: 2026-02-27
- Decision: Decision order is deterministic rules first, then semantic retrieval/fusion, then MAF fallback only when confidence remains below threshold.
- Rationale: Prioritizes cost, latency, and explainability before model-intensive fallback paths.
- Sources: `docs/architecture/ai-orchestration-flow.md`, `docs/agent-context/architecture-agentic-context.md`

## AD-004: Worker-owned asynchronous orchestration
- Status: Accepted
- Date: 2026-02-27
- Decision: API owns synchronous contracts and review actions; Worker owns asynchronous command processing, retries, and orchestration execution.
- Rationale: Isolates long-running/event-driven workflows from request-response API paths.
- Sources: `docs/architecture/multi-agent-system-topology.md`, `docs/architecture/multi-agent-orchestration-sequences.md`

## AD-005: Eventing backbone role separation
- Status: Accepted
- Date: 2026-02-27
- Decision: Use Service Bus for durable commands, Event Grid for fan-out notifications, and Event Hubs for telemetry/replay streams.
- Rationale: Keeps transport behavior aligned with each workload type and failure semantics.
- Sources: `docs/architecture/multi-agent-system-topology.md`, `docs/architecture/multi-agent-orchestration-sequences.md`

## AD-006: Aspire-native messaging wiring for Service Bus and Event Hubs
- Status: Accepted
- Date: 2026-02-27
- Decision: Service Bus and Event Hubs are provisioned and referenced through Aspire-native resources (`AddAzureServiceBus`, `AddAzureEventHubs`, `WithReference(...)`) instead of manual connection-string contracts.
- Rationale: Standardizes resource wiring, improves local reliability with emulator support, and aligns with Aspire integration guidance.
- Sources: `src/apphost.cs`, `src/MosaicMoney.Api/Program.cs`, `src/MosaicMoney.Worker/Program.cs`, `docs/agent-context/secrets-and-configuration-playbook.md`

## AD-007: Event Grid explicit configuration fallback
- Status: Accepted
- Date: 2026-02-27
- Decision: Event Grid publish settings remain explicit configuration values managed as AppHost secrets until a first-class Aspire Event Grid integration path is adopted.
- Rationale: Preserves functionality while avoiding non-standard or speculative resource wiring.
- Sources: `src/apphost.cs`, `docs/agent-context/secrets-and-configuration-playbook.md`

## AD-008: Secrets lifecycle and runtime injection
- Status: Accepted
- Date: 2026-02-27
- Decision: Sensitive values are defined in AppHost with `AddParameter(..., secret: true)`, stored locally with user-secrets, and injected via references/environment bindings.
- Rationale: Keeps secrets out of source control and centralizes orchestration-time secret flow.
- Sources: `docs/agent-context/secrets-and-configuration-playbook.md`, `docs/agent-context/aspire-dotnet-integration-policy.md`

## AD-009: API is resource server, clients are untrusted
- Status: Accepted
- Date: 2026-02-27
- Decision: Web/mobile clients never receive sensitive backplane credentials; API validates JWTs and enforces authorization.
- Rationale: Maintains clear trust boundaries and least privilege for user-facing surfaces.
- Sources: `docs/agent-context/architecture-agentic-context.md`, `project-plan/architecture.md`

## AD-010: Unified REST and MCP hosting in one ASP.NET Core service
- Status: Accepted
- Date: 2026-02-27
- Decision: `MosaicMoney.Api` is the pragmatic unified host for Minimal API endpoints and MCP endpoints, with both layers delegating to shared core business services.
- Rationale: Enables single deployment and shared policy/business logic while supporting both human-facing HTTP clients and agent-facing MCP clients.
- Sources: `docs/architecture/unified-api-mcp-entrypoints.md`, `project-plan/architecture.md`

## AD-011: No automatic Minimal API to MCP conversion
- Status: Accepted
- Date: 2026-02-27
- Decision: Minimal API routes are not auto-exposed as MCP tools. MCP entrypoints are explicit tool wrappers that call shared core services.
- Rationale: HTTP endpoint shape and MCP JSON-RPC tool contracts differ in protocol and interaction model, so explicit presentation-layer wrappers are required.
- Sources: `docs/architecture/unified-api-mcp-entrypoints.md`, `.github/instructions/csharp-mcp-server.instructions.md`

## AD-012: MCP transport baseline is HTTP transport with authenticated `/api/mcp`
- Status: Accepted
- Date: 2026-03-01
- Decision: `MosaicMoney.Api` uses `ModelContextProtocol.AspNetCore` `.WithHttpTransport()` and maps MCP at `/api/mcp` with required authorization.
- Rationale: Aligns with current Streamable HTTP-compatible guidance while preserving a single authenticated endpoint for agent tool access.
- Sources: `src/MosaicMoney.Api/Program.cs`, `docs/architecture/unified-api-mcp-entrypoints.md`, `docs/agent-context/foundry-mcp-iq-bootstrap-runbook.md`

## AD-013: Service Bus is selective, not default, for API and MCP execution paths
- Status: Accepted
- Date: 2026-03-01
- Decision: Interactive API and MCP request/response operations stay synchronous; Service Bus lanes are reserved for asynchronous, retry-heavy, and outage-tolerant workflows.
- Rationale: Prevents unnecessary queue latency for user-facing operations while preserving reliability and load-leveling where asynchronous semantics are required.
- Sources: `docs/architecture/unified-api-mcp-entrypoints.md`, `docs/agent-context/foundry-mcp-iq-bootstrap-runbook.md`, `https://learn.microsoft.com/azure/architecture/microservices/design/interservice-communication`
