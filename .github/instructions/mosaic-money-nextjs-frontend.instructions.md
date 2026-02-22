---
name: Mosaic Money Next.js Frontend
description: Mosaic Money web frontend rules for Next.js 16 App Router, React 19, Tailwind, and Aspire-aware API boundaries.
applyTo: '**/*.{tsx,jsx,css}'
---

# Mosaic Money Next.js Frontend Rules

## Next.js architecture
- Use App Router patterns with Server Components by default.
- Move client-only interactivity into explicit Client Components.
- Keep internal service URLs on server boundaries when possible.

## Data and API boundaries
- Reflect backend truth and avoid front-end-only financial side effects.
- Do not mutate ledger semantics in the UI.
- Treat amortization as projection-only rendering logic.
- Prefer injected reference-based connectivity over hardcoded endpoint URLs.
- Under Aspire orchestration, source API URLs and server-side secrets via AppHost-injected environment variables.
- Commit `.env.example` templates only for standalone frontend runs; never commit `.env` or `.env.local`.
- Treat `NEXT_PUBLIC_*` as public values and never place credentials or private tokens there.
- When adding or changing environment keys, update `.env.example` with placeholder values and short key-purpose comments.

## UI standards
- Maintain accessibility and responsive behavior for desktop and mobile.
- Keep loading and error states explicit for financial workflows.
- Use Tailwind consistently with reusable component patterns.

## Aspire alignment
- Follow `docs/agent-context/aspire-javascript-frontend-policy.md` for orchestration and app-host behavior.
- Do not introduce `AddNpmApp` in Aspire-related guidance or generated code.

## Verification
- Validate key interaction paths with Playwright-style flows when behavior changes.
