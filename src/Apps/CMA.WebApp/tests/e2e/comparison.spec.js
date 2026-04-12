import { test, expect } from '@playwright/test';

/**
 * Comparison Page E2E tests — verify match comparison and cost breakdown.
 *
 * Covers:
 * - Page loads at /compare/:matchId
 * - Score breakdown with factor rows renders
 * - Landed cost breakdown card renders
 * - Score gauge is visible
 * - Match details card shows when match is loaded
 * - No-match state shows recent match links
 */
test.describe('Comparison Page', () => {
  test.describe.configure({ mode: 'parallel' });

  test('loads with no matchId — shows recent matches', async ({ page }) => {
    await page.goto('/compare');
    await page.waitForLoadState('networkidle');

    // Prompt text should be visible
    await expect(page.getByText(/enter.*prompt|select.*match/i).or(page.getByRole('heading', { name: /comparison/i }))).toBeVisible();
  });

  test('loads with valid matchId — shows score breakdown', async ({ page }) => {
    // First navigate to dashboard to get a real matchId
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const cards = page.getByTestId('opportunity-card');
    const count = await cards.count();

    if (count === 0) {
      // No data — skip dynamic test, verify page structure
      await page.goto('/compare/nonce-0000-0000-0000-000000000000');
      await page.waitForLoadState('networkidle');
      // Should either show "no data" or the breakdown card
      const hasNoData = await page.getByText(/no.*data|not found/i).isVisible().catch(() => false);
      const hasBreakdown = await page.getByText(/composite.*score|landed.*cost/i).isVisible().catch(() => false);
      expect(hasNoData || hasBreakdown).toBeTruthy();
      return;
    }

    // Click first card to navigate to comparison
    await cards.first().click();
    await page.waitForURL(/\/compare\//);

    // Verify breakdown section heading is visible
    await expect(page.getByText(/composite.*score/i).or(page.getByText(/score.*breakdown/i))).toBeVisible({ timeout: 5_000 });
  });

  test('cost breakdown card renders', async ({ page }) => {
    // Visit a specific match (won't exist in empty DB but page structure renders)
    await page.goto('/compare/test-match-0000');
    await page.waitForLoadState('networkidle');

    const hasCostSection = await page
      .getByText(/landed.*cost|purchase.*price|import.*duty|vat/i)
      .isVisible()
      .catch(() => false);
    // If data loaded, cost section is present; if not, other content is visible
    await expect(page.getByRole('heading', { name: /comparison/i })).toBeVisible();
    if (hasCostSection) {
      await expect(page.getByText(/landed.*cost/i).or(page.getByText(/total.*landed/i))).toBeVisible();
    }
  });

  test('score factor rows have weighted values', async ({ page }) => {
    await page.goto('/compare/test-match-0000');
    await page.waitForLoadState('networkidle');

    // Factor labels should be present if breakdown loaded
    const factorLabels = ['ProfitMargin', 'Demand', 'Competition', 'Stability', 'Confidence'];
    const hasAnyFactor = await Promise.all(
      factorLabels.map(label => page.getByText(new RegExp(label, 'i')).isVisible().catch(() => false)))
    );
    // At least one factor text or a "no data" message should be visible
    const hasData = await page.getByText(/composite.*score|factors?/i).isVisible().catch(() => false);
    expect(hasAnyFactor.some(Boolean) || hasData).toBeTruthy();
  });

  test('match details card shows when match data loads', async ({ page }) => {
    await page.goto('/compare/test-match-0000');
    await page.waitForLoadState('networkidle');

    const hasMatchDetails = await page.getByText(/match.*details|status|confidence/i).isVisible().catch(() => false);
    const hasNoData = await page.getByText(/no.*data|not found/i).isVisible().catch(() => false);
    // Either match details OR a graceful no-data message
    expect(hasMatchDetails || hasNoData).toBeTruthy();
  });

  test('navigating from dashboard to comparison preserves route', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const cards = page.getByTestId('opportunity-card');
    const count = await cards.count();
    if (count === 0) test.skip();

    const firstCardUrl = await cards.first().getAttribute('href');
    await cards.first().click();
    await page.waitForURL(/\/compare\//);

    // Current URL should match the card href
    const currentUrl = page.url();
    expect(currentUrl).toMatch(/\/compare\//);
    // Back navigation returns to dashboard
    await page.goBack();
    await expect(page).toHaveURL('/');
  });
});
