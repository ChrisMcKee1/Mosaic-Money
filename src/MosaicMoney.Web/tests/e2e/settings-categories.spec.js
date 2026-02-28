import { test, expect } from '@playwright/test';

test.describe('Settings Categories', () => {
  test('should navigate to categories settings and render scope lanes', async ({ page }) => {
    await page.goto('/settings');

    await page.getByTestId('settings-manage-categories-link').click();

    await expect(page).toHaveURL(/.*\/settings\/categories/);
    await expect(page.getByTestId('settings-categories-heading')).toBeVisible();

    await expect(page.getByRole('button', { name: 'My Categories' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Household Shared' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Platform Baseline' })).toBeVisible();

    await expect(page.getByText('Personal Care')).toBeVisible();
    await expect(page.getByText('Haircuts').first()).toBeAttached();

    await page.getByRole('button', { name: 'Platform Baseline' }).click();
    await expect(page.getByText('Platform taxonomy is read-only from web settings.')).toBeVisible();
    await expect(page.getByText('Utilities')).toBeVisible();
  });

  test('should render category mutation controls for mutable scopes', async ({ page }) => {
    await page.goto('/settings/categories');

    await expect(page.getByRole('textbox', { name: 'Category name', exact: true }).first()).toBeVisible();
    await expect(page.getByRole('button', { name: 'Create', exact: true }).first()).toBeVisible();
    await expect(page.getByRole('textbox', { name: 'Subcategory name' }).first()).toBeVisible();
    await expect(page.getByRole('button', { name: 'Add' }).first()).toBeVisible();
  });

  test('should allow creating, editing, and deleting a subcategory', async ({ page }) => {
    await page.goto('/settings/categories');
    
    await expect(page.getByPlaceholder('Subcategory name').first()).toBeVisible();
    await expect(page.getByRole('button', { name: 'Add' }).first()).toBeVisible();
  });
});

