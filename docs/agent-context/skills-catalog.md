# Mosaic Money Skill Catalog

This document maps externally researched skills to the Mosaic Money Dream Team agents.

## Sources reviewed
- VS Code Agent Skills docs: `https://code.visualstudio.com/docs/copilot/customization/agent-skills`
- Anthropic reference skills: `https://github.com/anthropics/skills/tree/main/skills`
- Awesome Copilot skills: `https://github.com/github/awesome-copilot/tree/main/skills`

## Selection criteria
- Direct fit for Mosaic Money stack: Aspire, .NET, Next.js, AI workflows.
- Reinforces hard architecture guardrails.
- Improves safety, testability, and implementation consistency.

## Installed project-local skills
- `.github/skills/aspire-mosaic-money/SKILL.md`
  - Derived from Aspire-oriented skill patterns.
  - Enforces Mosaic Money AppHost and integration package rules.
- `.github/skills/agent-governance/SKILL.md`
  - Derived from agent governance pattern skills.
  - Adds risk classification and escalation policy.
- `.github/skills/agentic-eval/SKILL.md`
  - Derived from iterative evaluation skills.
  - Requires measurable quality checks before completion.
- `.github/skills/nuget-manager/SKILL.md`
  - Derived from NuGet/package management skill patterns.
  - Standardizes package updates and restore validation.
- `.github/skills/webapp-testing/SKILL.md`
  - Derived from Playwright web testing skill patterns.
  - Standardizes frontend verification and failure capture.
- `.github/skills/prd/SKILL.md`
  - Derived from PRD skill patterns.
  - Standardizes planning and decomposition workflows.

## External skills to use by name when available
- `aspire`
- `microsoft-docs`
- `microsoft-code-reference`
- `git`
- `azure-role-selector` (only when IAM role selection is requested)

## Agent-to-skill mapping
- Planner: `prd`, `agent-governance`, `aspire-mosaic-money`, `agentic-eval`, `microsoft-docs`.
- Backend: `aspire-mosaic-money`, `nuget-manager`, `agent-governance`, `agentic-eval`, `microsoft-code-reference`.
- Frontend: `aspire-mosaic-money`, `webapp-testing`, `agent-governance`, `agentic-eval`.
- Mobile: `agent-governance`, `agentic-eval`, `prd`.
- AI: `agent-governance`, `agentic-eval`, `aspire-mosaic-money`, `microsoft-docs`.
- DevOps: `aspire-mosaic-money`, `agent-governance`, `webapp-testing`, `nuget-manager`, `microsoft-docs`.

## Installation correctness checklist
- Skill directories are under `.github/skills/<skill-name>/SKILL.md`.
- The `name` field matches each parent folder name.
- Agent files explicitly instruct when to load skills before implementation.
