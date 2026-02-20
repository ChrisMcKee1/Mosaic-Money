---
name: agent-governance
description: Governance and safety workflow for Mosaic Money agent tasks. Use when implementing AI workflows, automation, data mutations, external integrations, or any potentially sensitive operation.
---

# Agent Governance

Use this skill to apply policy checks before implementation.

## Governance flow
1. Classify risk as `low`, `medium`, or `high`.
2. Validate task against Mosaic Money hard constraints.
3. Decide `allow`, `needs-review`, or `deny`.
4. Record rationale in implementation notes or `AgentNote` summary.

## Mosaic Money hard constraints
- Single-entry ledger model only.
- Preserve `UserNote` and `AgentNote` as separate tracks.
- Do not execute external messaging actions directly.
- Route ambiguous financial or classification outcomes to `NeedsReview`.
- Keep deterministic and in-database methods ahead of model fallback where possible.

## Output format for sensitive changes
- `Risk:` low|medium|high
- `Decision:` allow|needs-review|deny
- `Rationale:` concise policy explanation
- `Escalation:` what must be reviewed by a human
