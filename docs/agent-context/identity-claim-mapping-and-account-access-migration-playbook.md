# MM-ASP-08 and MM-ASP-09 Playbook


## Agent Loading
- Load when: changing identity claim mapping, household-member resolution, or account-access migration behavior.
- Apply with workspace policy: [.github/copilot-instructions.md](../../.github/copilot-instructions.md)

## Purpose
This playbook is the implementation runbook for:
- `MM-ASP-08`: Identity claim mapping configuration documented and reproducible across AppHost/API/Web/Mobile for local and CI.
- `MM-ASP-09`: Migration rollout and rollback playbook for account access migration.

For a concrete two-user Clerk sample persona workflow with household bootstrap and validation evidence, see `docs/agent-context/clerk-sample-users-household-validation-runbook.md`.

## Source-Of-Truth Files
- `src/apphost.cs`
- `src/MosaicMoney.Api/Program.cs`
- `src/MosaicMoney.Api/Apis/TransactionsEndpoints.cs`
- `src/MosaicMoney.Api/Migrations/20260225190147_AddMosaicUserAndHouseholdMembershipLifecycle.cs`
- `src/MosaicMoney.Api/Migrations/20260225192826_AddAccountMemberAccessAclModel.cs`
- `src/MosaicMoney.Api/Migrations/20260225194514_AddPlaidAccountLinkMapping.cs`
- `src/MosaicMoney.Api/Migrations/20260225202720_AddAccountAccessPolicyBackfillReviewQueue.cs`
- `src/MosaicMoney.Api/Domain/Ledger/AccessPolicy/AccountAccessPolicyBackfillService.cs`
- `src/MosaicMoney.Web/lib/api.js`
- `src/MosaicMoney.Web/.env.example`
- `src/MosaicMoney.Mobile/src/shared/services/mobileApiClient.ts`
- `src/MosaicMoney.Mobile/.env.example`
- `src/MosaicMoney.Web/playwright.config.mjs`

## Identity Claim Mapping Contract
API member-context resolution for account/transaction reads is implemented in `src/MosaicMoney.Api/Apis/TransactionsEndpoints.cs`.

Resolution order:
1. Claim `mosaic_household_user_id`
2. Claim `household_user_id`
3. Header `X-Mosaic-Household-User-Id`

The resolved value must be a GUID and must map to an active household membership (`MembershipStatus == Active`), or the API returns fail-closed `401/403` responses.

Web and mobile do not mint server identity claims in local runs. For deterministic local and CI testing, inject household-member context through `X-Mosaic-Household-User-Id`:
- Web server-side calls: Clerk bearer token is forwarded automatically by `src/MosaicMoney.Web/lib/api.js`, and env `MOSAIC_HOUSEHOLD_USER_ID` supplies `X-Mosaic-Household-User-Id`
- Mobile calls: Clerk bearer token is forwarded by `src/MosaicMoney.Mobile/src/shared/services/mobileApiClient.ts` (provider configured in `src/MosaicMoney.Mobile/app/_layout.tsx`), and env `EXPO_PUBLIC_MOSAIC_HOUSEHOLD_USER_ID` supplies `X-Mosaic-Household-User-Id`

## Local Mapping Steps (AppHost + API + Web + Mobile)
1. Start the stack.
```powershell
dotnet build src/apphost.cs
aspire run --project src/apphost.cs --detach
aspire wait api --project src/apphost.cs --status healthy --timeout 180
```
2. Resolve an active household member ID from PostgreSQL.
```sql
SELECT "Id"
FROM "HouseholdUsers"
WHERE "MembershipStatus" = 1
ORDER BY "Id"
LIMIT 1;
```
3. Web local mapping.
```powershell
cd src/MosaicMoney.Web
copy .env.example .env.local
# Set MOSAIC_HOUSEHOLD_USER_ID=<active-household-user-guid> in .env.local
npm install
npm run dev
```
4. Mobile local mapping.
```powershell
cd src/MosaicMoney.Mobile
copy .env.example .env.local
# Set EXPO_PUBLIC_MOSAIC_HOUSEHOLD_USER_ID=<active-household-user-guid> in .env.local
npm install
npm run start:lan
```
5. AppHost orchestration role.
- `src/apphost.cs` keeps endpoint wiring reference-driven (`WithReference(api)`), so Web resolves API endpoints via Aspire-injected service env vars.
- API migration and backfill run on API startup in `ApplyMigrationsAsync` (`src/MosaicMoney.Api/Program.cs`).

## CI Mapping Steps
1. Provide household-member mapping env in CI jobs that execute Web or Mobile API-integrated tests.
- Web job env: `MOSAIC_HOUSEHOLD_USER_ID`
- Mobile job env: `EXPO_PUBLIC_MOSAIC_HOUSEHOLD_USER_ID`
2. Keep API base URL configuration non-secret.
- Web CI uses `API_URL` (example already wired in `src/MosaicMoney.Web/playwright.config.mjs`).
- Mobile CI uses `EXPO_PUBLIC_API_BASE_URL`.
3. Validate fail-closed behavior in CI.
- One request without header/claims must return `401` (`member_context_required`).
- One request with valid mapped household member ID must succeed.

## Secret-Handling And Environment Injection Notes
1. Treat member mapping IDs as environment-scoped operational config, not source-controlled values.
2. Never commit `.env`, `.env.local`, or any real credentials.
3. Keep orchestration-level secrets in AppHost user-secrets.
Project-based AppHost commands:
```powershell
dotnet user-secrets init
dotnet user-secrets set "<Key>" "<Value>"
dotnet user-secrets list
```
File-based AppHost commands (`src/apphost.cs`):
```powershell
dotnet user-secrets set "<Key>" "<Value>" --file src/apphost.cs
dotnet user-secrets list --file src/apphost.cs
```
4. Keep private values out of browser-visible keys. `NEXT_PUBLIC_*` and `EXPO_PUBLIC_*` are public by design.

## Migration Rollout Playbook (MM-ASP-09)
Target migration chain:
1. `20260225190147_AddMosaicUserAndHouseholdMembershipLifecycle`
2. `20260225192826_AddAccountMemberAccessAclModel`
3. `20260225194514_AddPlaidAccountLinkMapping`
4. `20260225202720_AddAccountAccessPolicyBackfillReviewQueue`

Rollout steps:
1. Preflight tests.
```powershell
dotnet test src/MosaicMoney.Api.Tests/MosaicMoney.Api.Tests.csproj --filter "FullyQualifiedName~IdentityMembershipModelContractTests|FullyQualifiedName~AccountAccessPolicyBackfillServiceTests|FullyQualifiedName~AccountAccessPolicyReviewQueueModelContractTests|FullyQualifiedName~AccountMemberAccessModelContractTests|FullyQualifiedName~TransactionProjectionMetadataQueryServiceTests"
```
2. Capture migration baseline.
```sql
SELECT "MigrationId"
FROM "__EFMigrationsHistory"
ORDER BY "MigrationId";
```
3. Roll forward by starting upgraded API (startup applies migrations + backfill through `ApplyMigrationsAsync` in `src/MosaicMoney.Api/Program.cs`).
4. Verify migration landed.
```sql
SELECT "MigrationId"
FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20260225202720_AddAccountAccessPolicyBackfillReviewQueue';
```
5. Verify backfill outcomes.
```sql
SELECT COUNT(*) AS active_accounts
FROM "Accounts"
WHERE "IsActive" = TRUE;
```
```sql
SELECT COUNT(*) AS access_entries
FROM "AccountMemberAccessEntries";
```
```sql
SELECT COUNT(*) AS review_flagged_accounts
FROM "Accounts"
WHERE "AccessPolicyNeedsReview" = TRUE;
```
```sql
SELECT COUNT(*) AS open_review_queue_entries
FROM "AccountAccessPolicyReviewQueueEntries"
WHERE "ResolvedAtUtc" IS NULL;
```
6. Verify API access enforcement.
- No member context -> `401 member_context_required`
- Invalid GUID -> `401 member_context_invalid`
- Inactive member -> `403 membership_access_denied`
- Active mapped member -> `200` for `/api/v1/transactions`

## Rollback Playbook
Always take a DB backup before schema rollback.

```powershell
pg_dump --format=custom --file mosaicmoney-pre-rollback.dump <connection-string-or-flags>
```

Rollback mode A (rollback only review-queue migration):
1. Stop the running stack.
```powershell
aspire stop --project src/apphost.cs
```
2. Roll DB back one migration.
```powershell
dotnet ef database update 20260225194514_AddPlaidAccountLinkMapping --project src/MosaicMoney.Api/MosaicMoney.Api.csproj --startup-project src/MosaicMoney.Api/MosaicMoney.Api.csproj
```
3. Redeploy/restart API version that does not require `AccountAccessPolicyReviewQueueEntries`.
4. Verify:
```sql
SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";
```

Rollback mode B (rollback full M7 identity/access schema):
1. Stop stack and deploy previous application version.
2. Roll DB back to pre-M7 boundary.
```powershell
dotnet ef database update 20260224211700_AddInvestmentsAndRecurringEnrichment --project src/MosaicMoney.Api/MosaicMoney.Api.csproj --startup-project src/MosaicMoney.Api/MosaicMoney.Api.csproj
```
3. Verify M7 tables/columns are removed according to migration `Down` methods.
4. Run smoke tests against previous version.

## Rollout Gate Checklist
1. Targeted identity/access tests pass.
2. `__EFMigrationsHistory` reflects expected migration state.
3. Backfill metrics and review queue counts are captured.
4. API member-context fail-closed checks are captured.
5. Rollback command path tested in a non-production environment with evidence.

