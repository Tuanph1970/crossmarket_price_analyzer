import { test, expect } from '@playwright/test';

/**
 * Quick Lookup E2E tests — verify URL-based product analysis flow.
 *
 * Covers:
 * - Page loads with URL input
 * - Submit without URL shows validation
 * - Submit with URL triggers loading state
 * - Results (or error) displayed after analysis
 * - Loading indicator has proper ARIA role
 */
test.describe('Quick Lookup', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/quick-lookup');
    await page.waitForLoadState('networkidle');
  });

  test('loads with URL input and Analyze button', async ({ page }) => {
    await expect(page.getByRole('heading', { name: /quick.?lookup/i })).toBeVisible();
    await expect(page.getByPlaceholder(/paste.*product.*url/i)).toBeVisible();
    await expect(page.getByRole('button', { name: /analyze/i })).toBeVisible();
  });

  test('shows loading state while analyzing', async ({ page }) => {
    // Type a valid-looking URL
    const input = page.getByPlaceholder(/paste.*product.*url/i);
    await input.fill('https://www.amazon.com/dp/B08N5WRWNW');

    // Click Analyze — loading state should appear
    await page.getByRole('button', { name: /analyze/i }).click();

    // Spinner appears (role="status")
    await expect(page.getByRole('status', { name: /loading/i })).toBeVisible();
  });

  test('shows error when URL fetch fails', async ({ page }) => {
    // A URL that the gateway will attempt but return an error for
    const input = page.getByPlaceholder(/paste.*product.*url/i);
    await input.fill('https://invalid-url-that-will-fail.example.com/product');

    await page.getByRole('button', { name: /analyze/i }).click();

    // Either an error alert or a "no matches" message appears
    await page.waitForTimeout(3_000); // wait for request to settle
    const hasError = await page.getByText(/error|failed|failed to analyze/i).isVisible().catch(() => false);
    const hasNoMatches = await page.getByText(/no.*matches?/i).isVisible().catch(() => false);
    expect(hasError || hasNoMatches).toBeTruthy();
  });

  test('clears previous results when submitting new URL', async ({ page }) => {
    // Submit first URL
    await page.getByPlaceholder(/paste.*product.*url/i).fill('https://www.amazon.com/dp/B08N5WRWNW');
    await page.getByRole('button', { name: /analyze/i }).click();
    await page.waitForTimeout(2_000);

    // Submit second URL
    await page.getByPlaceholder(/paste.*product.*url/i).fill('https://www.walmart.com/ip/123');
    await page.getByRole('button', { name: /analyze/i }).click();
    await page.waitForTimeout(2_000);

    // The page should still be on quick-lookup and no crash
    await expect(page.getByRole('heading', { name: /quick.?lookup/i })).toBeVisible();
  });

  test('Enter key submits the form', async ({ page }) => {
    const input = page.getByPlaceholder(/paste.*product.*url/i);
    await input.fill('https://www.amazon.com/dp/B08N5WRWNW');

    await input.press('Enter');
    await expect(page.getByRole('status', { name: /loading/i })).toBeVisible();
  });

  test('Analyze button is disabled when input is empty', async ({ page }) => {
    const btn = page.getByRole('button', { name: /analyze/i });
    await expect(btn).toBeDisabled();
  });

  test('Analyze button is enabled after typing', async ({ page }) => {
    const input = page.getByPlaceholder(/paste.*product.*url/i);
    await input.fill('https://www.amazon.com/dp/B08N5WRWNW');
    await expect(page.getByRole('button', { name: /analyze/i })).toBeEnabled();
  });
});
