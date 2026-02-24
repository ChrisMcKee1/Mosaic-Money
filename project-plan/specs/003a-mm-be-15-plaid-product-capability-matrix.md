# MM-BE-15 Plaid Product Capability Matrix

## Status
- Task: `MM-BE-15`
- Milestone: M2
- Date: 2026-02-23
- Owner: `mosaic-money-planner`
- Scope: Product-capability gate for expanding beyond `transactions`
- Review: Planner-approved on 2026-02-23 (`MM-BE-15` set to `Done`)

## Decision Key
- `Adopt Now`: Required for current MVP outcomes and implementation should proceed in the next active slices.
- `Adopt Later`: Valuable but deferred until post-MVP or after current release gates.
- `Out of Scope`: Not targeted for MVP; revisit only with explicit PRD or governance update.

## Source-Linked Capability Matrix
| PRD Need | Plaid Product | Endpoint and Webhook Contract | Sandbox Simulation Coverage | Required Backend Schema Fields | Required API Contracts | Review and HITL Implications | MVP Decision | Rationale and Risks | Sources |
|---|---|---|---|---|---|---|---|---|---|
| Import account and transaction truth, then route ambiguity to `NeedsReview` | Transactions | Endpoints: `/transactions/sync`, `/transactions/refresh` (optional operator refresh), `/transactions/recurring/get` (optional). Webhooks: `SYNC_UPDATES_AVAILABLE` as primary for sync flow. | Validated for dynamic test data with `user_transactions_dynamic`, `/sandbox/transactions/create`, `/sandbox/item/fire_webhook`, and `/transactions/refresh`. | Add persisted sync state keyed by `(PlaidEnvironment, ItemId)`: `Cursor`, `InitialUpdateComplete`, `HistoricalUpdateComplete`, `LastSyncedAtUtc`, `LastWebhookAtUtc`, `LastProviderRequestId`, `LastSyncErrorCode`, `LastSyncErrorAtUtc`. Keep existing raw/enriched idempotency paths. | Add internal worker contract for pull-and-upsert by Item/cursor and webhook-trigger enqueue contract. Keep existing ingestion endpoint for controlled test harnessing only. | Fail closed when sync payload confidence or mapping is ambiguous. Any unknown state transition routes to `NeedsReview`. | Adopt Now | Core MVP dependency. Primary risk is duplicate or missed updates if cursor state is not durable and webhook handling is not idempotent. | `project-plan/PRD.md`; `project-plan/architecture.md`; https://plaid.com/docs/api/products/transactions; https://plaid.com/docs/transactions/sync-migration; https://plaid.com/docs/sandbox/test-credentials; https://plaid.com/docs/api/sandbox |
| Web and mobile OAuth-safe Link onboarding with backend-only secret handling | Link token and Item exchange lifecycle (cross-product foundation) | Endpoints: `/link/token/create`, `/item/public_token/exchange` via backend provider abstraction. OAuth redirect URI rules apply to Link sessions. Webhook dependencies follow per-product webhooks after Item creation. | Validated: sandbox supports localhost HTTP redirect URI exception; production requires HTTPS redirect URIs. Mobile app-to-app and webview reinit rules documented. | Existing `PlaidLinkSessions` and `PlaidItemCredentials` are present. Add `RequestedProductSetVersion` and `OAuthReinitializeRequired` flags if flow branching expands. | Keep server-only token issuance and exchange endpoints. Add explicit redirect-reinitialize metadata contract for web and mobile clients. | High-impact auth and account-link failures must not auto-resolve. Recovery continues to `requires_update_mode`, `requires_relink`, or `NeedsReview`. | Adopt Now | Already in-progress and required for secure ingestion. Risk is OAuth regressions across mobile/web contexts if redirect and reinit semantics drift. | `project-plan/specs/003-m2-ledger-truth-and-review-core.md`; https://plaid.com/docs/link/oauth; https://plaid.com/docs/api/link |
| Improve recurring/subscription forecasting accuracy while preserving single-entry truth | Transactions recurring signals | Endpoint: `/transactions/recurring/get`. Webhook: `RECURRING_TRANSACTIONS_UPDATE` (if enabled for product flow). | Partially validated through transactions sandbox pathways; dedicated recurring webhook behavior is documented but not yet executed in local integration tests. | Add optional enrichment fields (nullable) on recurring templates or mappings: `PlaidRecurringStreamId`, `PlaidRecurringConfidence`, `PlaidRecurringLastSeenAtUtc`, `RecurringSource` (`deterministic` or `plaid`). | Add read-only recurring enrichment contract so UI can compare deterministic vs Plaid hints without mutating ledger truth. | Conflicts between deterministic and Plaid-derived recurring links route to `NeedsReview`; never auto-overwrite approved user decisions. | Adopt Later | Useful for recall/precision tuning, but deterministic recurring matcher already exists and is the safer MVP baseline. | `project-plan/PRD.md`; `project-plan/architecture.md`; https://plaid.com/docs/api/products/transactions |
| Deep data visibility for account identity and ownership troubleshooting | Identity | Endpoints: `/identity/get`, `/identity/match`. No dedicated Item-level Identity webhook identified in current API product reference. | Partially validated via generic sandbox credentials; no dedicated local simulation pass executed yet. | Add optional PII-scoped identity snapshot tables with strict retention metadata: `IdentitySnapshot`, `IdentityField`, `CapturedAtUtc`, `SourceItemId`, `RetentionExpiresAtUtc`. | Add backend-only identity fetch endpoint and admin-only diagnostics contract; do not expose raw PII broadly to web/mobile surfaces. | Any identity mismatch affecting ownership attribution requires human review and explicit user confirmation before applying ownership changes. | Adopt Later | Helps with ownership diagnostics but not required for MVP transaction review loop. Risk is PII expansion and retention obligations. | `project-plan/PRD.md`; https://plaid.com/docs/api/products/identity |
| Debt and obligation visibility for future projection enhancements | Liabilities | Endpoint: `/liabilities/get`. Webhook: `LIABILITIES` `DEFAULT_UPDATE`. | Backend lane is implemented and validated end-to-end with sandbox token create -> backend exchange -> liabilities webhook ingestion -> API retrieval proof. | Persist liabilities snapshot model: `LiabilityAccount`, `LiabilitySnapshot`, `LiabilityType`, `MinimumPayment`, `APR`, `NextPaymentDueDate`, `AsOfUtc`. | Expose read-only liabilities APIs for projection modules; prohibit direct mutation from clients. | Liability-driven projection changes are high-impact; route uncertain mapping and account-link conflicts to `NeedsReview`. | Adopt Now | Required to progress toward 360 backend coverage with controlled, read-only contracts and idempotent replay behavior. | `project-plan/PRD.md`; https://plaid.com/docs/api/products/liabilities; https://plaid.com/docs/sandbox |
| Architect persona long-range visibility into non-deposit assets | Investments | Endpoints: `/investments/holdings/get`, `/investments/transactions/get`, `/investments/refresh`. Webhooks: `HOLDINGS: DEFAULT_UPDATE`, `INVESTMENTS_TRANSACTIONS: DEFAULT_UPDATE`, `INVESTMENTS_TRANSACTIONS: HISTORICAL_UPDATE`. | Partially validated. Investments sandbox is available and supports test data, but no Mosaic ingestion contract exists. | Add dedicated investment tables to avoid polluting checking-account ledger semantics: `InvestmentAccount`, `InvestmentHoldingSnapshot`, `InvestmentTransaction`. | Add separate API surface for investment read models and snapshots. Keep core budget ledger distinct from investment books. | Avoid mixing investment market movements with spend/income ledger classification. Ambiguous cross-domain categorizations require review. | Adopt Later | Valuable for future analytics, but introduces substantial schema and UX complexity beyond MVP. | `project-plan/PRD.md`; https://plaid.com/docs/api/products/investments; https://plaid.com/docs/investments |
| Income verification and employment workflows | Income | Endpoints: `/credit/bank_income/get`, `/credit/payroll_income/get`, `/credit/employment/get` and related refresh endpoints. Webhooks include `INCOME_VERIFICATION` and related income codes. | Partially validated with `/sandbox/income/fire_webhook`. Full workflow and report handling not exercised locally. | Would require new document/verification state models with strict retention and audit requirements: `IncomeVerification`, `IncomeReport`, `IncomeWebhookEvent`. | New verification-specific endpoints and admin workflows required; not part of existing transaction-review APIs. | Income verification outcomes must remain human-reviewed before any downstream money action; no automatic external action path allowed. | Out of Scope | Not required for MVP budgeting/review loop and significantly expands compliance and data-handling scope. | `project-plan/PRD.md`; https://plaid.com/docs/api/products/income; https://plaid.com/docs/api/sandbox |
| Downloadable bank statement workflows | Statements | Endpoints: `/statements/list`, `/statements/download`, `/statements/refresh`. Webhook: `STATEMENTS_REFRESH_COMPLETE`. | Not yet validated in local sandbox flow; docs confirm endpoint and webhook contract. | Would require document storage and retention model: `StatementDocument`, `StatementExtractionRun`, `StorageUri`, `Checksum`, `RetentionExpiresAtUtc`. | New statement retrieval/export API boundary required, including controlled access and audit logging. | Statement extraction and document handling introduce PII risk; any automated interpretation must route uncertain outcomes to review. | Out of Scope | Not in current PRD MVP outcomes and adds heavy document-handling complexity. | `project-plan/PRD.md`; https://plaid.com/docs/api/products/statements |
| ACH-style payment setup through verified account/routing details | Auth | Endpoints: `/auth/get`, `/bank_transfer/event/list`, `/bank_transfer/event/sync`. Webhooks include `DEFAULT_UPDATE`, `AUTOMATICALLY_VERIFIED`, `VERIFICATION_EXPIRED`, `BANK_TRANSFERS_EVENTS_UPDATE`, `SMS_MICRODEPOSITS_VERIFICATION`. | Partially validated via docs and sandbox verification simulation endpoints. Not integrated in current Mosaic flows. | Add secure account verification state and masked routing metadata only where needed; do not persist unneeded sensitive data. | Introduce dedicated payment-verification API contracts separate from ledger categorization endpoints. | Any payment-verification failure or stale verification status should block downstream money action and require explicit user intervention. | Adopt Later | Not needed for current MVP use cases but likely useful for future transfer or payout scenarios. | https://plaid.com/docs/api/products/auth; https://plaid.com/docs/auth/coverage/microdeposit-events |

## Implementation Order and Migration Plan
1. Complete transactions sync lane first.
Add durable per-Item cursor state and webhook-triggered worker polling, then prove idempotent replay safety against repeated webhook and refresh events.

2. Keep recurring enrichment behind a feature flag.
Implement optional Plaid recurring hints only after deterministic recurring baseline remains stable and measurable.

3. Add one non-transaction product at a time (later milestones).
Recommended order from this point: `Identity` then `Investments` then `Auth`, each behind explicit schema migration and API contract slices.

4. Exclude document-heavy lanes from MVP.
Defer `Income` and `Statements` until explicit PRD update, retention model approval, and compliance review are complete.

5. Enforce migration safety checks for every product-lane expansion.
Require: migration rollback plan, sandbox simulation script, webhook replay test, and `NeedsReview` policy assertions before promotion.

## Dependency-Based Delegation Notes
- `mosaic-money-backend`
Implement `PlaidItemSyncState` schema and cursor-aware worker ingestion orchestration; add webhook receiver path for `SYNC_UPDATES_AVAILABLE`.
- `mosaic-money-devops`
Validate AppHost secret wiring for Plaid credentials and webhook endpoints using `AddParameter(..., secret: true)` and user-secrets flows.
- `mosaic-money-frontend`
Wire OAuth redirect/reinitialize behavior for web onboarding and keep all token exchange server-side.
- `mosaic-money-mobile`
Validate React Native Link re-entry and status UX, including safe recovery prompts for update-mode and relink.
- `mosaic-money-ai`
Define deterministic policy for recurring-signal conflicts and mandatory `NeedsReview` routing for ambiguous cross-source classifications.

## Explicit Gate Outcome
- Product lanes approved for immediate expansion: `Transactions`, `Liabilities`.
- Product lanes intentionally deferred: `Transactions recurring`, `Identity`, `Investments`, `Auth`.
- Product lanes removed from MVP scope: `Income`, `Statements`.

No additional non-`transactions` ingestion lane should be implemented until the corresponding `Adopt Later` lane is promoted through spec update, schema plan, and review sign-off.

## Backend Coverage Audit (2026-02-24)

### Verified Implemented Now
- Plaid onboarding flow is active for `transactions`: `/link/token/create`, `/item/public_token/exchange`, and `TRANSACTIONS:SYNC_UPDATES_AVAILABLE` webhook handling.
- Durable persistence is verified for `PlaidItemCredentials`, `PlaidItemSyncStates`, `RawTransactionIngestionRecords`, and `EnrichedTransactions`.
- Ingestion path remains idempotent and fail-closed for unmapped account deltas.
- Embedding queue integration is wired from sync ingestion and runs asynchronously.
- Deterministic/semantic/MAF classification orchestration is available via existing classification endpoints and persisted outcomes.
- Liabilities lane is implemented end-to-end in backend contracts: `/liabilities/get` provider support, `LIABILITIES:DEFAULT_UPDATE` webhook endpoint, `LiabilityAccount`/`LiabilitySnapshot` persistence, and read-only liabilities APIs.
- Liabilities evidence gate is captured for sandbox flow: Plaid sandbox public token create (`liabilities`,`transactions`) -> backend exchange -> liabilities webhook processing (`accountsUpsertedCount=12`, `snapshotsInsertedCount=3`) -> liabilities retrieval endpoints and database row-count deltas.

### Not Yet Implemented (Backend 360 Gap)
- No backend schema or ingestion lanes yet for `Identity`, `Investments`, or `Auth` product payloads.
- No `TRANSACTIONS:RECURRING_TRANSACTIONS_UPDATE` webhook lane or `/transactions/recurring/get` persistence lane.
- No backend schema or ingestion lanes for `Income` and `Statements` (intentionally out of MVP scope).

### Required To Reach Full 360 Financial View
1. Promote each deferred product lane (`Identity`, `Investments`, `Auth`, and optional recurring lane) through spec status change from `Adopt Later` to active implementation.
2. Add per-product schema slices and idempotent ingestion contracts with webhook replay tests.
3. Extend `IPlaidTokenProvider` and provider implementations with product-specific endpoints.
4. Add backend read APIs for newly persisted product models while keeping secrets server-only and preserving single-entry ledger semantics.
5. Run end-to-end sandbox evidence gates for each lane (webhook -> persistence -> retrieval) before promoting statuses to `Done`.

Update note (2026-02-24): Liabilities backend lane is validated with full sandbox evidence using real provider wiring and persisted row-count deltas for `LiabilityAccounts` and `LiabilitySnapshots`, in addition to successful liabilities retrieval API responses.
