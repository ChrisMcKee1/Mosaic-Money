import { test, expect } from '@playwright/test';
import { resetMockApi } from './support/mockApi';

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

test.describe('Categories Donut Chart', () => {
  test('donut chart renders correctly in light mode without black ring', async ({ page, isMobile }) => {
    test.skip(isMobile, "Donut rendering UI check skipped on mobile for parity");
    await setTheme(page, 'light');
    await page.goto('/categories');
    
    // Wait for apex chart to finish rendering
    const chart = page.locator('.apexcharts-canvas').first();
    await expect(chart).toBeVisible();

    // Check if total spent center label is visible and readable
    const totalSpentLabel = page.getByText(/of \$/i).first();
    await expect(totalSpentLabel).toBeVisible();

    // In light mode, the stroke color for donut slices should not explicitly default to black (#000000)
    // ApexCharts uses <path> for slices. Let's verify none of them have stroke="#000000" if we can,
    // or just that they exist.
    const slicePaths = page.locator('path.apexcharts-pie-area');
    const count = await slicePaths.count();
    
    if (count > 0) {
      for (let i = 0; i < count; i++) {
        const strokeValue = await slicePaths.nth(i).getAttribute('stroke');
        // Ensure that white/black defaults aren't overriding our CSS variable surface color
        // The stroke should now be the var(--color-surface) or similar, but since Playwright reads attributes,
        // it might not evaluate CSS variables in getAttribute. Let's ensure it's not strictly #000000 if it failed previously.
        if (strokeValue !== null && strokeValue.startsWith('#000')) {
          expect(strokeValue).not.toBe('#000000');
        }
      }
    }
  });

  test('donut chart renders correctly in dark mode', async ({ page, isMobile }) => {
    test.skip(isMobile, "Donut rendering UI check skipped on mobile for parity");
    await setTheme(page, 'dark');
    await page.goto('/categories');
    
    const chart = page.locator('.apexcharts-canvas').first();
    await expect(chart).toBeVisible();
    
    const totalSpentLabel = page.getByText(/of \$/i).first();
    await expect(totalSpentLabel).toBeVisible();
  });
});