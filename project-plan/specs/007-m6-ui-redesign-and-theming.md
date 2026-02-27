# Milestone 6: UI Redesign and Theming

## Objective
Implement a distinctive, production-grade frontend interface with high design quality, moving away from generic aesthetics. The redesign includes a comprehensive dark/light mode, a new sidebar navigation, and polished screens for Dashboard, Accounts, Transactions, Categories, Investments, and Recurrings.

For dashboarding/reporting quality, this milestone now standardizes chart-component frameworks across surfaces:
- Web: `react-apexcharts`
- Mobile: `victory-native-xl`

## Design Direction
- **Aesthetic**: Refined, data-dense but breathable financial dashboard.
- **Typography**: Distinctive display fonts paired with highly legible body fonts for financial data.
- **Color & Theme**: Comprehensive Dark and Light modes using CSS variables. Dominant dark backgrounds with sharp, intentional accent colors for charts and status indicators.
- **Motion**: Subtle, CSS-only transitions for hover states and page loads.
- **Spatial Composition**: Sidebar navigation on the left, main content area, and contextual right-side panels for details.

## Component Framework Decision (2026-02-26)
- Web dashboard/reporting charts move to `react-apexcharts` for stronger financial time-series controls (zoom, pan, range interactions, mixed-series support).
- Mobile dashboard/reporting charts use `victory-native-xl` for native rendering performance and composable chart primitives.
- Existing `recharts` implementation remains transitional for already-shipped screens until migrated.
- No net-new `recharts` additions are permitted for M6 redesign scope.

## Execution Game Plan
1. **Foundation pass (Web + Mobile)**
	- Create shared data-shaping utilities for interval bucketing (`day`, `week`, `month`) and currency/percent formatting.
	- Define reusable chart configuration primitives per surface (web/mobile), token-aligned.
2. **Web migration pass**
	- Replace dashboard and reporting charts in `Dashboard`, `Accounts`, `Categories`, `Investments`, and `Recurrings` from `recharts` to `react-apexcharts`.
	- Preserve existing domain guardrails (projection-only rendering, no ledger-side mutation in UI).
3. **Mobile parity pass**
	- Introduce native chart widgets for overview and detail surfaces using `victory-native-xl`.
	- Ensure parity for interval switching and key KPI summaries with web behavior.
4. **Validation + release-gate pass**
	- Reconcile Playwright selectors/contracts for web chart interactions.
	- Run mobile type/test suites and targeted runtime checks for chart-rendered screens.

## Task Breakdown

| Task ID | Owner | Status | Description |
|---|---|---|---|
| MM-FE-10 | `mosaic-money-frontend` | Done | **Global Layout & Theming**: Implement Dark/Light mode toggle, CSS variable color system, distinctive typography, and the main application shell (Left Sidebar, Main Content, Right Context Panel). |
| MM-FE-11 | `mosaic-money-frontend` | Done | **Dashboard Overview Screen**: Implement Monthly spending line chart, Net worth line chart, Transactions to review widget, Top categories summary, and Next two weeks recurring widget. |
| MM-FE-12 | `mosaic-money-frontend` | Done | **Accounts Screen**: Implement Assets/Debts summary chart, and grouped lists for Credit cards, Depository, Investment, Loan, and Real estate with sparklines. Right panel for specific account details. |
| MM-FE-13 | `mosaic-money-frontend` | Done | **Transactions Screen**: Implement grouped transaction list (Today, Yesterday, etc.) with category tags and amounts. Right panel for transaction details, categorization, and history. |
| MM-FE-14 | `mosaic-money-frontend` | Done | **Categories & Budgeting Screen**: Implement total spent vs budget donut chart, detailed progress bars for regular categories. Right panel for category breakdown and historical bar chart. |
| MM-FE-15 | `mosaic-money-frontend` | Done | **Investments Screen**: Implement live balance estimate chart, top movers widget, and account list with 1W balance change. Right panel for specific asset details (e.g., Crypto chart and positions). |
| MM-FE-16 | `mosaic-money-frontend` | Done | **Recurrings Screen**: Implement left to pay vs paid so far donut chart, list of recurring transactions with status (paid, overdue, upcoming). Right panel for recurring rule details and history. |
| MM-FE-18 | `mosaic-money-frontend` | Done | **Semantic Search & Reranked Typeahead (Web)**: Upgrade all web search inputs/dropdowns to use semantic retrieval + reranking so related intents (for example `utilities` and `water`) surface relevant results even when exact labels differ. |
| MM-MOB-09 | `mosaic-money-mobile` | Done | **Semantic Search & Reranked Pickers (Mobile)**: Apply the same semantic retrieval + reranking behavior to mobile search and picker experiences to preserve cross-surface AI-enabled discovery quality. |

Update note (2026-02-25): Planner delegated MM-FE-18 and MM-MOB-09 with research-first and validation requirements. Implementations were returned with semantic search wiring across web/mobile plus focused validation evidence, and both tasks were promoted to `In Review`.

Update note (2026-02-26): Planner reran full web Playwright regression and moved `MM-FE-10..16` and `MM-FE-18` to `Blocked` pending selector/interaction contract reconciliation (`tests/e2e/dashboard-and-transactions.spec.js`, `tests/e2e/navigation-responsive.spec.js`, `tests/e2e/needs-review.spec.js`). `MM-MOB-09` is `Blocked` pending mobile runtime parity validation coverage.

Update note (2026-02-26): Planner approved visualization framework direction for redesign quality: web `react-apexcharts`, mobile `victory-native-xl`, with `recharts` frozen to legacy-only maintenance during migration.

Update note (2026-02-26): Planner moved `MM-FE-10..16`, `MM-FE-18`, and `MM-MOB-09` to `In Progress` to execute the framework migration completion wave with delegated frontend/mobile specialist implementation and validation.

Update note (2026-02-26): Planner promoted `MM-FE-10..16`, `MM-FE-18`, and `MM-MOB-09` to `Done` after migration/code-review cleanup and validation evidence (`npm run build`, full `npm run test:e2e`, focused `tests/e2e/chart-theme-parity.spec.js`, and mobile type/regression checks).

## Implementation Guidelines
- Follow the `frontend-design` skill principles.
- Avoid generic "AI slop" aesthetics (e.g., overused fonts like Inter/Roboto, predictable layouts).
- Ensure all charts and data visualizations are responsive and accessible.
- For new or migrated web dashboard/reporting visuals, use `react-apexcharts`.
- For new or migrated mobile dashboard/reporting visuals, use `victory-native-xl`.
- Do not introduce new `recharts` charts in M6 redesign work.
- Use Tailwind CSS for styling, leveraging custom theme extensions in `tailwind.config.ts`.
- Ensure seamless integration with existing Next.js App Router structure.