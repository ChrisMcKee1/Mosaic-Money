# MM-FE-08 Playwright Regression Pack

This suite validates FE-04 through FE-07 critical web journeys using deterministic fixtures.

## What is covered

- Projection rendering on dashboard (FE-06, FE-07).
- Read-only transaction rendering and projection context (FE-04, FE-07).
- Needs-review approval/reclassify/reject actions (FE-05).
- Desktop and mobile navigation behavior.
- Explicit error-state rendering for dashboard, transactions, and needs-review routes.

## Deterministic strategy

- Playwright starts a local test-only mock API (`tests/e2e/mock-api-server.mjs`).
- Next.js runs against this mock API via server-side `API_URL`.
- Tests call mock control endpoints to reset state and toggle failures:
  - `POST /__e2e/reset`
  - `POST /__e2e/scenario`
- No production secrets are required, and no backend dependency is needed for FE-08 coverage.

## Run

```bash
npm run test:e2e
```

Optional:

```bash
npm run test:e2e:headed
npm run test:e2e:ui
```
