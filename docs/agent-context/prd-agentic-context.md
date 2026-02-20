# PRD Agentic Context

This file is the planner-friendly grounding summary for Mosaic Money.
Canonical source: [Full PRD](../../project-plan/PRD.md)

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

## AI Escalation Order
1. Deterministic rules.
2. PostgreSQL semantic operators (`azure_ai`, `pgvector`).
3. MAF graph workflow for ambiguous or complex cases.
