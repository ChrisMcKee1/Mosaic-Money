import { test, expect } from '@playwright/test';

test.describe('Accounts Page', () => {
  test('should display Add Account CTA and navigate to Plaid onboarding', async ({ page }) => {
    // Navigate to the accounts page
    await page.goto('/accounts');

    // Check if the page title is visible
    await expect(page.locator('h1')).toHaveText('Accounts');

    // Check if the Add Account button is visible
    const addAccountButton = page.locator('a', { hasText: 'Add Account' });
    await expect(addAccountButton).toBeVisible();

    // Click the Add Account button
    await addAccountButton.click();

    // Verify navigation to Plaid onboarding
    await expect(page).toHaveURL(/\/onboarding\/plaid/);
    await expect(page.locator('h1')).toHaveText('Connect Your Bank');
  });
});