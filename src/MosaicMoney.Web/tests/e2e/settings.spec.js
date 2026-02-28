import { test, expect } from '@playwright/test';

test.describe('Settings Page', () => {
  test('should display appearance and security sections', async ({ page }) => {
    await page.goto('/settings');
    
    await expect(page.getByRole('heading', { name: 'Settings', exact: true })).toBeVisible();
    
    // Appearance section (ThemeSwitcher)
    await expect(page.getByText('Theme preference is saved locally in your browser.')).toBeVisible();
    
    // Security section
    await expect(page.getByRole('heading', { name: 'Security & Authentication' })).toBeVisible();

    // Category taxonomy section
    await expect(page.getByRole('heading', { name: 'Category Taxonomy' })).toBeVisible();
    await expect(page.getByTestId('settings-manage-categories-link')).toBeVisible();
    
    // Since Clerk is not configured in tests, it should show the fallback
    await expect(page.getByText('Authentication Disabled')).toBeVisible();
  });

  test('should display back navigation link on security settings page', async ({ page }) => {
    // Navigate directly to security page to check the fallback UI 
    await page.goto('/settings/security');
    
    // Check if back link is visible and navigates to the main settings
    const backLink = page.getByRole('link', { name: 'Back to Settings', exact: true });
    await expect(backLink).toBeVisible();
    await expect(backLink).toHaveAttribute('href', '/settings');
    
    // Click the back link
    await backLink.click();
    
    // Check if we are back on main settings
    await expect(page).toHaveURL('/settings');
    await expect(page.getByRole('heading', { name: 'Settings', exact: true })).toBeVisible();
  });
});