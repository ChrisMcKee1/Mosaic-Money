# Agent Context and Policy Docs


## Agent Loading
- Load when: Load first to route to the correct docs/agent-context file for the current task.
- Apply with workspace policy: [.github/copilot-instructions.md](../../.github/copilot-instructions.md)

This folder contains AI-agent-facing context and implementation policies.

## Purpose
- Keep operational guardrails and agent execution context separate from long-form product planning docs.
- Provide stable references for custom agents in [.github/agents](../../.github/agents/).
- Act as the routing index so detailed docs avoid duplicating each other.

## Canonical planning sources
- [project-plan/PRD.md](../../project-plan/PRD.md)
- [project-plan/architecture.md](../../project-plan/architecture.md)

## Files in this folder
- [prd-agentic-context.md](./prd-agentic-context.md): planner-friendly PRD summary for task decomposition.
- [architecture-agentic-context.md](./architecture-agentic-context.md): architecture constraints and routing map for agents.
- [aspire-dotnet-integration-policy.md](./aspire-dotnet-integration-policy.md): C# / EF / Aspire integration package policy.
- [aspire-javascript-frontend-policy.md](./aspire-javascript-frontend-policy.md): frontend JavaScript / Aspire orchestration policy.
- [aspire-local-run-reliability.md](./aspire-local-run-reliability.md): deterministic local startup, recovery, and diagnostics workflow for AppHost resources.
- [identity-claim-mapping-and-account-access-migration-playbook.md](./identity-claim-mapping-and-account-access-migration-playbook.md): MM-ASP-08/MM-ASP-09 identity mapping contract and migration rollout/rollback runbook.
- [clerk-sample-users-household-validation-runbook.md](./clerk-sample-users-household-validation-runbook.md): repeatable two-persona Clerk sample-user + household bootstrap and API/UI/DB verification workflow.
- [runtime-agentic-worker-runbook.md](./runtime-agentic-worker-runbook.md): runtime worker queue retry/dead-letter/replay and trace-correlation operations for assistant and orchestration lanes.
- [foundry-mcp-iq-bootstrap-runbook.md](./foundry-mcp-iq-bootstrap-runbook.md): minimal v1 Foundry agent bootstrap runbook (model + system prompt), plus tracked follow-up items for MCP tool integration.
- [secrets-and-configuration-playbook.md](./secrets-and-configuration-playbook.md): layered secret management and per-project configuration contract guidance.
- [semantic-search-pattern.md](./semantic-search-pattern.md): canonical dynamic semantic-search and hybrid retrieval pattern for transaction search APIs.
- [skills-catalog.md](./skills-catalog.md): cross-directory skill inventory (repo, user, extension), JIT loading policy, and per-agent baseline/on-demand mappings.
- [instructions-catalog.md](./instructions-catalog.md): curated custom-instructions selection, exclusions, and adaptation decisions.

