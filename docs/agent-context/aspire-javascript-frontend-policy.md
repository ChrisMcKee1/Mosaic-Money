# Aspire JavaScript Frontend Policy


## Agent Loading
- Load when: changing Next.js/Vite/Node frontend wiring or AppHost JavaScript resource composition.
- Apply with workspace policy: [.github/copilot-instructions.md](../../.github/copilot-instructions.md)

This policy applies to web frontends (Next.js, Vite, and Node-based JS apps) orchestrated by Aspire.

## Required hosting integration
- AppHost must use `Aspire.Hosting.JavaScript` for JavaScript resources.
- Use the current APIs:
  - `AddJavaScriptApp` for generic JavaScript apps.
  - `AddViteApp` for Vite-based apps.
  - `AddNodeApp` for Node script-based apps.

## Migration guardrail
- Do not introduce `AddNpmApp` for Aspire 13+ work.
- `AddNpmApp` is deprecated/removed from the modern JavaScript hosting model.

## Frontend-to-API connectivity
- Prefer AppHost `WithReference(api)` to inject service URLs and dependency wiring.
- For browser-facing frontend resources, use `WithExternalHttpEndpoints()`.
- Use `WaitFor(api)` when frontend startup depends on API readiness.
- Avoid hardcoded localhost URLs in frontend code when Aspire can inject environment values.

## Environment variable and secret boundaries
- For Aspire-orchestrated runs, inject backend URLs and sensitive server-side values from AppHost using `WithReference(...)` and `WithEnvironment(...)`.
- Define shared sensitive values in AppHost with `AddParameter(..., secret: true)` and source local values from AppHost user-secrets.
- Keep browser-exposed variables non-sensitive. Any `NEXT_PUBLIC_*` value must be treated as public.
- Commit `.env.example` templates with placeholders for standalone frontend workflows, but do not commit `.env` or `.env.local`.
- Keep secrets on server boundaries (Route Handlers, Server Components, backend APIs) whenever possible.

## Script and package-manager policy
- Default run/build script flow should align with `AddJavaScriptApp` conventions (`dev` for local run, `build` for publish) unless explicitly customized.
- If custom behavior is needed, use `WithRunScript(...)`, `WithBuildScript(...)`, and package manager selectors (`WithNpm`, `WithYarn`, `WithPnpm`).

## Next.js-specific implementation notes
- Keep server-side API calls on server boundaries (Route Handlers / Server Components) when possible.
- Do not leak internal service URLs to browser bundles unless intentionally public.
- If client-side calls are required, expose only approved public env vars and route through controlled API surfaces.

## Dashboard and reporting visualization policy (web)
- Standardize on `react-apexcharts` for web dashboard/reporting charts that require interval toggles and time-series interactions.
- Treat existing `recharts` usage as transitional legacy; do not add new `recharts` components for net-new M6/M7 dashboarding work.
- Centralize chart option builders and shared formatting in reusable modules (avoid duplicated per-screen config blobs).
- Pre-compute grouped financial series (day/week/month buckets) in selectors/hooks before passing data into chart components.
- Keep chart colors and typography aligned to design tokens; avoid one-off chart-local hardcoded color values.

## Source grounding
- JavaScript hosting package and methods:
  - `https://aspire.dev/integrations/frameworks/javascript/`
  - `https://aspire.dev/get-started/add-aspire-existing-app/`
- JavaScript API migration details:
  - `https://aspire.dev/whats-new/aspire-13/`
- ApexCharts React docs:
  - `https://apexcharts.com/docs/react-charts/`
- Mosaic Money secrets/config playbook: `docs/agent-context/secrets-and-configuration-playbook.md`

