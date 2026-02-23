import { expect, test } from "@playwright/test";
import { resetMockApi, setMockScenario } from "./support/mockApi";

test.beforeEach(async ({ request }) => {
  await resetMockApi(request);
});

test("supports approve and reclassify flows in needs-review queue", async ({ page }) => {
  await page.goto("/needs-review");

  await expect(page.getByRole("heading", { name: "Needs Review" })).toBeVisible();

  const firstItem = page.getByTestId("needs-review-item-nr-100");
  await expect(firstItem).toBeVisible();
  await firstItem.getByTestId("needs-review-approve-nr-100").click();

  await expect(page.getByTestId("needs-review-item-nr-100")).toHaveCount(0);

  const secondItem = page.getByTestId("needs-review-item-nr-200");
  await expect(secondItem).toBeVisible();
  await secondItem.getByTestId("needs-review-reclassify-nr-200").click();
  await secondItem.getByTestId("needs-review-subcategory-nr-200").fill("33333333-3333-3333-3333-333333333333");
  await secondItem.getByTestId("needs-review-reclassify-reason-nr-200").fill("Deterministic reclassification for test coverage.");
  await secondItem.getByTestId("needs-review-confirm-reclassify-nr-200").click();

  await expect(page.getByTestId("needs-review-empty")).toBeVisible();
});

test("supports reject routing flow without removing queue item", async ({ page }) => {
  await page.goto("/needs-review");

  const item = page.getByTestId("needs-review-item-nr-100");
  await item.getByTestId("needs-review-reject-nr-100").click();
  await item.getByTestId("needs-review-reject-reason-nr-100").fill("Escalating to user for manual decision.");
  await item.getByTestId("needs-review-confirm-reject-nr-100").click();

  await expect(page.getByTestId("needs-review-item-nr-100")).toBeVisible();
  await expect(page.getByText("Escalating to user for manual decision.")).toBeVisible();
});

test("shows needs-review error banner when queue endpoint fails", async ({ page, request }) => {
  await setMockScenario(request, { failNeedsReview: true });

  await page.goto("/needs-review");

  await expect(page.getByRole("heading", { name: "Needs Review" })).toBeVisible();
  await expect(page.getByTestId("needs-review-error-banner")).toContainText("Failed to load transactions");
});
