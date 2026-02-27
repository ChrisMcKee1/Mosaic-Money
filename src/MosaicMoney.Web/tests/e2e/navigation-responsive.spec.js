import { expect, test } from "@playwright/test";
import { resetMockApi } from "./support/mockApi";

test.beforeEach(async ({ request }) => {
  await resetMockApi(request);
});

test("desktop navigation routes between dashboard and transactions", async ({ page, isMobile }) => {
  test.skip(isMobile, "Desktop-only navigation coverage");

  await page.goto("/");

  const transactionsLink = page.getByRole("link", { name: "Transactions" });
  await expect(transactionsLink).toBeVisible();
  await transactionsLink.click();
  await expect(page).toHaveURL(/\/transactions$/);
  await expect(page.getByRole("heading", { name: "Transactions" })).toBeVisible();
});

test("mobile bottom navigation routes to needs-review", async ({ page, isMobile }) => {
  test.skip(!isMobile, "Mobile-only navigation coverage");

  await page.goto("/");

  const needsReviewLink = page.getByRole("link", { name: "Needs Review" });
  await expect(needsReviewLink).toBeVisible();
  await needsReviewLink.click();
  await expect(page).toHaveURL(/\/needs-review$/);
  await expect(page.getByRole("heading", { name: "Needs Review" })).toBeVisible();
});
