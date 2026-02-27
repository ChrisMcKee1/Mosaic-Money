# UI/UX Audit Correlation and Mobile Alignment (2026-02-24)

## Scope
This report captures:
- Planner-led UI/UX audit of the web M6 implementation.
- Independent frontend-specialist audit.
- Correlated findings matrix.
- Mobile gap analysis and implemented alignment pass.

## Planner Audit (Primary)

### High Severity Findings
- Mobile contextual-detail loss on web pages using `rightPanel`.
  - `src/MosaicMoney.Web/components/layout/PageLayout.jsx:23`
  - `rightPanel` is hidden for smaller screens (`hidden lg:block`) with no mobile fallback, which removes detail views for transaction/category/investment/recurring selections.
- Missing scalable paging strategy for large ledgers.
  - `src/MosaicMoney.Web/app/transactions/TransactionsClient.jsx:108`
  - Transactions are rendered without virtualization, pagination, or incremental loading.

### Medium Severity Findings
- Mock data mixed directly in feature clients introduces drift risk against real contracts.
  - `src/MosaicMoney.Web/app/categories/CategoriesClient.jsx:16`
  - `src/MosaicMoney.Web/app/investments/InvestmentsClient.jsx:25`
  - `src/MosaicMoney.Web/app/accounts/page.jsx:11`
- Theme-token and semantic consistency gaps.
  - `src/MosaicMoney.Web/app/page.jsx:170`
  - Uses hardcoded purple accents in places despite global tokenized design.
- Dashboard fallback logic suggests incorrect field assumptions.
  - `src/MosaicMoney.Web/app/page.jsx:73`
  - `transactions.filter(tx => tx.needsReview).length || 3` can mask real state and suggests non-contract property usage.

### Low Severity Findings
- Potential visual jitter when selected row adds border-left styling.
  - `src/MosaicMoney.Web/app/transactions/TransactionsClient.jsx:113`
- Web-only custom scrollbar styling lacks Firefox equivalents.
  - `src/MosaicMoney.Web/app/globals.css:125`

## Frontend Specialist Audit (Independent)

### Executive Verdict
Web M6 aesthetic quality is strong and cohesive, but mobile parity and list scalability are primary UX risks.

### Reported High Findings
- Hidden mobile detail panel parity blocker.
  - `src/MosaicMoney.Web/components/layout/PageLayout.jsx:23`
- Large-list performance risk due to non-virtualized rendering.
  - `src/MosaicMoney.Web/app/transactions/TransactionsClient.jsx:108`

### Reported Medium Findings
- Mock-data intermix in category/investment clients.
  - `src/MosaicMoney.Web/app/categories/CategoriesClient.jsx:16`
  - `src/MosaicMoney.Web/app/investments/InvestmentsClient.jsx:25`
- Potential chart rendering overhead from SVG glow filters.
  - `src/MosaicMoney.Web/components/dashboard/DashboardCharts.jsx:65`
- Non-semantic clickable account rows.
  - `src/MosaicMoney.Web/components/accounts/AccountsList.jsx:43`

### Reported Low Findings
- Selected-row left-border layout shift.
  - `src/MosaicMoney.Web/app/transactions/TransactionsClient.jsx:113`
- Cross-browser scrollbar styling inconsistency.
  - `src/MosaicMoney.Web/app/globals.css:96`

## Correlated Findings Matrix

| Area | Planner | Frontend Specialist | Correlated Conclusion |
|---|---|---|---|
| Mobile parity for contextual details | High | High | Critical and agreed: no mobile fallback for right panel details. |
| Large-list scalability | High | High | Critical and agreed: add paging/virtualization path. |
| Mock/live data separation | Medium | Medium | Agreed: move mock shaping out of presentation clients. |
| Token/design consistency | Medium | Medium | Agreed: tighten semantic token usage and remove ad hoc accents. |
| Micro-interaction polish | Low | Low | Agreed: remove row-shift and improve small UX quality details. |

## Mobile Gap Analysis and Handoff Outcome
The correlated findings were handed to `mosaic-money-mobile` for UI parity alignment.

### Mobile Gaps Identified (Before)
- Generic light theme (`#f2f4f7` page and `#ffffff` cards) diverged from M6 web visual language.
- Repeated hardcoded color values across projection and review components.
- Weaker visual hierarchy for financial values and semantic statuses.

### Mobile Improvements Implemented (After)
- Added shared design tokens aligned to web M6 palette and semantics.
  - `src/MosaicMoney.Mobile/src/theme/tokens.ts`
- Applied dark-forward shell styling in stack layout.
  - `src/MosaicMoney.Mobile/app/_layout.tsx`
- Updated key review/projection surfaces to tokenized cards, semantic badges, and improved hierarchy.
  - `src/MosaicMoney.Mobile/src/features/transactions/components/NeedsReviewQueueScreen.tsx`
  - `src/MosaicMoney.Mobile/src/features/transactions/components/NeedsReviewQueueItem.tsx`
  - `src/MosaicMoney.Mobile/src/features/transactions/components/TransactionDetailScreen.tsx`
  - `src/MosaicMoney.Mobile/src/features/transactions/components/ReviewActionPanel.tsx`
  - `src/MosaicMoney.Mobile/src/features/transactions/components/StatePanel.tsx`
  - `src/MosaicMoney.Mobile/src/features/projections/components/ProjectionDashboardScreen.tsx`
  - `src/MosaicMoney.Mobile/src/features/projections/components/ProjectionSummarySection.tsx`
  - `src/MosaicMoney.Mobile/src/features/projections/components/ProjectionListSection.tsx`
  - `src/MosaicMoney.Mobile/src/features/projections/components/ProjectionDetailSection.tsx`

## Verification Evidence
- `cd src/MosaicMoney.Mobile; npm run typecheck` -> pass
- `cd src/MosaicMoney.Mobile; npm run test:sync-recovery` -> pass (4/4)
- `cd src/MosaicMoney.Mobile; npm run test:review-projection` -> pass (2/2)

## Resolution Update (2026-02-24, parity execution)

The following parity actions were completed after the initial audit:

- Web mobile contextual detail parity fixed:
  - `src/MosaicMoney.Web/components/layout/PageLayout.jsx`
  - Mobile now receives the same detail/context content previously hidden behind desktop-only right panels.

- Web transaction scalability improved with server-page controls:
  - `src/MosaicMoney.Web/app/transactions/page.jsx`
  - `src/MosaicMoney.Web/app/transactions/TransactionsClient.jsx`
  - Added page/pageSize query handling and previous/next controls; preserved search and detail selection behavior.

- Mobile surface parity expanded to mirror web primary sections:
  - New routes: `Transactions`, `Accounts`, `Categories`, `Investments`, `Recurrings`.
  - Shared navigation added across surfaces: `src/MosaicMoney.Mobile/src/shared/components/PrimarySurfaceNav.tsx`.
  - New screens:
    - `src/MosaicMoney.Mobile/src/features/parity/components/TransactionsOverviewScreen.tsx`
    - `src/MosaicMoney.Mobile/src/features/parity/components/AccountsOverviewScreen.tsx`
    - `src/MosaicMoney.Mobile/src/features/parity/components/CategoriesOverviewScreen.tsx`
    - `src/MosaicMoney.Mobile/src/features/parity/components/InvestmentsOverviewScreen.tsx`
    - `src/MosaicMoney.Mobile/src/features/parity/components/RecurringsOverviewScreen.tsx`

### Additional Validation Evidence
- `cd src/MosaicMoney.Web; npm run build` -> pass

## Remaining Gaps (Next Iteration)
- Web mobile fallback for right-context panels remains unimplemented.
- Web list virtualization/paging for very large transaction datasets remains unimplemented.
- Optional: introduce shared mobile UI primitives (card/badge/button wrappers) for consistency and maintainability.

## Framework Modernization Direction (2026-02-26)

Following redesign-quality concerns around dashboarding/reporting controls, planner direction is:
- Web dashboard/reporting charts: migrate to `react-apexcharts`.
- Mobile dashboard/reporting charts: standardize on `victory-native-xl`.
- Existing web `recharts` usage remains transitional only while migration completes.

### Why this direction
- Better built-in time-series interaction patterns for financial workflows (interval switching, zooming, panning, mixed series).
- Stronger parity path between rich web visuals and native mobile chart primitives.
- Cleaner separation between data shaping (selectors/hooks) and chart rendering components.

### Handoff requirements for frontend/mobile engineers
- Implement shared day/week/month bucket builders before chart render boundaries.
- Build reusable chart config primitives per surface instead of per-screen ad hoc options.
- Keep color/typography strictly token-driven and remove one-off chart-local styling.
- Add interaction and snapshot coverage for key chart widgets in web/mobile validation suites.
