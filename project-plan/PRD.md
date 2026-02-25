# Mosaic Money PRD

## 1. Product Intent
Mosaic Money is a cloud-first, single-entry personal finance system for a two-user household with commingled finances. It combines deterministic financial logic, review-safe AI assistance, and clear household ownership controls without allowing autonomous high-impact actions.

## 2. Non-Negotiable Constraints
- Single-entry ledger model only.
- Keep `UserNote` and `AgentNote` as separate persisted fields.
- Ambiguous outcomes must fail closed into `NeedsReview`.
- No autonomous external messaging. Draft-only assistance is allowed.
- Projection logic (amortization/safe-to-spend) cannot mutate ledger truth.

## 3. Primary User Journeys
1. Connect institutions with Plaid Link from web or mobile while keeping secrets server-side.
2. Review and resolve `NeedsReview` items with explicit approve/reject/reclassify intent.
3. Ask Copilot for context and receive concise, auditable `AgentNote` summaries.
4. Understand future cash pressure from recurring obligations, reimbursements, and net-worth trend lines.
5. Manage household membership, invites, and account visibility boundaries.

## 4. Functional Modules

### Module A: Ledger Truth And Ingestion
- Raw payloads are persisted and idempotently upserted into enriched transaction records.
- Plaid transaction history depth is explicitly requested and bounded (`30..730`, default `730`).
- Ingestion remains duplicate-safe and preserves user-entered notes.

### Module B: Semantic Retrieval And Search
- Semantic embeddings are generated asynchronously after writes.
- Web and mobile support semantic transaction/category discovery through search endpoints.
- Retrieval is local to PostgreSQL (`pgvector` and `azure_ai` extension path), not remote historical re-query.

### Module C: Recurring, Reimbursements, And Projections
- Recurring matching supports date drift and amount variance with review-safe fallbacks.
- Reimbursement linking requires explicit user confirmation.
- Projection metadata is read-only and rendered in clients without mutating source transactions.

### Module D: Plaid Onboarding
- Web and mobile both use backend-issued `link_token` and server-side `public_token` exchange.
- Secrets (`client_id`, `secret`, `access_token`) remain backend-only.
- Web currently includes a demo/simulated UI path in onboarding and is not yet full production Link SDK parity.

### Module E: Identity, Household Access, And ACL
- First-class `MosaicUsers` identity model and household membership lifecycle are in scope.
- Account-level ACL model supports owner/read-only/hidden visibility states.
- Authorization rollout is fail-closed, with explicit backfill and review queues for ambiguous policy defaults.

### Module F: AI Escalation And Safety
- Escalation order is deterministic rules -> semantic retrieval -> MAF fallback.
- MAF participation is bounded and review-safe; unresolved ambiguity remains `NeedsReview`.
- External messaging action families are denied, logged, and treated as release-blocking if violated.

### Module G: AI Quality Gates And Replay
- MM-AI-11 defines deterministic release-blocking criteria.
- MM-AI-12 adds official evaluator replay artifacts and source-linked rerun guidance.
- MM-AI-12 is not `Done` until cloud evaluator evidence is captured and reproducible in CI.

## 5. Operational Modes
- Full-stack local mode: AppHost provisions local runtime dependencies and service references.
- External database mode: AppHost consumes `ConnectionStrings:mosaicmoneydb` and skips local DB provisioning.
- DB-only Azure mode: `src/apphost.database/apphost.cs` provisions PostgreSQL-focused infrastructure separately.

## 6. Current Documentation Gates
- Specs and project board status must stay synchronized.
- M5/M6/M7 docs must reference current acceptance evidence and known remaining gaps.
- Architecture diagrams and data models are maintained in split documents under `docs/architecture/` and `docs/data-models/`.