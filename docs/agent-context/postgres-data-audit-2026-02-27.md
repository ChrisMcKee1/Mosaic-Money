# PostgreSQL Data Audit (2026-02-27)


## Agent Loading
- Load when: referencing the 2026-02-27 PostgreSQL baseline during data-quality investigations.
- Apply with workspace policy: [.github/copilot-instructions.md](../../.github/copilot-instructions.md)

## Status
- Date: 2026-02-27
- Environment: `pgsql/mosaic-connection/mosaicmoneydb`
- Audit mode: Read-only (no data mutations)
- Delegation: Chunked table analysis via `mosaic-money-backend` subagent (Chunk A, Chunk B)

## Scope
This audit focused on:
- Tables with zero rows.
- Non-empty tables with high null density.
- Text columns with empty/whitespace values.
- Classification of findings into:
  - expected now,
  - suspicious and likely broken/missing,
  - expected to be populated by agent/runtime later,
  - should be user- or platform-configurable.

## Methodology
1. Connected with PostgreSQL tools and loaded full schema context.
2. Collected exact row counts for all application tables in `public`.
3. Computed null counts/null percentages for columns in non-empty tables.
4. Computed blank/whitespace text-value rates.
5. Ran delegated chunk analysis (A/B) to classify discrepancies and prioritize actions.
6. Cross-checked current Web/Mobile/API surfaces for category/settings/admin capabilities.

## Table Inventory (Exact Row Counts)

### Non-empty tables
- `AccountAccessPolicyReviewQueueEntries`: 2
- `AccountMemberAccessEntries`: 10
- `Accounts`: 5
- `ClassificationStageOutputs`: 414
- `EnrichedTransactions`: 141
- `HouseholdUsers`: 2
- `Households`: 1
- `LiabilityAccounts`: 12
- `LiabilitySnapshots`: 3
- `MosaicUsers`: 2
- `PlaidItemCredentials`: 2
- `PlaidItemSyncStates`: 2
- `PlaidLinkSessionEvents`: 10
- `PlaidLinkSessions`: 2
- `RawTransactionIngestionRecords`: 138
- `TransactionClassificationOutcomes`: 138
- `TransactionEmbeddingQueueItems`: 138
- `__EFMigrationsHistory`: 8

### Empty tables
- `AgentDecisionAudit`: 0
- `AgentRunStages`: 0
- `AgentRuns`: 0
- `AgentSignals`: 0
- `Categories`: 0
- `IdempotencyKeys`: 0
- `InvestmentAccounts`: 0
- `InvestmentHoldingSnapshots`: 0
- `InvestmentTransactions`: 0
- `PlaidAccountLinks`: 0
- `RecurringItems`: 0
- `ReimbursementProposals`: 0
- `Subcategories`: 0
- `TransactionSplits`: 0

## High-Priority Discrepancies

1. Taxonomy baseline is missing.
- Evidence: `Categories = 0`, `Subcategories = 0`.
- Impact: categorization/routing cannot resolve concrete category assignments.

2. Proposed category/subcategory linkage is empty in active classification data.
- Evidence: `ClassificationStageOutputs.ProposedSubcategoryId = 414/414 null (100%)`.
- Evidence: `TransactionClassificationOutcomes.ProposedSubcategoryId = 138/138 null (100%)`.
- Impact: system is operating with ambiguity fallback but without category destination options.

3. Transaction category linkage is absent.
- Evidence: `EnrichedTransactions.SubcategoryId = 141/141 null (100%)`.
- Impact: transactions remain uncategorized, weakens budgeting/reporting and category analytics.

4. Needs-review assignment ownership gap.
- Evidence: `EnrichedTransactions.NeedsReviewByUserId = 141/141 null (100%)`.
- Impact: review queue ownership is unclear (no direct assignee), risking unresolved backlog.

5. Plaid correlation/link integrity gaps.
- Evidence: `PlaidAccountLinks = 0` while Plaid ingestion tables contain data.
- Evidence: `PlaidLinkSessions.LinkedItemId = 2/2 null (100%)` and `PlaidLinkSessions.HouseholdId = 2/2 null (100%)`.
- Evidence: `PlaidItemCredentials.LastLinkedSessionId = 2/2 null (100%)`.
- Impact: weak session-to-item/account traceability and harder recovery/reconciliation.

## Null Hotspots Likely Expected (Current State)
- `HouseholdUsers.RemovedAtUtc` 100% null with active memberships (expected by membership lifecycle).
- `PlaidItemSyncStates.LastSyncErrorAtUtc` and `LastSyncErrorCode` 100% null (healthy sync path).
- `TransactionEmbeddingQueueItems.DeadLetteredAtUtc` and `LastError` 100% null (no dead-letter/error observed).
- `RawTransactionIngestionRecords.LastReviewReason` 100% null can be expected if review rationale is not yet captured by pipeline (still worth design decision).
- `EnrichedTransactions.AgentNote` / `UserNote` high null rate (notes are optional and user-driven).

## Blank String Audit
No material empty/whitespace-only text issue was observed in populated text columns (`blank_pct = 0.00` across profiled columns).

## Chunked Delegation Summary

### Chunk A (core ledger + agent + taxonomy + investments)
- `Categories` classified as suspicious empty.
- Agent lifecycle tables (`AgentRuns`, `AgentRunStages`, `AgentSignals`, `AgentDecisionAudit`) classified as expected-empty until full runtime-agent path is active.
- Investments tables classified expected-empty in this environment unless investments lane is currently expected to be populated.

### Chunk B (plaid + outcomes + queue + recurring/reimbursement)
- `Subcategories` and `PlaidAccountLinks` classified suspicious-empty.
- Recurring/reimbursement/splits tables classified expected-empty unless those user/agent flows are considered already enabled for this env.
- Confirmed taxonomy gap is the dominant upstream blocker affecting multiple downstream null hotspots.

## Product and UX Findings

### Existing surfaces (verified)
- Web settings exists at `src/MosaicMoney.Web/app/settings/page.jsx` with tabs/cards for theme, security, household.
- Web categories page exists at `src/MosaicMoney.Web/app/categories/CategoriesClient.jsx` but is analytics/dashboard style with mock budget behavior and no category CRUD/reorder management.
- Mobile settings exists at `src/MosaicMoney.Mobile/src/features/settings/components/SettingsScreen.tsx` with appearance/security/household/account-link cards, but no category management screen.
- Mobile has a `CategoryPicker` (`src/MosaicMoney.Mobile/src/features/transactions/components/CategoryPicker.tsx`) for selection only.
- API exposes category search (`GET /search/categories`) via `src/MosaicMoney.Api/Apis/SearchEndpoints.cs`, but no dedicated category/subcategory management endpoints for create/update/reorder/reparent operations.

### Gap confirmation
- No dedicated user-facing category configuration workflow.
- No platform-admin static-table CRUD area.
- No API lane for category tree management and reparenting.

## Data Model Research Track: User-Level + Joint Categories

### Requirement direction from product discussion
- Category preferences should be user-level, not purely household-level.
- Support a joint/shared option for categories that both household members can use.
- Allow moving a subcategory from one parent category to another without data loss.

### Recommended modeling direction (research candidate)
1. Introduce category ownership scope.
- `owner_scope`: `System`, `User`, `JointHousehold`.
- `owner_user_id` nullable, `household_id` nullable, constrained by scope.

2. Split taxonomy templates from assignments.
- Keep platform/system taxonomy as immutable templates.
- Add user/joint overlays that can extend or remap templates.

3. Preserve historical truth when reparenting.
- Use effective-dated mapping for transaction->subcategory assignment or maintain assignment history table.
- Do not silently mutate prior financial semantics without auditability.

4. Keep fail-closed review behavior.
- If subcategory mapping is ambiguous/missing, continue routing to `NeedsReview`.

## Proposed Delivery Backlog (AP0 candidate)

### Phase 1: Data integrity and bootstrap
- Seed baseline `Categories` + `Subcategories` (system taxonomy).
- Backfill pipeline behavior checks to ensure `ProposedSubcategoryId` can populate when confidence allows.

### Phase 2: User category configuration UX
- Add `Settings -> Categories` (web/mobile) with:
  - create/edit/delete categories and subcategories,
  - reorder categories/subcategories,
  - move subcategory between parent categories,
  - scope toggle (`My`, `Joint`, optional `System` read-only visibility).

### Phase 3: Platform static-table CRUD
- Add internal operator/admin CRUD surface for platform-managed static tables (not user-owned), behind development/operator access control.

### Phase 4: API contracts
- Add category management endpoints with optimistic concurrency and audit metadata.
- Add explicit reparent operation endpoint with safeguards and dry-run impact preview.

## Suggested Advanced Research Questions
1. Should user-level taxonomies be household-overlaid or fully isolated with optional sharing links?
2. Should historical transactions retain old category mapping after reparent, or should mapping be time-versioned?
3. What minimal set of static tables require platform CRUD versus user CRUD?
4. How should cross-user conflict resolution work for joint category edits?
5. What migration strategy should map existing uncategorized history once taxonomy seeds exist?

## Read-Only SQL Checks for Revalidation
```sql
SELECT
    (SELECT COUNT(*) FROM public."Categories") AS categories_count,
    (SELECT COUNT(*) FROM public."Subcategories") AS subcategories_count;
```

```sql
SELECT
    COUNT(*) AS total_stage_outputs,
    COUNT(*) FILTER (WHERE "ProposedSubcategoryId" IS NULL) AS null_stage_proposals
FROM public."ClassificationStageOutputs";
```

```sql
SELECT
    COUNT(*) AS total_outcomes,
    COUNT(*) FILTER (WHERE "ProposedSubcategoryId" IS NULL) AS null_outcome_proposals
FROM public."TransactionClassificationOutcomes";
```

```sql
SELECT
    COUNT(*) AS total_transactions,
    COUNT(*) FILTER (WHERE "SubcategoryId" IS NULL) AS null_subcategory_links,
    COUNT(*) FILTER (WHERE "NeedsReviewByUserId" IS NULL) AS null_review_assignees
FROM public."EnrichedTransactions";
```

```sql
SELECT
    (SELECT COUNT(*) FROM public."PlaidAccountLinks") AS plaid_account_links_count,
    (SELECT COUNT(*) FROM public."PlaidLinkSessions" WHERE "LinkedItemId" IS NULL) AS link_sessions_without_item,
    (SELECT COUNT(*) FROM public."PlaidItemCredentials" WHERE "LastLinkedSessionId" IS NULL) AS credentials_without_last_linked_session;
```

