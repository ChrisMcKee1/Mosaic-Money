# Agent Context and Policy Docs

This folder contains AI-agent-facing context and implementation policies.

## Purpose
- Keep operational guardrails and agent execution context separate from long-form product planning docs.
- Provide stable references for custom agents in `.github/agents/`.

## Canonical planning sources
- `project-plan/PRD.md`
- `project-plan/architecture.md`

## Files in this folder
- `prd-agentic-context.md`: planner-friendly PRD summary for task decomposition.
- `architecture-agentic-context.md`: architecture constraints and routing map for agents.
- `aspire-dotnet-integration-policy.md`: C# / EF / Aspire integration package policy.
- `aspire-javascript-frontend-policy.md`: frontend JavaScript / Aspire orchestration policy.
- `aspire-local-run-reliability.md`: deterministic local startup, recovery, and diagnostics workflow for AppHost resources.
- `secrets-and-configuration-playbook.md`: layered secret management and per-project configuration contract guidance.
- `skills-catalog.md`: curated skill selection, installation model, and per-agent mappings.
- `instructions-catalog.md`: curated custom-instructions selection, exclusions, and adaptation decisions.
