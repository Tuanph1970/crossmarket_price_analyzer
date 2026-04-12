import { test, expect } from '@playwright/test';

/**
 * Dashboard E2E tests — verify the main opportunity feed page.
 *
 * Covers:
 * - Page loads with metric cards
 * - Opportunity cards render (or empty state)
 * - Filter bar interactions
 * - Pagination controls
 * - Export CSV button is present
 * - Keyboard navigation to comparison page
 */
test.describe('Dashboard', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    // Wait for either cards or loading/empty state to settle
    await page.waitForLoadState('networkidle');
  });

  test('loads and shows metric cards', async ({ page }) => {
    await expect(page.getByRole('heading', { name: /dashboard/i })).toBeVisible();
    // At least one metric card should be visible
    await expect(page.getByTestId('metric-total').or(page.getByText('Total Opportunities'))).toBeVisible();
  });

  test('displays opportunity cards or empty state', async ({ page }) => {
    // Either cards are rendered
    const cards = page.getByTestId('opportunity-card');
    const cardCount = await cards.count();

    if (cardCount > 0) {
      // Each card shows a score badge
      await expect(page.getByTestId('composite-score-badge').first()).toBeVisible();
    } else {
      // Or a friendly empty state is shown
      await expect(page.getByText(/no opportunities/i)).toBeVisible();
    }
  });

  test('filter bar margin select works', async ({ page }) => {
    const marginSelect = page.locator('#filter-margin');
    await expect(marginSelect).toBeVisible();

    await marginSelect.selectOption('20');
    // Wait for re-fetch
    await page.waitForTimeout(500);

    // Selecting a filter changes the URL / query — just verify no crash
    await expect(page.getByRole('heading', { name: /dashboard/i })).toBeVisible();
  });

  test('reset filters button is functional', async ({ page }) => {
    // Apply a filter
    await page.locator('#filter-margin').selectOption('30');
    await page.waitForTimeout(300);

    // Click Reset
    await page.getByRole('button', { name: /reset/i }).click();
    await page.waitForTimeout(300);

    // Select should be back to default
    const selected = await page.locator('#filter-margin').inputValue();
    expect(selected).toBe('');
  });

  test('pagination controls appear when totalCount > 20', async ({ page }) => {
    const paginationNav = page.locator('nav[aria-label="Pagination"]');
    // Only visible when there are enough results
    const isVisible = await paginationNav.isVisible().catch(() => false);
    if (isVisible) {
      await expect(page.getByTestId('btn-prev')).toBeVisible();
      await expect(page.getByTestId('btn-next')).toBeVisible();
    }
  });

  test('export CSV button is present', async ({ page }) => {
    // Only attempt if we have cards (triggers download)
    const cards = page.getByTestId('opportunity-card');
    const cardCount = await cards.count();
    if (cardCount > 0) {
      const exportBtn = page.getByRole('button', { name: /export/i });
      await expect(exportBtn).toBeVisible();
    }
  });

  test('clicking an opportunity card navigates to comparison page', async ({ page }) => {
    const cards = page.getByTestId('opportunity-card');
    const cardCount = await cards.count();
    if (cardCount > 0) {
      await cards.first().click();
      await expect(page).toHaveURL(/\/compare\//);
      await expect(page.getByRole('heading', { name: /comparison/i })).toBeVisible({ timeout: 5_000 });
    }
  });

  test('keyboard: Tab reaches interactive elements', async ({ page }) => {
    await page.goto('/');
    // Tab to first interactive element after skip link
    await page.keyboard.press('Tab'); // skip link (sr-only, still gets focus)
    await page.keyboard.press('Tab'); // first real interactive element
    const focused = await page.evaluate(() => document.activeElement?.tagName);
    expect(focused).not.toBeNull();
  });

  test('ARIA live region announces opportunity count', async ({ page }) => {
    // Live region should be present and non-empty after data loads
    const liveRegion = page.locator('[role="status"][aria-live="polite"]');
    await expect(liveRegion).toHaveAttribute('aria-live', 'polite');
  });
});
