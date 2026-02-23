import { expect, test } from "@playwright/test";
import { resetMockApi, setMockScenario } from "./support/mockApi";

test.beforeEach(async ({ request }) => {
  await resetMockApi(request);
});

test("renders projection metrics and transaction context from backend truth", async ({ page }) => {
  await page.goto("/");

  await expect(page.getByRole("heading", { name: "Dashboard" })).toBeVisible();
  await expect(page.getByTestId("api-status-value")).toHaveText("Connected");

  await expect(page.getByTestId("metric-total-liquidity")).toHaveText("$1620.00");
  await expect(page.getByTestId("metric-household-burn")).toHaveText("$1300.00");
  await expect(page.getByTestId("metric-business-expenses")).toHaveText("$80.00");
  await expect(page.getByTestId("metric-safe-to-spend")).toHaveText("$470.00");
  await expect(page.getByTestId("metric-upcoming-recurring")).toHaveText("$1250.00");
  await expect(page.getByTestId("metric-pending-reimbursements")).toHaveText("$100.00");

  await expect(page.getByText("2026-02-10", { exact: true })).toBeVisible();
  await expect(page.getByText("Business Expense", { exact: true })).toBeVisible();
  await expect(page.getByText("Amortized: 3 splits", { exact: true })).toBeVisible();
});

test("shows disconnected state when health check fails", async ({ page, request }) => {
  await setMockScenario(request, { failHealth: true });

  await page.goto("/");

  await expect(page.getByTestId("api-status-value")).toHaveText("Disconnected");
  await expect(page.getByTestId("dashboard-error-banner")).toContainText("Failed to load dashboard data");
});

test("shows transactions error state when projection data is unavailable", async ({ page, request }) => {
  await setMockScenario(request, { failProjectionMetadata: true });

  await page.goto("/transactions");

  await expect(page.getByRole("heading", { name: "Transactions" })).toBeVisible();
  await expect(page.getByTestId("transactions-error-banner")).toContainText("Failed to load transactions");
});
