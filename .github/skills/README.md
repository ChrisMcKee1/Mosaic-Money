# Mosaic Money Skills

This folder contains project-local Agent Skills for Mosaic Money.

Last audited: 2026-03-01

## Installation model
- Project scope: skills are available from `.github/skills/`.
- Structure rule: each skill lives in its own folder and contains `SKILL.md`.
- Invocation model: skills can be auto-loaded by relevance or manually invoked via `/` commands.
- Cross-directory note: additional shared skills may also exist under user/extension paths; see `docs/agent-context/skills-catalog.md` for full inventory and when-to-load guidance.

## External Skill Locations To Audit
- User-level Copilot skills: `C:\Users\chrismckee\.copilot\skills\`
- User-level agent pack skills: `C:\Users\chrismckee\.agents\skills\`
- Extension-provided skills under `.vscode-insiders\extensions\...`
- Keep `docs/agent-context/skills-catalog.md` synchronized whenever these inventories change.

## Why these skills exist
- They are curated from patterns in:
  - `github/awesome-copilot`
  - `anthropics/skills`
  - `vercel-labs/agent-skills`
  - `vercel-labs/next-skills`
  - `vercel/ai`
  - `vercel/ai-elements`
  - `vercel/streamdown`
  - `vercel/components.build`
- They are adapted to Mosaic Money constraints (Aspire-first orchestration, single-entry ledger, HITL review flow).

## Included skills
- `aspire`: daily-channel Aspire CLI + MCP workflow baseline for this repo.
- `aspire-mosaic-money`: Aspire orchestration and package policy for this repo.
- `agent-governance`: safety, guardrails, and escalation rules.
- `agentic-eval`: evaluation loops for AI and feature quality.
- `ai-sdk`: Vercel AI SDK implementation and troubleshooting guidance.
- `ai-elements`: AI-native React component patterns and integration guidance.
- `frontend-design`: production-grade UI interfaces with high design quality, avoiding generic AI aesthetics.
- `vercel-react-best-practices`: React and Next.js performance optimization guidance.
- `vercel-composition-patterns`: scalable React composition patterns.
- `next-best-practices`: Next.js architecture, boundary, and data pattern guidance.
- `next-cache-components`: Next.js 16 Cache Components and PPR implementation guidance.
- `next-upgrade`: migration/codemod guidance for upgrading Next.js versions.
- `web-design-guidelines`: accessibility/performance/UX review guidance for web interfaces.
- `building-components`: composable component design and API ergonomics guidance.
- `streamdown`: streaming-safe React markdown rendering guidance for chat UIs.
- `gh-cli`: GitHub CLI reference for repo/issue/PR/release workflows.
- `git-commit`: conventional commit workflow and safe staging guidance.
- `github-projects`: GitHub Projects V2 board management, GraphQL mutations, and status sync workflow.
- `nuget-manager`: safe NuGet workflows with Aspire package preference.
- `playwright-cli`: browser automation command reference for interactive test/debug loops.
- `prd`: PRD decomposition and acceptance criteria workflow.
- `webapp-testing`: Playwright-driven web verification workflow.
