# Milestone 6: UI Redesign and Theming

## Objective
Implement a distinctive, production-grade frontend interface with high design quality, moving away from generic aesthetics. The redesign includes a comprehensive dark/light mode, a new sidebar navigation, and polished screens for Dashboard, Accounts, Transactions, Categories, Investments, and Recurrings.

## Design Direction
- **Aesthetic**: Refined, data-dense but breathable financial dashboard.
- **Typography**: Distinctive display fonts paired with highly legible body fonts for financial data.
- **Color & Theme**: Comprehensive Dark and Light modes using CSS variables. Dominant dark backgrounds with sharp, intentional accent colors for charts and status indicators.
- **Motion**: Subtle, CSS-only transitions for hover states and page loads.
- **Spatial Composition**: Sidebar navigation on the left, main content area, and contextual right-side panels for details.

## Task Breakdown

| Task ID | Owner | Status | Description |
|---|---|---|---|
| MM-FE-10 | `mosaic-money-frontend` | In Review | **Global Layout & Theming**: Implement Dark/Light mode toggle, CSS variable color system, distinctive typography, and the main application shell (Left Sidebar, Main Content, Right Context Panel). |
| MM-FE-11 | `mosaic-money-frontend` | In Review | **Dashboard Overview Screen**: Implement Monthly spending line chart, Net worth line chart, Transactions to review widget, Top categories summary, and Next two weeks recurring widget. |
| MM-FE-12 | `mosaic-money-frontend` | In Review | **Accounts Screen**: Implement Assets/Debts summary chart, and grouped lists for Credit cards, Depository, Investment, Loan, and Real estate with sparklines. Right panel for specific account details. |
| MM-FE-13 | `mosaic-money-frontend` | In Review | **Transactions Screen**: Implement grouped transaction list (Today, Yesterday, etc.) with category tags and amounts. Right panel for transaction details, categorization, and history. |
| MM-FE-14 | `mosaic-money-frontend` | In Review | **Categories & Budgeting Screen**: Implement total spent vs budget donut chart, detailed progress bars for regular categories. Right panel for category breakdown and historical bar chart. |
| MM-FE-15 | `mosaic-money-frontend` | In Review | **Investments Screen**: Implement live balance estimate chart, top movers widget, and account list with 1W balance change. Right panel for specific asset details (e.g., Crypto chart and positions). |
| MM-FE-16 | `mosaic-money-frontend` | In Review | **Recurrings Screen**: Implement left to pay vs paid so far donut chart, list of recurring transactions with status (paid, overdue, upcoming). Right panel for recurring rule details and history. |

## Implementation Guidelines
- Follow the `frontend-design` skill principles.
- Avoid generic "AI slop" aesthetics (e.g., overused fonts like Inter/Roboto, predictable layouts).
- Ensure all charts and data visualizations are responsive and accessible.
- Use Tailwind CSS for styling, leveraging custom theme extensions in `tailwind.config.ts`.
- Ensure seamless integration with existing Next.js App Router structure.