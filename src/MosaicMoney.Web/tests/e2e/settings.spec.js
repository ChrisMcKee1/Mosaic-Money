import { test, expect } from '@playwright/test';

test.describe('Settings Page', () => {
  test('should display appearance and security sections', async ({ page }) => {
    await page.goto('/settings');
    
    await expect(page.getByRole('heading', { name: 'Settings', exact: true })).toBeVisible();
    
    // Appearance section (ThemeSwitcher)
    await expect(page.getByText('Theme preference is saved locally in your browser.')).toBeVisible();
    
    // Security section
    await expect(page.getByRole('heading', { name: 'Security & Authentication' })).toBeVisible();
    
    // Since Clerk is not configured in tests, it should show the fallback
    await expect(page.getByText('Authentication Disabled')).toBeVisible();
  });
});