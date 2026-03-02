# PRD Agentic Context


## Agent Loading
- Load when: decomposing scope, acceptance criteria, and milestone delivery slices.
- Apply with workspace policy: [.github/copilot-instructions.md](../../.github/copilot-instructions.md)

This file is the planner-friendly grounding summary for Mosaic Money.
Canonical source: [Full PRD](../../project-plan/PRD.md)

Related detail docs:
- [Architecture Index](../../project-plan/architecture.md)
- [Data Models Index](../data-models/README.md)

## Product Intent
- Build a cloud-first personal finance system for a two-user household with commingled accounts.
- Treat AI as a historian and drafting assistant, not an autonomous decision maker.

## Non-Negotiable Constraints
- Ledger model is single-entry only.
- Preserve dual-track notes as separate fields: `UserNote` and `AgentNote`.
- Never auto-send external messages; only draft text for user review.
- Keep reimbursement and ambiguous-link decisions human approved.

## Delivery Priorities
- Reliable ingestion and enrichment from Plaid data.
- Clear `NeedsReview` workflows for ambiguous transactions.
- Recurring forecasting and safe-to-spend projections.
- Business-expense isolation from household budget burn.
- Semantic retrieval over local PostgreSQL embeddings.
- Runtime agentic orchestration with durable workflow audit state, followed by specialist finance-agent expansion.
- Conversational assistant surface that orchestrates backend agents with explicit human approval gates.
- Household identity and account access policy controls.
- Clerk-based authentication and authorization across web/mobile/API with Microsoft SSO and passkey readiness.
- Discoverable settings and account-linking UX entry points (appearance, security, and Plaid linking).
- Release-gate evidence for AI quality before production promotion.

## AI Escalation Order
1. Deterministic rules.
2. PostgreSQL semantic operators (`azure_ai`, `pgvector`).
3. MAF graph workflow for ambiguous or complex cases.

## Release Governance
- MM-AI-11: release-gate policy and score thresholds are production blockers.
- MM-AI-12: official evaluator replay + artifact publication must be green before final promotion.

