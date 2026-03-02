# Architecture Agentic Context


## Agent Loading
- Load when: planning architecture boundaries, runtime invariants, or cross-domain routing decisions.
- Apply with workspace policy: [.github/copilot-instructions.md](../../.github/copilot-instructions.md)

Planner-facing architecture constraints and routing map.
Canonical source: [Full Architecture](../../project-plan/architecture.md)

## Single Responsibility
- This document defines architecture boundaries and invariants only.
- Operational procedures and setup commands live in runbooks under `docs/agent-context/`.
- Stack-specific implementation policies live in dedicated policy files.

## Required Source Documents
- [Architecture Docs Index](../architecture/README.md)
- [Master Platform Architecture](../architecture/master-platform-architecture.md)
- [Unified API and MCP Entrypoints](../architecture/unified-api-mcp-entrypoints.md)
- [Auth Scope and Access Control Flow](../architecture/auth-scope-and-access-control-flow.md)
- [Data Models Index](../data-models/README.md)

## Architecture Invariants
- API may host both Minimal APIs and MCP, but both must resolve server-side authenticated household-member scope before account/transaction reads or mutations.
- Runtime orchestration is worker-owned (`MosaicMoney.Worker`) and uses durable provenance records (`AgentRuns`, `AgentRunStages`, `AgentSignals`, `AgentDecisionAudit`, `IdempotencyKeys`).
- Conversational contracts use `/api/v1/agent/conversations/*` and worker command lane `runtime-agent-message-posted`.
- Deterministic and in-database AI paths remain primary; ambiguous or high-impact outcomes must route to `NeedsReview`.
- Ledger semantics remain single-entry with projection-only derived logic.

## Routing Map (Who Owns What)
- `mosaic-money-backend`: API contracts, entities, persistence, ingestion.
- `mosaic-money-frontend`: web UX, server/client boundary, charting and assistant surfaces.
- `mosaic-money-mobile`: Expo screens, offline-safe workflows, contract parity.
- `mosaic-money-ai`: retrieval/escalation policy, evaluation, HITL guardrails.
- `mosaic-money-devops`: AppHost composition, runtime messaging, diagnostics and deployment.

## Related Detailed Policies
- [Aspire .NET Integration Policy](./aspire-dotnet-integration-policy.md)
- [Aspire JavaScript Frontend Policy](./aspire-javascript-frontend-policy.md)
- [Secrets and Configuration Playbook](./secrets-and-configuration-playbook.md)
- [Aspire Local Run Reliability](./aspire-local-run-reliability.md)

