import { expect, test } from "@playwright/test";
import { resetMockApi } from "./support/mockApi";

test.beforeEach(async ({ request }) => {
  await resetMockApi(request);
});

test("desktop navigation routes between dashboard and transactions", async ({ page, isMobile }) => {
  test.skip(isMobile, "Desktop-only navigation coverage");

  await page.goto("/");

  await expect(page.getByRole("navigation", { name: "Main Navigation" })).toBeVisible();
  await page.getByRole("link", { name: "Transactions" }).click();
  await expect(page).toHaveURL(/\/transactions$/);
  await expect(page.getByRole("heading", { name: "Transactions" })).toBeVisible();
});

test("mobile bottom navigation routes to needs-review", async ({ page, isMobile }) => {
  test.skip(!isMobile, "Mobile-only navigation coverage");

  await page.goto("/");

  await expect(page.getByRole("navigation", { name: "Mobile Navigation" })).toBeVisible();
  await page.getByRole("link", { name: "Needs Review" }).click();
  await expect(page).toHaveURL(/\/needs-review$/);
  await expect(page.getByRole("heading", { name: "Needs Review" })).toBeVisible();
});
