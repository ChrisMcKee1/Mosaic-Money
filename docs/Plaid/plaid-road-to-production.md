# Plaid Road To Production

## Purpose
This guide defines the Mosaic Money Plaid path from sandbox to production, including product scope, redirect URI requirements, OAuth expectations, webhook setup, and secret handling.

Related planning artifact: `project-plan/specs/003a-mm-be-15-plaid-product-capability-matrix.md`

## Current Local Redirect URI (Active Aspire Run)
Use this for the currently running local stack:
- `http://localhost:53832/onboarding/plaid`

This URL is valid for Plaid Sandbox localhost testing.

The AppHost now pins the web host port to `53832`, so this redirect URI remains stable across local restarts.

If you need to verify endpoint state during troubleshooting:

```powershell
aspire resources --project src/apphost.cs
```

Then append `/onboarding/plaid` to the `web` endpoint.

## Redirect URI Rules
- Sandbox/local: HTTP loopback redirect URIs are allowed for localhost testing.
- Production: register HTTPS redirect URIs only.
- Web callback route used by Mosaic Money: `/onboarding/plaid`.

## Product Plan
### Adopt Now (MVP)
- `transactions`
- Link token + public token exchange lifecycle foundation
- `SYNC_UPDATES_AVAILABLE` webhook ingestion path

### Adopt Later (Post-MVP slices)
- `transactions` recurring hints (`/transactions/recurring/get`)
- `identity`
- `liabilities`
- `investments`
- `auth`

### Out Of Scope (Current MVP)
- `income`
- `statements`

## Backend/Webhook Setup Baseline
1. Link token creation remains server-side (`/api/v1/plaid/link-tokens`).
2. Public token exchange remains server-side (`/api/v1/plaid/public-token-exchange`).
3. Transactions webhook receiver:
- `POST /api/v1/plaid/webhooks/transactions`
- Supports: `TRANSACTIONS` + `SYNC_UPDATES_AVAILABLE`
4. Durable sync state is persisted in `PlaidItemSyncStates` (cursor and replay-safe sync bookkeeping).

## OAuth Notes
- Keep OAuth redirect and re-entry behavior consistent across web/mobile surfaces.
- Treat item recovery states (`requires_update_mode`, `requires_relink`, `NeedsReview`) as explicit workflow states.
- Do not auto-resolve ambiguous/high-impact conditions.

## Secret And Configuration Setup
Define orchestration secrets in AppHost and keep values in user-secrets, not source control.

Project-based AppHost flow:

```powershell
cd src
dotnet user-secrets init --project apphost.csproj
dotnet user-secrets set "Parameters:plaid-client-id" "<your-client-id>" --project apphost.csproj
dotnet user-secrets set "Parameters:plaid-secret" "<your-secret>" --project apphost.csproj
dotnet user-secrets list --project apphost.csproj
```

File-based AppHost flow:

```powershell
cd src
dotnet user-secrets set "Parameters:plaid-client-id" "<your-client-id>" --file apphost.cs
dotnet user-secrets set "Parameters:plaid-secret" "<your-secret>" --file apphost.cs
dotnet user-secrets list --file apphost.cs
```

Do not place secrets in `.env`, `.env.local`, `NEXT_PUBLIC_*`, or committed `appsettings*.json` values.

## Local Docker + Postgres Validation Flow
Use this exact flow for local sandbox validation without printing secret values.

1. Find the running Postgres container:

```powershell
docker ps --format "table {{.ID}}\t{{.Image}}\t{{.Names}}\t{{.Status}}\t{{.Ports}}"
```

2. Set the container name and read the password into a variable without logging it:

```powershell
$container = "<postgres-container-name>"
$password = (docker exec $container sh -lc 'echo $POSTGRES_PASSWORD').Trim()
```

3. Run `psql` inside the container with `PGPASSWORD` to verify extensions:

```powershell
docker exec -e PGPASSWORD=$password $container psql -U postgres -d mosaicmoneydb -c "SELECT extname FROM pg_extension WHERE extname IN ('vector','azure_ai') ORDER BY extname;"
```

4. Validate public schema table count:

```powershell
docker exec -e PGPASSWORD=$password $container psql -U postgres -d mosaicmoneydb -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='public';"
```

5. Validate row counts for Plaid and ingestion durability tables:

```powershell
docker exec -e PGPASSWORD=$password $container psql -U postgres -d mosaicmoneydb -c "SELECT 'PlaidItemCredentials' AS table_name, COUNT(*) AS row_count FROM \"PlaidItemCredentials\" UNION ALL SELECT 'PlaidItemSyncStates', COUNT(*) FROM \"PlaidItemSyncStates\" UNION ALL SELECT 'RawTransactionIngestionRecords', COUNT(*) FROM \"RawTransactionIngestionRecords\" UNION ALL SELECT 'EnrichedTransactions', COUNT(*) FROM \"EnrichedTransactions\";"
```

6. Optional per-table row-count commands:

```powershell
docker exec -e PGPASSWORD=$password $container psql -U postgres -d mosaicmoneydb -c "SELECT COUNT(*) FROM \"PlaidItemCredentials\";"
docker exec -e PGPASSWORD=$password $container psql -U postgres -d mosaicmoneydb -c "SELECT COUNT(*) FROM \"PlaidItemSyncStates\";"
docker exec -e PGPASSWORD=$password $container psql -U postgres -d mosaicmoneydb -c "SELECT COUNT(*) FROM \"RawTransactionIngestionRecords\";"
docker exec -e PGPASSWORD=$password $container psql -U postgres -d mosaicmoneydb -c "SELECT COUNT(*) FROM \"EnrichedTransactions\";"
```

## Plaid Production Readiness Checklist
1. Register final HTTPS redirect URI(s) in Plaid Dashboard.
2. Ensure production domain callback resolves to `/onboarding/plaid`.
3. Configure production webhook URL(s) for transaction sync events.
4. Validate replay/idempotency behavior with webhook retries.
5. Run end-to-end verification for item recovery and review routing (`NeedsReview` fail-closed behavior).
6. Promote any `Adopt Later` product only via new spec approval + schema/API impact review.
