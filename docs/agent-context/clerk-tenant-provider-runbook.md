# Clerk Tenant and Provider Runbook (M8 MM-ASP-11)


## Agent Loading
- Load when: configuring Clerk tenant/provider keys, issuer, SSO, passkey readiness, and runtime env contracts.
- Apply with workspace policy: [.github/copilot-instructions.md](../../.github/copilot-instructions.md)

## Purpose
This runbook defines the Clerk tenant/provider setup and runtime configuration contract for Mosaic Money across API, web, and mobile surfaces.

It is aligned to MM-ASP-10 and MM-ASP-11:

1. Runtime key and issuer injection through AppHost parameters
2. Microsoft social connection setup
3. Passkey/WebAuthn readiness and constraints
4. Local and CI configuration flows without committing secret values

For two-persona validation and household data bootstrap using sample Clerk users, see `docs/agent-context/clerk-sample-users-household-validation-runbook.md`.

## Source links
- Aspire JavaScript hosting integration: https://aspire.dev/integrations/frameworks/javascript/
- Aspire 13 JavaScript API migration (`AddNpmApp` removed, `AddJavaScriptApp` introduced): https://aspire.dev/whats-new/aspire-13/
- Clerk environment variables: https://clerk.com/docs/guides/development/clerk-environment-variables
- Clerk Next.js quickstart: https://clerk.com/docs/nextjs/getting-started/quickstart
- Clerk Expo quickstart: https://clerk.com/docs/expo/getting-started/quickstart
- Clerk Microsoft social connection guide: https://clerk.com/docs/guides/configure/auth-strategies/social-connections/microsoft
- Clerk sign-up/sign-in options (passkey limitations): https://clerk.com/docs/guides/configure/auth-strategies/sign-up-sign-in-options
- Clerk custom passkey flow (domain restrictions and API methods): https://clerk.com/docs/guides/development/custom-flows/authentication/passkeys

## AppHost contract
In `src/apphost.cs`, Clerk values are modeled as AppHost parameters and injected into resources.

Defined parameters:

1. `clerk-publishable-key` (public)
2. `clerk-secret-key` (secret)
3. `clerk-issuer` (public configuration value)

Injected runtime env values:

1. API (`api` resource)
- `Authentication__Clerk__Issuer`
- `Authentication__Clerk__SecretKey`

2. Web (`web` resource)
- `CLERK_PUBLISHABLE_KEY`
- `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY`
- `CLERK_SECRET_KEY`

3. Mobile
- Mobile is not AppHost-launched in this repo today.
- Runtime contract is documented in `src/MosaicMoney.Mobile/.env.example`.

## Clerk app setup (Mosaic Money)
1. Create or select a Clerk app in the Clerk Dashboard.
2. Capture app keys from the API keys page:
- Publishable key (starts with `pk_test_` or `pk_live_`)
- Secret key (starts with `sk_test_` or `sk_live_`)
3. Determine issuer URL for JWT validation (use your Clerk instance issuer/domain as required by backend JWT validation policy).
4. Store all runtime values through local user-secrets/AppHost parameter flow; do not commit values in repo files.

## Microsoft SSO provider setup
Use Clerk's Microsoft social connection flow.

Development instance:

1. In Clerk dashboard, open `SSO connections`.
2. Add connection for all users.
3. Select Microsoft.
4. Development instances can use Clerk shared OAuth credentials by default.

Production instance:

1. In Clerk dashboard, enable Microsoft social connection with custom credentials.
2. In Microsoft Entra ID, create or reuse app registration with Clerk redirect URI.
3. Create client secret and store expiry reminder.
4. Configure OpenID settings as required by Clerk guide.
5. Apply Clerk-documented hardening guidance for nOAuth mitigation (including optional claim handling).
6. Enter client ID/secret in Clerk dashboard and test sign-in.

## Passkey/WebAuthn enablement considerations
1. Enable passkeys in Clerk User & Authentication settings.
2. Passkeys are domain-bound. Use consistent domain strategy for local and production flows.
3. Passkeys are not currently available as an MFA option in Clerk.
4. Clerk docs explicitly note Expo-specific caveats:
- "Passkey related APIs will not work with Expo" on the sign-up/sign-in options page.
- Follow dedicated Expo passkey guidance before enabling passkey UX in mobile flows.
5. For custom passkey UX, use Clerk passkey APIs (`User.createPasskey()`, `SignIn.authenticateWithPasskey()`) only where platform/browser support is validated.

## Local setup commands
### Project-based AppHost flow
Use when AppHost is a standard `.csproj` project.

```bash
dotnet user-secrets init --project <path-to-apphost-csproj>
dotnet user-secrets set "Parameters:clerk-publishable-key" "<pk_test_...>" --project <path-to-apphost-csproj>
dotnet user-secrets set "Parameters:clerk-secret-key" "<sk_test_...>" --project <path-to-apphost-csproj>
dotnet user-secrets set "Parameters:clerk-issuer" "https://<your-instance>" --project <path-to-apphost-csproj>
dotnet user-secrets list --project <path-to-apphost-csproj>
```

### File-based AppHost flow (this repository)
This repo uses file-based AppHost at `src/apphost.cs`.

```bash
dotnet user-secrets set "Parameters:clerk-publishable-key" "<pk_test_...>" --file src/apphost.cs
dotnet user-secrets set "Parameters:clerk-secret-key" "<sk_test_...>" --file src/apphost.cs
dotnet user-secrets set "Parameters:clerk-issuer" "https://<your-instance>" --file src/apphost.cs
dotnet user-secrets list --file src/apphost.cs
```

## Key mapping table
| Key name | Consumer project(s) | Secret or public | Local source |
| --- | --- | --- | --- |
| `Parameters:clerk-publishable-key` | AppHost -> Web (`CLERK_PUBLISHABLE_KEY`, `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY`) | Public | AppHost user-secrets |
| `Parameters:clerk-secret-key` | AppHost -> API (`Authentication__Clerk__SecretKey`), Web (`CLERK_SECRET_KEY`) | Secret | AppHost user-secrets |
| `Parameters:clerk-issuer` | AppHost -> API (`Authentication__Clerk__Issuer`) | Public configuration | AppHost user-secrets |
| `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY` | Web standalone `.env.local` | Public | Untracked local env file |
| `CLERK_SECRET_KEY` | Web standalone `.env.local` server boundary only | Secret | Untracked local env file or CI secret store |
| `EXPO_PUBLIC_CLERK_PUBLISHABLE_KEY` | Mobile Expo runtime | Public | Untracked local env file |

## CI guidance
1. Never commit Clerk key values or issuer values in repo-tracked files.
2. Store Clerk values in CI secret/variable management (for example GitHub Actions secrets/variables, Azure pipeline secret variables, or managed secret store).
3. If CI runs AppHost directly, inject parameter values via runtime secret provisioning before run.
4. Commit only contract templates (`.env.example`, `appsettings.json` placeholders).
5. Treat all `NEXT_PUBLIC_*` and `EXPO_PUBLIC_*` values as public.

## Validation checklist
1. `src/apphost.cs` has Clerk parameters with `clerk-secret-key` marked `secret: true`.
2. API and web resources receive Clerk env values via `WithEnvironment(...)`.
3. `src/MosaicMoney.Web/.env.example` and `src/MosaicMoney.Mobile/.env.example` include Clerk placeholders only.
4. `src/MosaicMoney.Api/appsettings.json` includes Clerk placeholders only.
5. No Clerk credentials are committed.

