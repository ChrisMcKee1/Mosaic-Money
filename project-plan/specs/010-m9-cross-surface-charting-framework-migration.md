# Spec 010: M9 Cross-Surface Charting Framework Migration

## Status
- Drafted: 2026-02-26
- Milestone: M9
- Scope: Replace legacy web charting primitives and establish native mobile charting standards for dashboarding/reporting parity.

## Problem Statement
Current dashboard/reporting controls are not meeting redesign quality expectations for time-series financial workflows. Existing web charting is fragmented and interactions are inconsistent across surfaces. Mobile lacks a standardized chart framework for parity-level KPI visualization.

## Decision Summary
- Web chart standard: `react-apexcharts`
- Mobile chart standard: `victory-native-xl`
- Existing web `recharts` components are transitional legacy and frozen to maintenance-only while migration completes.

## Goals
1. Improve dashboard/reporting interactions for financial time-series data (interval switching, zoom/pan, mixed-series).
2. Enforce design-token consistency (light/dark) across all charted surfaces.
3. Achieve cross-surface parity for key KPI visualizations and behavior.
4. Validate interactions with Playwright evidence and track remaining UX gaps explicitly.

## Non-Goals
1. No changes to ledger truth semantics.
2. No autonomous financial action workflows.
3. No expansion of M8 mobile authentication scope (`MM-MOB-13` remains deferred/blocked).

## Execution Phases

### Phase A: Foundation
- Shared time-bucket utilities (`day/week/month`) and formatters.
- Shared chart configuration builders for tokenized options.
- Acceptance: chart data is pre-aggregated before render; no chart-local bucketing logic.

### Phase B: Web Migration
- Migrate M6 reporting/dashboard charts to ApexCharts:
  - Dashboard
  - Accounts
  - Categories/Budgeting
  - Investments
  - Recurrings
- Acceptance: no net-new `recharts` in migrated flows; interaction behavior documented and testable.

### Phase C: Mobile Chart Parity
- Introduce Victory Native XL chart components for parity-level KPI widgets.
- Acceptance: mobile views render key KPI trends and interval variants consistent with web intent.

### Phase D: Verification and Gap Capture
- Playwright validation for chart interactions on web in both light and dark themes.
- Capture missing/defective UX behaviors as explicit backlog/project items with reproduction notes and expected behavior.
- Acceptance: validation artifact with pass/fail matrix and gap log.

## Task Breakdown
| Task ID | Owner | Status | Description |
|---|---|---|---|
| MM-FE-25 | `mosaic-money-frontend` | Done | ApexCharts foundation + shared web chart config modules and time-bucket selector wiring. |
| MM-FE-26 | `mosaic-money-frontend` | Done | Migrate M6 web chart surfaces from Recharts to ApexCharts and preserve design-token parity. |
| MM-MOB-15 | `mosaic-money-mobile` | In Review | Add Victory Native XL chart primitives and parity KPI widgets across mobile overview/detail surfaces. |
| MM-QA-05 | `mosaic-money-planner` | Done | Playwright light/dark chart-interaction validation + documented gap registry with follow-up board items. |

## Done Criteria
- All M6 dashboard/reporting chart surfaces use ApexCharts on web.
- Mobile charted surfaces use Victory Native XL for parity widgets.
- Light/dark chart theming is token-consistent with no hardcoded rogue colors.
- Playwright validation artifacts confirm interactive chart behavior and log any residual gaps.
- Any unresolved gap has an explicit board item with reproducible steps and rationale.

## Risk and Governance
- Risk: Medium (high UI churn, low domain-risk to ledger truth).
- Decision: Allow with phased rollout and strict validation gates.
- Escalation: unresolved interaction defects or theme-token mismatches remain tracked in board backlog before final `Done` promotion.

## Validation Closeout (2026-02-26)
- Web validation: `npm run build` and full `npm run test:e2e` in `src/MosaicMoney.Web` passed with focused chart parity coverage in `tests/e2e/chart-theme-parity.spec.js`.
- Mobile validation: `npm run typecheck`, `npm run test:sync-recovery`, `npm run test:review-projection`, and `npx expo install --check` in `src/MosaicMoney.Mobile` passed.
- Residual gap tracked: GitHub issue `#125` (`MM-MOB-GAP-01`) for replacing `mockHistory` in `InvestmentsOverviewScreen.tsx` with API historical series data.
