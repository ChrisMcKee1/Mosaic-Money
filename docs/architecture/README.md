# Architecture Docs

This folder contains detailed Mosaic Money architecture artifacts that back `project-plan/architecture.md`.

## Files
- `system-topology.md`: runtime boundaries and service interactions.
- `ai-orchestration-flow.md`: deterministic/semantic/MAF escalation and review loop.
- `deployment-modes.md`: local and Azure deployment modes, wiring, and constraints.

## Guardrails
- Preserve single-entry ledger semantics.
- Preserve dual-track notes (`UserNote`, `AgentNote`).
- Keep human review in the loop for ambiguous or high-impact actions.
- Prefer Aspire `WithReference(...)` service wiring over hardcoded endpoints.
