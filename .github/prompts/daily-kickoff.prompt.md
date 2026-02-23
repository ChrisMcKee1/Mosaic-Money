---
description: Cold-start orchestration - situational awareness, spec review, and task delegation.
agent: mosaic-money-planner
---

Start a fresh planning session for Mosaic Money. You have no prior context - build it from scratch.

## 1 - Situational awareness

- Read your primary context files: [PRD agentic context](../../docs/agent-context/prd-agentic-context.md), [architecture agentic context](../../docs/agent-context/architecture-agentic-context.md), [skills catalog](../../docs/agent-context/skills-catalog.md).
- Check recent git history (`git log --oneline -20`) to see what was shipped since the last session.
- Read the master task breakdown at [001-mvp-foundation-task-breakdown.md](../../project-plan/specs/001-mvp-foundation-task-breakdown.md) and scan milestone specs (002-006) for current statuses.
- Check your repo memory for any saved notes from prior sessions.

## 2 - Identify today's work

- List tasks that are `In Progress`, `Blocked`, or `In Review` and decide next steps for each.
- Pick the highest-priority `Not Started` tasks that are unblocked (dependencies met).
- Set selected tasks to `In Progress` in both the master and milestone spec files before delegating.

## 3 - Delegate to subagents

Route each task to the correct specialist (`mosaic-money-backend`, `mosaic-money-frontend`, `mosaic-money-mobile`, `mosaic-money-ai`, `mosaic-money-devops`).

Include these standing orders in every delegation:

1. **Research first.** Do not rely on memorised knowledge. We are on bleeding-edge versions (.NET 10, C# 14, Aspire 13, Next.js 16, React 19, EF Core 10, etc.). Look up current SDK docs, verify API shapes, and confirm version compatibility before writing code.
2. **Do not self-approve.** Subagents must not mark tasks `Done`. Report completion back to the planner for review.
3. **Follow repo guardrails.** Read the relevant instruction files (`.github/instructions/`) and skill files (`.github/skills/`) before starting.
4. **Validate.** Run tests, build checks, or Aspire preflight as appropriate. Include evidence in the report.

## 4 - Synthesise and track

After subagents report back, review outputs against done criteria, promote tasks to `In Review` or `Done`, update specs, and summarise progress.
