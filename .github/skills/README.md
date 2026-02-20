# Mosaic Money Skills

This folder contains project-local Agent Skills for Mosaic Money.

## Installation model
- Project scope: skills are available from `.github/skills/`.
- Structure rule: each skill lives in its own folder and contains `SKILL.md`.
- Invocation model: skills can be auto-loaded by relevance or manually invoked via `/` commands.

## Why these skills exist
- They are curated from patterns in:
  - `github/awesome-copilot`
  - `anthropics/skills`
- They are adapted to Mosaic Money constraints (Aspire-first orchestration, single-entry ledger, HITL review flow).

## Included skills
- `aspire-mosaic-money`: Aspire orchestration and package policy for this repo.
- `agent-governance`: safety, guardrails, and escalation rules.
- `agentic-eval`: evaluation loops for AI and feature quality.
- `nuget-manager`: safe NuGet workflows with Aspire package preference.
- `webapp-testing`: Playwright-driven web verification workflow.
- `prd`: PRD decomposition and acceptance criteria workflow.
