# Aspire JavaScript Frontend Policy

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

## Script and package-manager policy
- Default run/build script flow should align with `AddJavaScriptApp` conventions (`dev` for local run, `build` for publish) unless explicitly customized.
- If custom behavior is needed, use `WithRunScript(...)`, `WithBuildScript(...)`, and package manager selectors (`WithNpm`, `WithYarn`, `WithPnpm`).

## Next.js-specific implementation notes
- Keep server-side API calls on server boundaries (Route Handlers / Server Components) when possible.
- Do not leak internal service URLs to browser bundles unless intentionally public.
- If client-side calls are required, expose only approved public env vars and route through controlled API surfaces.

## Source grounding
- JavaScript hosting package and methods:
  - `https://aspire.dev/integrations/frameworks/javascript/`
  - `https://aspire.dev/get-started/add-aspire-existing-app/`
- JavaScript API migration details:
  - `https://aspire.dev/whats-new/aspire-13/`
