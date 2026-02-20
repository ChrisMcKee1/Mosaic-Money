---
name: webapp-testing
description: Playwright-based workflow for testing Mosaic Money web UX and frontend/API integration paths. Use when validating Next.js screens, interaction flows, regressions, and responsive behavior.
---

# Webapp Testing

Use this skill when frontend behavior must be verified in a running browser.

## Workflow
1. Ensure required local services are running.
2. Execute targeted interaction tests for the changed surface.
3. Capture screenshots/logs on failures.
4. Report reproducible steps and observed behavior.

## Focus areas
- Transaction flows and dashboard rendering.
- Error and loading states.
- Mobile and desktop viewport behavior.
- Frontend calls to backend endpoints through approved boundaries.

## Testing guidance
- Prefer role- or test-id-based selectors.
- Use explicit waits for network and render completion.
- Keep tests deterministic and narrow in scope.
