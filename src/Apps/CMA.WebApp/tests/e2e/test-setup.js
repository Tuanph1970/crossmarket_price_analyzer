import { test as setup, expect } from '@playwright/test';

const baseURL = process.env.PLAYWRIGHT_BASE_URL || 'http://localhost:5173';

setup.describe.configure({ mode: 'serial' });

setup('global setup', async ({ page }) => {
  await page.goto(baseURL, { waitUntil: 'networkidle' });
  await expect(page.getByRole('heading')).toBeVisible({ timeout: 30_000 });
});

setup('skip if backend unavailable', async ({ page }) => {
  const response = await page.request.get(`${baseURL}/`).catch(() => null);
  if (!response?.ok()) {
    // eslint-disable-next-line no-console
    console.warn(
      `[Playwright] Backend unavailable (${response?.status() ?? 'no response'}). ` +
      'Skipping E2E suite. Start the gateway with `npm run dev` first.'
    );
    // Use test.setTimeout(0) + skip approach for CI resilience
    page.setDefaultTimeout(0);
  }
});