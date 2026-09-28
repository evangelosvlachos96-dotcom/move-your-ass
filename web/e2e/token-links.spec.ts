import { Page, expect, test } from '@playwright/test';

/**
 * The bug: signed in as the admin, open an invitation link in the same browser, set the client's
 * password, press "log in" — and land in the **admin's** dashboard, because the admin's refresh
 * cookie was still there the whole time. The password had been set for one account while the
 * browser stayed logged in as another.
 *
 * Opening a password-token page now signs out whoever is there first, server-side.
 */

const ADMIN = {
  email: process.env['E2E_ADMIN_EMAIL'],
  password: process.env['E2E_ADMIN_PASSWORD'],
};

test.describe('password token links end the current session', () => {
  test.skip(!ADMIN.email || !ADMIN.password, 'E2E_ADMIN_EMAIL / E2E_ADMIN_PASSWORD not set');
  test.describe.configure({ mode: 'serial' });

  async function signInAsAdmin(page: Page): Promise<void> {
    await page.goto('/login');
    await page.fill('input[formControlName="email"]', ADMIN.email!);
    await page.fill('input[formControlName="password"]', ADMIN.password!);

    const [response] = await Promise.all([
      page.waitForResponse((r) => r.url().includes('/auth/login')),
      page.click('button[type="submit"]'),
    ]);

    if (response.status() === 429) {
      throw new Error(
        'Sign-in was rate limited (429). Five attempts per (email, IP) per fifteen minutes. ' +
          'Restart the API to clear it — the limiter is in memory.',
      );
    }

    await page.waitForURL(/\/dashboard/, { timeout: 20_000, waitUntil: 'commit' });
    await expect(page.locator('app-shell')).toBeAttached();
  }

  /**
   * Whether the browser still holds a session the server will honour.
   *
   * Asked through the context's request API rather than a fetch inside the page: the page's own
   * fetch is subject to the cookie's SameSite rules, which is a property of the browser, not of
   * whether the session exists. This asks the server directly with the same cookie jar.
   */
  async function refreshStatus(page: Page): Promise<number> {
    const response = await page.context().request.post('/api/auth/refresh', { failOnStatusCode: false });
    return response.status();
  }

  for (const route of ['/set-password', '/reset-password']) {
    test(`${route} signs the previous account out`, async ({ page }) => {
      await signInAsAdmin(page);
      expect(await refreshStatus(page), 'the admin session should be live before we start').toBe(200);

      // A token link, of the shape the emails send: the credential lives in the fragment.
      await page.goto(`${route}#token=${'A'.repeat(64)}`);
      await page.waitForLoadState('domcontentloaded');
      await expect(page.locator('mat-card')).toBeVisible();

      // The old refresh cookie must be dead server-side, not merely forgotten in this tab.
      expect(await refreshStatus(page), 'the admin session should have been revoked').toBe(401);

      // And the page says why, rather than silently logging somebody out.
      await expect(page.getByText(/Αποσυνδεθήκατε από τον προηγούμενο λογαριασμό/)).toBeVisible();

      // Navigating to the dashboard must now land on login, never inside the admin's account.
      await page.goto('/dashboard');
      await page.waitForURL(/\/login/, { timeout: 20_000 });
      await expect(page.locator('input[formControlName="email"]')).toBeVisible();
    });
  }

  test('a second tab of the old session does not stay half-authenticated', async ({ browser }, testInfo) => {
    const context = await browser.newContext({
      baseURL: testInfo.project.use.baseURL,
      viewport: testInfo.project.use.viewport,
    });

    try {
      const first = await context.newPage();
      await signInAsAdmin(first);

      const second = await context.newPage();
      await second.goto('/dashboard');
      await second.waitForLoadState('domcontentloaded');
      await expect(second.locator('app-shell')).toBeAttached();

      // The first tab follows an invitation link, which ends the session for the whole browser.
      await first.goto(`/set-password#token=${'B'.repeat(64)}`);
      await first.waitForLoadState('domcontentloaded');
      await expect(first.locator('mat-card')).toBeVisible();

      // The other tab is told over the shared channel and returns to login on its own.
      await second.waitForURL(/\/login/, { timeout: 20_000 });
    } finally {
      await context.close();
    }
  });
});
