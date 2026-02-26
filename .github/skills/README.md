# Mosaic Money Skills

This folder contains project-local Agent Skills for Mosaic Money.

## Installation model
- Project scope: skills are available from `.github/skills/`.
- Structure rule: each skill lives in its own folder and contains `SKILL.md`.
- Invocation model: skills can be auto-loaded by relevance or manually invoked via `/` commands.
- Cross-directory note: additional shared skills may also exist under user/extension paths; see `docs/agent-context/skills-catalog.md` for full inventory and when-to-load guidance.

## Why these skills exist
- They are curated from patterns in:
  - `github/awesome-copilot`
  - `anthropics/skills`
- They are adapted to Mosaic Money constraints (Aspire-first orchestration, single-entry ledger, HITL review flow).

## Included skills
- `aspire`: daily-channel Aspire CLI + MCP workflow baseline for this repo.
- `aspire-mosaic-money`: Aspire orchestration and package policy for this repo.
- `agent-governance`: safety, guardrails, and escalation rules.
- `agentic-eval`: evaluation loops for AI and feature quality.
- `frontend-design`: production-grade UI interfaces with high design quality, avoiding generic AI aesthetics.
- `gh-cli`: GitHub CLI reference for repo/issue/PR/release workflows.
- `git-commit`: conventional commit workflow and safe staging guidance.
- `github-projects`: GitHub Projects V2 board management, GraphQL mutations, and status sync workflow.
- `nuget-manager`: safe NuGet workflows with Aspire package preference.
- `playwright-cli`: browser automation command reference for interactive test/debug loops.
- `prd`: PRD decomposition and acceptance criteria workflow.
- `webapp-testing`: Playwright-driven web verification workflow.
