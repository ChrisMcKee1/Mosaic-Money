# Architecture Docs

This folder contains detailed Mosaic Money architecture artifacts that back `project-plan/architecture.md`.

## Files
- `master-platform-architecture.md`: master cross-platform diagrams spanning clients, services, agents, eventing, and data boundaries.
- `architecture-decisions.md`: active architecture decision log (ADRs) with status, rationale, and source links.
- `unified-api-mcp-entrypoints.md`: unified Minimal API + MCP architecture pattern with shared core service reuse.
- `system-topology.md`: runtime boundaries and service interactions.
- `ai-orchestration-flow.md`: deterministic/semantic/MAF escalation and review loop.
- `deployment-modes.md`: local and Azure deployment modes, wiring, and constraints.
- `multi-agent-system-topology.md`: worker-owned runtime multi-agent topology and eventing boundaries.
- `multi-agent-orchestration-sequences.md`: sequence diagrams for ingestion-triggered and conversational assistant orchestration.

## Guardrails
- Preserve single-entry ledger semantics.
- Preserve dual-track notes (`UserNote`, `AgentNote`).
- Keep human review in the loop for ambiguous or high-impact actions.
- Prefer Aspire `WithReference(...)` service wiring over hardcoded endpoints.
