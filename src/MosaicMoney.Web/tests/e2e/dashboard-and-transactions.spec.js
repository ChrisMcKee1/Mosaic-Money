import { expect, test } from "@playwright/test";
import { resetMockApi, setMockScenario } from "./support/mockApi";

test.beforeEach(async ({ request }) => {
  await resetMockApi(request);
});

test("renders projection metrics and transaction context from backend truth", async ({ page }) => {
  await page.goto("/");

  await expect(page.getByRole("heading", { name: "Dashboard" })).toBeVisible();
  await expect(page.getByText("Safe to Spend", { exact: true })).toBeVisible();
  await expect(page.getByText("Household Burn", { exact: true })).toBeVisible();
  await expect(page.getByText("Business Expenses", { exact: true })).toBeVisible();

  await expect(page.getByText("$470.00", { exact: true }).first()).toBeVisible();
  await expect(page.getByText("$1,300.00", { exact: true }).first()).toBeVisible();
  await expect(page.getByText("$80.00", { exact: true }).first()).toBeVisible();
  await expect(page.getByText("Monthly Rent", { exact: true })).toBeVisible();
  await expect(page.getByText("Payroll Deposit", { exact: true })).toBeVisible();

  await expect(page.getByText("2026-02-10", { exact: true })).toBeVisible();
  await expect(page.getByText("Design Software Subscription", { exact: true })).toBeVisible();
  await expect(page.getByText("Business", { exact: true })).toBeVisible();
});

test("shows disconnected state when health check fails", async ({ page, request }) => {
  await setMockScenario(request, { failHealth: true });

  await page.goto("/");

  await expect(page.getByRole("heading", { name: "Dashboard" })).toBeVisible();
  await expect(page.getByText("Failed to load dashboard data. Please ensure the API is running.")).toBeVisible();
});

test("shows transactions error state when projection data is unavailable", async ({ page, request }) => {
  await setMockScenario(request, { failProjectionMetadata: true });

  await page.goto("/transactions");

  await expect(page.getByTestId("transactions-error-banner")).toContainText("Failed to load transactions");
});
