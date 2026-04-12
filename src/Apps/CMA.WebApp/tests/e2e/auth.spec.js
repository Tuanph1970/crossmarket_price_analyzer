/**
 * P4-T02: Playwright regression suite — authentication flows.
 * Tests: register, login, logout, protected routes, and token persistence.
 */
import { test, expect } from '@playwright/test';

const BASE = process.env.PLAYWRIGHT_BASE_URL || 'http://localhost:5173';

// ── Helpers ───────────────────────────────────────────────────────────────────

async function register(page, { email, password, fullName }) {
  await page.goto(`${BASE}/register`);
  await page.getByLabel(/full name/i).fill(fullName);
  await page.getByLabel(/email/i).fill(email);
  await page.getByLabel(/^password$/i).fill(password);
  await page.getByRole('button', { name: /create account/i }).click();
}

async function login(page, { email, password }) {
  await page.goto(`${BASE}/login`);
  await page.getByLabel(/email/i).fill(email);
  await page.getByLabel(/^password$/i).fill(password);
  await page.getByRole('button', { name: /sign in/i }).click();
}

// ── Register ─────────────────────────────────────────────────────────────────

test.describe('Registration', () => {
  test('should register a new user and redirect to dashboard', async ({ page }) => {
    const email = `e2e_${Date.now()}@example.com`;
    await register(page, { email, password: 'TestPass123!', fullName: 'E2E Test' });

    // Should redirect to dashboard
    await expect(page).toHaveURL(/\/$|\/dashboard/);
    // User initials should appear in header
    await expect(page.locator('header')).toContainText('E2E');
  });

  test('should show error on duplicate email', async ({ page }) => {
    const email = `dup_${Date.now()}@example.com`;
    await register(page, { email, password: 'TestPass123!', fullName: 'First' });

    // Register same email again
    await page.goto(`${BASE}/register`);
    await page.getByLabel(/full name/i).fill('Second');
    await page.getByLabel(/email/i).fill(email);
    await page.getByLabel(/^password$/i).fill('TestPass123!');
    await page.getByRole('button', { name: /create account/i }).click();

    await expect(page.getByRole('alert')).toBeVisible();
  });

  test('should validate required fields', async ({ page }) => {
    await page.goto(`${BASE}/register`);
    await page.getByRole('button', { name: /create account/i }).click();

    await expect(page.getByText(/at least 2 characters/i)).toBeVisible();
  });

  test('should validate email format', async ({ page }) => {
    await page.goto(`${BASE}/register`);
    await page.getByLabel(/full name/i).fill('Test User');
    await page.getByLabel(/email/i).fill('not-an-email');
    await page.getByLabel(/^password$/i).fill('TestPass123!');
    await page.getByRole('button', { name: /create account/i }).click();

    await expect(page.getByText(/valid email/i)).toBeVisible();
  });
});

// ── Login ─────────────────────────────────────────────────────────────────────

test.describe('Login', () => {
  test('should log in with valid credentials', async ({ page }) => {
    const email = `login_${Date.now()}@example.com`;
    await register(page, { email, password: 'LoginPass456!', fullName: 'Login User' });

    // Logout first
    await page.locator('button[title*="og out"], button[aria-label*="og out"]').first().click();
    await expect(page).toHaveURL(/\/login/);

    // Login
    await login(page, { email, password: 'LoginPass456!' });
    await expect(page).toHaveURL(/\/$|\/dashboard/);
  });

  test('should show error on invalid password', async ({ page }) => {
    const email = `wrongpw_${Date.now()}@example.com`;
    await register(page, { email, password: 'CorrectPass1!', fullName: 'Wrong PW' });

    await page.goto(`${BASE}/login`);
    await page.getByLabel(/email/i).fill(email);
    await page.getByLabel(/^password$/i).fill('WrongPassword');
    await page.getByRole('button', { name: /sign in/i }).click();

    await expect(page.getByRole('alert')).toContainText(/invalid/i);
  });

  test('should show error on unknown email', async ({ page }) => {
    await page.goto(`${BASE}/login`);
    await page.getByLabel(/email/i).fill(`nobody_${Date.now()}@example.com`);
    await page.getByLabel(/^password$/i).fill('AnyPassword123!');
    await page.getByRole('button', { name: /sign in/i }).click();

    await expect(page.getByRole('alert')).toBeVisible();
  });
});

// ── Protected Routes ─────────────────────────────────────────────────────────

test.describe('Route Protection', () => {
  test('should redirect unauthenticated users to /login', async ({ page }) => {
    await page.goto(`${BASE}/`);
    await expect(page).toHaveURL(/\/login/);
  });

  test('should allow authenticated users to access dashboard', async ({ page }) => {
    const email = `protected_${Date.now()}@example.com`;
    await register(page, { email, password: 'Protected1!', fullName: 'Protected User' });

    // Stay on dashboard
    await expect(page).toHaveURL(/\/$|\/dashboard/);
  });

  test('should allow authenticated users to access watchlist', async ({ page }) => {
    const email = `watch_${Date.now()}@example.com`;
    await register(page, { email, password: 'Watchlist1!', fullName: 'Watch User' });

    await page.goto(`${BASE}/watchlist`);
    await expect(page).toHaveURL(/\/watchlist/);
    await expect(page.getByRole('heading', { name: /watchlist/i })).toBeVisible();
  });

  test('should allow authenticated users to access profile', async ({ page }) => {
    const email = `profile_${Date.now()}@example.com`;
    await register(page, { email, password: 'Profile1!', fullName: 'Profile User' });

    await page.goto(`${BASE}/profile`);
    await expect(page).toHaveURL(/\/profile/);
  });
});

// ── Session Persistence ───────────────────────────────────────────────────────

test.describe('Session Persistence', () => {
  test('should persist session across page reloads', async ({ browser }) => {
    const email = `persist_${Date.now()}@example.com`;
    const ctx   = await browser.newContext();
    const page  = await ctx.newPage();

    await register(page, { email, password: 'Persist1!', fullName: 'Persist User' });
    await expect(page).toHaveURL(/\/$|\/dashboard/);

    // Reload the page — session should survive
    await page.reload();
    await expect(page).toHaveURL(/\/$|\/dashboard/);

    await ctx.close();
  });

  test('should log out and clear session', async ({ page }) => {
    const email = `logout_${Date.now()}@example.com`;
    await register(page, { email, password: 'Logout1!', fullName: 'Logout User' });

    // Logout
    const logoutBtn = page.locator('button[title*="og out"], button[aria-label*="og out"]').first();
    await logoutBtn.click();

    await expect(page).toHaveURL(/\/login/);

    // Trying to access protected page should redirect again
    await page.goto(`${BASE}/`);
    await expect(page).toHaveURL(/\/login/);
  });
});
