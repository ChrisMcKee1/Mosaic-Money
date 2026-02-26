import { test, expect } from '@playwright/test';

test.describe('Plaid Onboarding Flow', () => {
  test('should display the onboarding page and start the flow', async ({ page }) => {
    // Navigate to the onboarding page
    await page.goto('/onboarding/plaid');

    // Check if the page title is visible
    await expect(page.locator('h1')).toHaveText('Connect Your Bank');

    // Check if the Get Started button is visible
    const getStartedButton = page.locator('button', { hasText: 'Get Started' });
    await expect(getStartedButton).toBeVisible();

    // Click the Get Started button
    await getStartedButton.click();

    // Wait for the Plaid Link button to appear
    const plaidLinkButton = page.locator('button', { hasText: 'Open Plaid Link' });
    await expect(plaidLinkButton).toBeVisible();
  });
});