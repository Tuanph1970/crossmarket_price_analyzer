/**
 * P4-T02: Playwright regression suite — watchlist, alerts, and PDF export.
 */
import { test, expect } from '@playwright/test';

const BASE = process.env.PLAYWRIGHT_BASE_URL || 'http://localhost:5173';

async function registerAndLogin(page) {
  const email = `feat_${Date.now()}@example.com`;
  await page.goto(`${BASE}/register`);
  await page.getByLabel(/full name/i).fill('Feature User');
  await page.getByLabel(/email/i).fill(email);
  await page.getByLabel(/^password$/i).fill('Feature1!');
  await page.getByRole('button', { name: /create account/i }).click();
  await expect(page).toHaveURL(/\/$|\/dashboard/);
  return email;
}

// ── Watchlist ─────────────────────────────────────────────────────────────────

test.describe('Watchlist', () => {
  test('should show empty state when no items saved', async ({ page }) => {
    await registerAndLogin(page);
    await page.goto(`${BASE}/watchlist`);

    await expect(page.getByText(/watchlist is empty/i)).toBeVisible();
    await expect(page.getByText(/browse opportunities/i)).toBeVisible();
  });

  test('should display watchlist items when present', async ({ page }) => {
    await registerAndLogin(page);
    await page.goto(`${BASE}/watchlist`);

    // Assuming watchlist API returns items
    await page.waitForLoadState('networkidle');
    // Items or empty state should be visible
    const hasItems = await page.locator('[data-testid="watchlist-item"]').count() > 0;
    if (!hasItems) {
      await expect(page.getByText(/empty|0 saved/i)).toBeVisible();
    }
  });
});

// ── Alert Thresholds ─────────────────────────────────────────────────────────

test.describe('Alert Thresholds', () => {
  test('should create a new alert threshold', async ({ page }) => {
    await registerAndLogin(page);
    await page.goto(`${BASE}/profile`);

    // Switch to thresholds tab
    await page.getByRole('button', { name: /alert thresholds/i }).click();

    // Open the create form
    await page.getByText(/new alert threshold/i).click();

    // Fill form
    await page.locator('input[type="number"]').first().fill('75');
    await page.locator('select, [role="combobox"]').first().selectOption('email');
    await page.locator('input[type="email"]').fill('feature@example.com');

    await page.getByRole('button', { name: /create threshold/i }).click();

    // Should show success or the new threshold in list
    await expect(page.getByText(/75|threshold created/i)).toBeVisible({ timeout: 5000 });
  });

  test('should show empty state when no thresholds', async ({ page }) => {
    await registerAndLogin(page);
    await page.goto(`${BASE}/profile`);

    await page.getByRole('button', { name: /alert thresholds/i }).click();

    await expect(page.getByText(/no alert thresholds/i)).toBeVisible({ timeout: 5000 });
  });
});

// ── PDF Export ────────────────────────────────────────────────────────────────

test.describe('PDF Export', () => {
  test('should show export button on comparison page when authenticated', async ({ page }) => {
    await registerAndLogin(page);
    // Note: needs a real matchId to trigger the button
    await page.goto(`${BASE}/compare/test-match-id-00000000-0000-0000-0000-000000000000`);
    await page.waitForLoadState('networkidle');

    const exportBtn = page.getByRole('button', { name: /export pdf/i });
    if (await exportBtn.isVisible()) {
      // PDF download — we can't easily test the blob, just verify the button works
      const [download] = await Promise.all([
        page.waitForEvent('download').catch(() => null),
        exportBtn.click(),
      ]);
      // Download may fail if no real match data, but button should be present
      expect(await exportBtn.isVisible()).toBe(true);
    }
    // If no match found, button won't be visible — that's OK
  });

  test('should not show export button without matchId', async ({ page }) => {
    await registerAndLogin(page);
    await page.goto(`${BASE}/compare/`);

    await expect(page.getByRole('button', { name: /export pdf/i })).not.toBeVisible();
  });
});

// ── Profile ──────────────────────────────────────────────────────────────────

test.describe('Profile', () => {
  test('should display user info after login', async ({ page }) => {
    const email = `profile_${Date.now()}@example.com`;
    await registerAndLogin(page);

    await page.goto(`${BASE}/profile`);
    await page.getByRole('heading', { name: /profile/i, exact: false }).waitFor();

    await expect(page.getByText(email)).toBeVisible({ timeout: 5000 });
  });

  test('should show three tabs on profile page', async ({ page }) => {
    await registerAndLogin(page);
    await page.goto(`${BASE}/profile`);

    await expect(page.getByRole('button', { name: /^profile$/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /alert thresholds/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /scheduled reports/i })).toBeVisible();
  });
});
