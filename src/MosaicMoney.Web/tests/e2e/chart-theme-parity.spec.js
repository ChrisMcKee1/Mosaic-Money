import { expect, test } from "@playwright/test";
import { resetMockApi } from "./support/mockApi";

test.beforeEach(async ({ request }) => {
  await resetMockApi(request);
});

async function setTheme(page, theme) {
  await page.goto("/settings");
  await page
    .locator("#main-content")
    .getByRole("button", { name: new RegExp(`^${theme}$`, "i") })
    .first()
    .click();

  await expect
    .poll(async () => page.evaluate(() => document.documentElement.dataset.theme))
    .toBe(theme);
}

test("dashboard charts stay interactive in light and dark themes", async ({ page, isMobile }) => {
  test.skip(isMobile, "Desktop-only chart interaction parity coverage");

  for (const theme of ["light", "dark"]) {
    await setTheme(page, theme);

    await page.goto("/");
    await expect(page.getByRole("heading", { name: "Dashboard" })).toBeVisible();

    const chartCanvases = page.locator(".apexcharts-canvas");
    await expect(chartCanvases.first()).toBeVisible();
    await expect(chartCanvases.nth(1)).toBeVisible();

    if (!isMobile) {
      await chartCanvases.first().hover({ position: { x: 80, y: 64 } });
      await expect(page.locator(".apexcharts-tooltip").first()).toBeVisible();
    }

    await expect
      .poll(async () => page.evaluate(() => document.documentElement.dataset.theme))
      .toBe(theme);
  }
});

test("reporting pages render apex chart surfaces", async ({ page, isMobile }) => {
  await setTheme(page, "light");

  const routes = isMobile
    ? ["/categories", "/investments", "/recurrings"]
    : ["/accounts", "/categories", "/investments", "/recurrings"];

  for (const route of routes) {
    await page.goto(route);
    await expect(page.locator(".apexcharts-canvas").first()).toBeVisible();
  }
});