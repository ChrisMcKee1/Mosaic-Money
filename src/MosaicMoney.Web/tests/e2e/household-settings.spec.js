import { test, expect } from '@playwright/test';

test.describe('Household Settings', () => {
  test('should navigate to household settings and render management sections', async ({ page }) => {
    await page.goto('/settings');

    await page.click('text=Manage Household');

    await expect(page).toHaveURL(/.*\/settings\/household/);

    await expect(page.locator('h1')).toHaveText('Household Members');

    await expect(page.getByRole('heading', { name: 'Active Members' })).toBeVisible();

    await expect(page.getByRole('heading', { name: 'Invite Member' })).toBeVisible();
    await expect(page.locator('input[name="email"]')).toBeVisible();
    await expect(page.locator('select[name="role"]')).toBeVisible();
    await expect(page.locator('button[type="submit"]')).toBeVisible();
  });

  test('should enforce invite form behavior based on household availability', async ({ page }) => {
    await page.goto('/settings/household');

    const emailInput = page.locator('input[name="email"]');
    const submitButton = page.locator('button[type="submit"]');

    if (await submitButton.isEnabled()) {
      await emailInput.fill('invalid-email');
      await submitButton.click();
      await expect(emailInput).toHaveValue('invalid-email');
      return;
    }

    await expect(page.locator('text=Create a household first to invite members.')).toBeVisible();
  });
});