import { test, expect } from '@playwright/test';

test.describe('Route Protection', () => {
  test('should bypass protection when Clerk is not configured', async ({ page }) => {
    // In the default test environment, Clerk env vars are not set.
    // Therefore, the middleware should bypass protection and allow access to protected routes.
    
    const response = await page.goto('/accounts');
    
    // Should not redirect to sign-in
    expect(response.status()).toBe(200);
    await expect(page.locator('h1')).toHaveText('Accounts');
  });
});