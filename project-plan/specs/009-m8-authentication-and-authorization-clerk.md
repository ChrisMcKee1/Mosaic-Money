# Spec 009: M8 Authentication and Authorization (Clerk)

## Status
- Drafted: 2026-02-25
- Milestone: M8
- Depends on:
- `project-plan/specs/001-mvp-foundation-task-breakdown.md`
- `project-plan/specs/007-m6-ui-redesign-and-theming.md`
- `project-plan/specs/008-m7-identity-household-access-and-account-ownership.md`

## Objective
Implement production-grade authentication and authorization for Mosaic Money using Clerk across web, mobile, and API layers, while preserving existing household ACL constraints and release safety guardrails.

## Strategic Rationale
- Reduce auth implementation burden by delegating identity/session UX and crypto-heavy concerns to Clerk.
- Prioritize Microsoft SSO for low-friction local and lab workflows.
- Keep architecture standards-based (OIDC/JWT) to preserve future provider flexibility.
- Enable passkey readiness without introducing custom WebAuthn backend complexity.

## In Scope
- Clerk tenant/provider setup runbook (Microsoft SSO and passkeys).
- AppHost/user-secrets/env contract for Clerk keys and issuer values.
- API JWT bearer validation and deny-by-default authorization policy for protected routes.
- Mapping authenticated `sub` claim to Mosaic identity (`MosaicUsers`) for household-scoped authorization.
- Web Clerk integration (`@clerk/clerk-react`) with sign-in/sign-up routes and protected app shell.
- Mobile Clerk integration (`@clerk/clerk-expo`) with `expo-secure-store` token cache and custom sign-in UX.
- UX path improvements:
  - Accounts page Add Account/Plaid CTA discoverability.
  - Settings information architecture for Appearance/Theming and Security/Auth sections.

## Out of Scope
- Replacing Clerk with custom in-house auth providers.
- Autonomous authorization decisions outside existing household ACL model.
- Any relaxation of M7 fail-closed account visibility or review-routing guarantees.

## Guardrails
- No secrets in source-controlled files or public env keys (`NEXT_PUBLIC_*`, `EXPO_PUBLIC_*`).
- API stays resource-server only: accepts/validates tokens; does not own login ceremony.
- Protected routes fail closed by default.
- Existing single-entry ledger and `NeedsReview` safeguards remain unchanged.

## Task Breakdown
| ID | Domain | Task | Dependencies | Deliverable | Status |
|---|---|---|---|---|---|
| MM-ASP-10 | DevOps | Clerk runtime secret and issuer wiring | MM-ASP-03, MM-ASP-04 | AppHost/API/Web/Mobile receive Clerk keys/issuer through env + user-secrets contract with documented key map. | In Review |
| MM-ASP-11 | DevOps | Clerk tenant/provider configuration runbook | MM-ASP-10 | Source-linked setup runbook for Clerk app creation, Microsoft SSO, passkey enablement, and local/CI variable mapping. | In Review |
| MM-BE-25 | Backend | API JWT validation and fallback auth policy | MM-ASP-10, MM-BE-19 | API validates Clerk JWTs and enforces deny-by-default authorization on protected endpoints. | In Review |
| MM-BE-26 | Backend | Auth subject to Mosaic identity mapping | MM-BE-25, MM-BE-19, MM-BE-20 | Backend maps `sub` claim to `MosaicUsers` and applies membership checks for household/account access APIs. | In Review |
| MM-FE-22 | Web | Clerk web integration and protected routes | MM-ASP-10 | Web app uses Clerk provider, sign-in/sign-up routes, and guarded shell navigation behavior. | In Review |
| MM-FE-23 | Web | Accounts Add Account and Plaid link path | MM-FE-22, MM-FE-12, MM-FE-09 | Accounts screen exposes Add Account CTA and routes users into Plaid onboarding/linking flow. | In Review |
| MM-FE-24 | Web | Settings IA for appearance and security | MM-FE-10, MM-FE-22 | Settings contains Appearance/Theming and Security sections without breaking existing M6 visual language. | Done |
| MM-MOB-13 | Mobile | Clerk Expo integration and custom sign-in | MM-ASP-10 | Mobile integrates Clerk + secure token cache and supports Microsoft sign-in path in custom React Native flow. | In Review |
| MM-MOB-14 | Mobile | Settings and account-link parity | MM-MOB-13, MM-MOB-10 | Mobile settings exposes appearance/security and Add Account/Plaid entrypoint parity with web intent. | Done |
| MM-QA-04 | QA | Auth and access regression gate | MM-BE-26, MM-FE-24, MM-MOB-14 | End-to-end pass for auth flows, protected endpoint behavior, and account-link navigation on web/mobile/API. | In Review |

## Acceptance Criteria
- Web and mobile can complete Clerk-backed sign-in flows and maintain valid sessions.
- Unauthorized requests to protected API routes are rejected by fallback authorization policy.
- Authenticated requests map to valid Mosaic user identity and household membership context.
- Accounts page includes a clear Add Account/Plaid entry action.
- Settings includes appearance and security/auth controls with coherent navigation.
- Secret/config contracts are documented and reproducible for local and CI.

## Verification
- Backend: auth middleware/unit/integration checks for JWT validation and unauthorized rejection.
- Web: build + route checks for sign-in/protected route behavior and account-link entrypoint.
- Mobile: typecheck + sign-in flow checks with secure token cache wiring.
- QA: scripted matrix for positive/negative auth flows and household ACL interaction sanity.

## Kickoff Note (2026-02-25)
Initial implementation slice is started and delegated for:
- `MM-ASP-10`
- `MM-ASP-11`
- `MM-BE-25`
- `MM-FE-22`
- `MM-FE-23`
- `MM-MOB-13`

## Update Note (2026-02-25)
MM-ASP-10 and MM-ASP-11 implementation now includes AppHost Clerk parameter wiring (`clerk-publishable-key`, `clerk-secret-key`, `clerk-issuer`), API/web environment injection via `WithEnvironment(...)`, placeholder contract updates in API/web/mobile config templates, and a source-linked runbook at `docs/agent-context/clerk-tenant-provider-runbook.md`. Status remains `In Progress` pending planner acceptance.

## Update Note (2026-02-25)
MM-BE-26 moved to `In Review` after adding shared household member context resolution that maps authenticated subject claims to active `MosaicUsers` and active `HouseholdUsers` memberships, with mismatch/ambiguity guard rails and coverage in `ApiAuthorizationTests`.

## Update Note (2026-02-26)
MM-FE-24 and MM-MOB-14 moved to `In Progress` for coordinated implementation of settings IA and account-link parity across web and mobile surfaces.

## Update Note (2026-02-26)
`MM-FE-24` and `MM-MOB-14` are now `Done` after implementing and validating web/mobile settings parity and Add Account entry points:
- Web validation: `npm run build` and `npm run test:e2e -- tests/e2e/settings.spec.js tests/e2e/auth-protection.spec.js tests/e2e/accounts.spec.js`
- Mobile validation: `npm run typecheck`, `npm run test:sync-recovery`, and `npm run test:review-projection`

`MM-QA-04` is moved to `In Review` with backend/web/mobile evidence captured from:
- API auth regression: `dotnet test src/MosaicMoney.Api.Tests/MosaicMoney.Api.Tests.csproj --filter ApiAuthorizationTests`
- Web route/auth/account-link checks: targeted Playwright suite above
- Mobile compile/regression checks: typecheck + focused vitest suites above

Planner retains final authority on promotion to `Done`.
