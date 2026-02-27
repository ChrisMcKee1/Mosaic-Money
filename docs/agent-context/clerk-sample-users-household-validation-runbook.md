# Clerk Sample Users Household Validation Runbook (M8)

## Purpose
This runbook defines a repeatable local workflow for creating two sample Clerk users (for example, spouse/partner personas), mapping them to a single Mosaic Money household, and validating the setup across database and UI flows.

This document is intended for end-to-end confidence checks before promoting M8 identity/household work to `Done`.

## Scope and current constraints
1. API v1 routes are protected by JWT bearer authentication.
2. Local web and mobile API integrations currently use household-member context mapping via:
   - `MOSAIC_HOUSEHOLD_USER_ID` (web server-side)
   - `EXPO_PUBLIC_MOSAIC_HOUSEHOLD_USER_ID` (mobile)
3. Because the member-context mapping is environment-driven, validating each sample persona in web/mobile requires switching the mapped household-member ID and restarting that client process.

For mapping contract details, see `docs/agent-context/identity-claim-mapping-and-account-access-migration-playbook.md`.

## Prerequisites
1. Clerk parameters are configured through AppHost user-secrets:
   - `Parameters:clerk-publishable-key`
   - `Parameters:clerk-secret-key`
   - `Parameters:clerk-issuer`
2. Stack is running and healthy:
```powershell
dotnet build src/apphost.cs
aspire run --project src/apphost.cs --detach
aspire wait api --project src/apphost.cs --status healthy --timeout 180
aspire wait web --project src/apphost.cs --status up --timeout 180
```
3. PostgreSQL access is available for executing SQL and validation queries.

## Sample persona template
Use two deterministic personas for household validation:

| Persona | Suggested Email | Clerk Subject (`sub`) | Household Member ID |
| --- | --- | --- | --- |
| Partner A | `sample.partner.a@local.test` | `<CLERK_SUB_A>` | `<HOUSEHOLD_USER_ID_A>` |
| Partner B | `sample.partner.b@local.test` | `<CLERK_SUB_B>` | `<HOUSEHOLD_USER_ID_B>` |

Notes:
- In Clerk, record each user's stable user identifier (`user_...`) and use that as `sub`.
- Keep these users in your Clerk test/development tenant only.

## Step 1: Create sample users in Clerk
1. Open Clerk Dashboard -> `Users`.
2. Create Partner A and Partner B users (email/password or Microsoft SSO).
3. Record each Clerk user ID (`sub`) for database mapping.

## Step 2: Bootstrap household and identity mapping in PostgreSQL
Replace the placeholders before running:
- `<HOUSEHOLD_ID>`: target household GUID
- `<HOUSEHOLD_NAME>`: display name (example `Sample Household`)
- `<CLERK_SUB_A>`, `<CLERK_SUB_B>`
- `<EMAIL_A>`, `<EMAIL_B>`
- `<DISPLAY_NAME_A>`, `<DISPLAY_NAME_B>`
- `<MOSAIC_USER_ID_A>`, `<MOSAIC_USER_ID_B>`
- `<HOUSEHOLD_USER_ID_A>`, `<HOUSEHOLD_USER_ID_B>`

```sql
-- 1) Ensure household exists.
INSERT INTO "Households" ("Id", "Name", "CreatedAtUtc")
VALUES ('<HOUSEHOLD_ID>'::uuid, '<HOUSEHOLD_NAME>', NOW())
ON CONFLICT ("Id") DO UPDATE
SET "Name" = EXCLUDED."Name";

-- 2) Upsert mapped Mosaic users for Clerk subjects.
INSERT INTO "MosaicUsers"
(
    "Id",
    "AuthProvider",
    "AuthSubject",
    "Email",
    "DisplayName",
    "IsActive",
    "CreatedAtUtc",
    "LastSeenAtUtc"
)
VALUES
(
    '<MOSAIC_USER_ID_A>'::uuid,
    'clerk',
    '<CLERK_SUB_A>',
    '<EMAIL_A>',
    '<DISPLAY_NAME_A>',
    TRUE,
    NOW(),
    NOW()
),
(
    '<MOSAIC_USER_ID_B>'::uuid,
    'clerk',
    '<CLERK_SUB_B>',
    '<EMAIL_B>',
    '<DISPLAY_NAME_B>',
    TRUE,
    NOW(),
    NOW()
)
ON CONFLICT ("AuthProvider", "AuthSubject") DO UPDATE
SET
    "Email" = EXCLUDED."Email",
    "DisplayName" = EXCLUDED."DisplayName",
    "IsActive" = TRUE,
    "LastSeenAtUtc" = NOW();

-- 3) Normalize Partner A household membership.
UPDATE "HouseholdUsers"
SET
    "MosaicUserId" = mu."Id",
    "DisplayName" = '<DISPLAY_NAME_A>',
    "ExternalUserKey" = LOWER('<EMAIL_A>'),
    "MembershipStatus" = 1,
    "InvitedAtUtc" = COALESCE("InvitedAtUtc", NOW()),
    "ActivatedAtUtc" = COALESCE("ActivatedAtUtc", NOW()),
    "RemovedAtUtc" = NULL
FROM "MosaicUsers" mu
WHERE mu."AuthProvider" = 'clerk'
  AND mu."AuthSubject" = '<CLERK_SUB_A>'
  AND "HouseholdUsers"."HouseholdId" = '<HOUSEHOLD_ID>'::uuid
  AND (
      "HouseholdUsers"."MosaicUserId" = mu."Id"
      OR "HouseholdUsers"."ExternalUserKey" = LOWER('<EMAIL_A>')
  );

INSERT INTO "HouseholdUsers"
(
    "Id",
    "HouseholdId",
    "MosaicUserId",
    "DisplayName",
    "ExternalUserKey",
    "MembershipStatus",
    "InvitedAtUtc",
    "ActivatedAtUtc",
    "RemovedAtUtc"
)
SELECT
    '<HOUSEHOLD_USER_ID_A>'::uuid,
    '<HOUSEHOLD_ID>'::uuid,
    mu."Id",
    '<DISPLAY_NAME_A>',
    LOWER('<EMAIL_A>'),
    1,
    NOW(),
    NOW(),
    NULL
FROM "MosaicUsers" mu
WHERE mu."AuthProvider" = 'clerk'
  AND mu."AuthSubject" = '<CLERK_SUB_A>'
  AND NOT EXISTS
  (
      SELECT 1
      FROM "HouseholdUsers" hu
      WHERE hu."HouseholdId" = '<HOUSEHOLD_ID>'::uuid
        AND hu."MosaicUserId" = mu."Id"
        AND hu."MembershipStatus" = 1
  );

-- 4) Normalize Partner B household membership.
UPDATE "HouseholdUsers"
SET
    "MosaicUserId" = mu."Id",
    "DisplayName" = '<DISPLAY_NAME_B>',
    "ExternalUserKey" = LOWER('<EMAIL_B>'),
    "MembershipStatus" = 1,
    "InvitedAtUtc" = COALESCE("InvitedAtUtc", NOW()),
    "ActivatedAtUtc" = COALESCE("ActivatedAtUtc", NOW()),
    "RemovedAtUtc" = NULL
FROM "MosaicUsers" mu
WHERE mu."AuthProvider" = 'clerk'
  AND mu."AuthSubject" = '<CLERK_SUB_B>'
  AND "HouseholdUsers"."HouseholdId" = '<HOUSEHOLD_ID>'::uuid
  AND (
      "HouseholdUsers"."MosaicUserId" = mu."Id"
      OR "HouseholdUsers"."ExternalUserKey" = LOWER('<EMAIL_B>')
  );

INSERT INTO "HouseholdUsers"
(
    "Id",
    "HouseholdId",
    "MosaicUserId",
    "DisplayName",
    "ExternalUserKey",
    "MembershipStatus",
    "InvitedAtUtc",
    "ActivatedAtUtc",
    "RemovedAtUtc"
)
SELECT
    '<HOUSEHOLD_USER_ID_B>'::uuid,
    '<HOUSEHOLD_ID>'::uuid,
    mu."Id",
    '<DISPLAY_NAME_B>',
    LOWER('<EMAIL_B>'),
    1,
    NOW(),
    NOW(),
    NULL
FROM "MosaicUsers" mu
WHERE mu."AuthProvider" = 'clerk'
  AND mu."AuthSubject" = '<CLERK_SUB_B>'
  AND NOT EXISTS
  (
      SELECT 1
      FROM "HouseholdUsers" hu
      WHERE hu."HouseholdId" = '<HOUSEHOLD_ID>'::uuid
        AND hu."MosaicUserId" = mu."Id"
        AND hu."MembershipStatus" = 1
  );
```

## Step 3: Optional account-sharing bootstrap for household ACL checks
Use this only if the household already has active accounts.

```sql
INSERT INTO "AccountMemberAccessEntries"
(
    "AccountId",
    "HouseholdUserId",
    "AccessRole",
    "Visibility",
    "GrantedAtUtc",
    "LastModifiedAtUtc"
)
SELECT
    a."Id",
    hu."Id",
    2,
    1,
    NOW(),
    NOW()
FROM "Accounts" a
JOIN "HouseholdUsers" hu ON hu."HouseholdId" = a."HouseholdId"
WHERE a."HouseholdId" = '<HOUSEHOLD_ID>'::uuid
  AND a."IsActive" = TRUE
  AND hu."MembershipStatus" = 1
ON CONFLICT ("AccountId", "HouseholdUserId") DO UPDATE
SET
    "AccessRole" = EXCLUDED."AccessRole",
    "Visibility" = EXCLUDED."Visibility",
    "LastModifiedAtUtc" = NOW();
```

## Step 4: Database verification queries
Run the following checks after bootstrap.

```sql
-- Active mapped users in this household.
SELECT
    hu."Id" AS "HouseholdUserId",
    hu."DisplayName",
    hu."ExternalUserKey",
    hu."MembershipStatus",
    mu."AuthProvider",
    mu."AuthSubject",
    mu."Email",
    mu."IsActive"
FROM "HouseholdUsers" hu
LEFT JOIN "MosaicUsers" mu ON mu."Id" = hu."MosaicUserId"
WHERE hu."HouseholdId" = '<HOUSEHOLD_ID>'::uuid
ORDER BY hu."DisplayName";

-- ACL density for the household (optional but recommended when accounts exist).
SELECT
    COUNT(*) AS "AccessRows"
FROM "AccountMemberAccessEntries" ama
JOIN "Accounts" a ON a."Id" = ama."AccountId"
WHERE a."HouseholdId" = '<HOUSEHOLD_ID>'::uuid;

-- Plaid ingestion proof counters (evidence gate).
SELECT 'PlaidItemCredentials' AS "Table", COUNT(*) AS "RowCount" FROM "PlaidItemCredentials"
UNION ALL
SELECT 'PlaidItemSyncStates', COUNT(*) FROM "PlaidItemSyncStates"
UNION ALL
SELECT 'RawTransactionIngestionRecords', COUNT(*) FROM "RawTransactionIngestionRecords"
UNION ALL
SELECT 'EnrichedTransactions', COUNT(*) FROM "EnrichedTransactions";
```

Expected minimum state:
1. Exactly two active household members mapped to Clerk subjects.
2. `MembershipStatus = 1` for both sample members.
3. If accounts are present, `AccountMemberAccessEntries` has rows for both sample members.

## Step 5: Web and mobile validation using each sample persona
1. Configure web for Partner A.
```powershell
cd src/MosaicMoney.Web
copy .env.example .env.local
# Set MOSAIC_HOUSEHOLD_USER_ID=<HOUSEHOLD_USER_ID_A>
npm install
npm run dev
```
2. Sign in with Partner A and validate:
   - `/settings/household` shows both members.
   - `/accounts` and `/transactions` load without member-context errors.
3. Repeat for Partner B by changing `MOSAIC_HOUSEHOLD_USER_ID=<HOUSEHOLD_USER_ID_B>` and restarting web.
4. For mobile, repeat with:
   - `EXPO_PUBLIC_MOSAIC_HOUSEHOLD_USER_ID=<HOUSEHOLD_USER_ID_A>` then `<HOUSEHOLD_USER_ID_B>`.

## Step 6: API spot checks with Clerk token (optional but recommended)
Use a valid bearer token issued for the signed-in sample user and include explicit household member context:

```powershell
$headers = @{
  Authorization = "Bearer <CLERK_JWT_FOR_USER>"
  "X-Mosaic-Household-User-Id" = "<HOUSEHOLD_USER_ID_A_OR_B>"
}

Invoke-RestMethod -Method Get -Uri "http://localhost:5091/api/v1/households" -Headers $headers
Invoke-RestMethod -Method Get -Uri "http://localhost:5091/api/v1/transactions" -Headers $headers
Invoke-RestMethod -Method Get -Uri "http://localhost:5091/api/v1/households/<HOUSEHOLD_ID>/account-access" -Headers $headers
```

## Evidence checklist for status promotion
Capture these artifacts before moving related tasks from `In Review` to `Done`:
1. SQL output proving two active mapped members.
2. SQL row counts for Plaid ingestion tables and (when applicable) account ACL rows.
3. Web screenshots for Partner A and Partner B on household settings.
4. API spot-check output (`GET /api/v1/households`, `GET /api/v1/transactions`).

## Security and cleanup
1. Never commit Clerk keys, JWTs, or `.env.local` files.
2. Keep sample users in Clerk test/development tenants only.
3. When validation is complete, either disable sample users in Clerk or rotate test credentials.
