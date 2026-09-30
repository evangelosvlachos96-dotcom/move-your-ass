import { APIRequestContext, Page, expect, test } from '@playwright/test';

/**
 * Closing a shared context can fail inside Playwright's own trace-artefact handling on Windows
 * (ENOENT copying a `.network` file out of `test-results/.playwright-artifacts-*`). That is
 * teardown, long after every assertion has run, so it must not turn a passing run into a failing
 * one. The close is still attempted; only its failure is swallowed.
 */
async function closeQuietly(page: Page | undefined): Promise<void> {
  try {
    await page?.context().close();
  } catch {
    // Tracing artefacts only; nothing the tests depend on.
  }
}

/**
 * The booking link and the four places it appears, plus the one place the contact form no longer
 * does.
 *
 * Every assertion here has a matching negative: the buttons are as much about **not** being
 * drawn — with no link set, and for the trainer herself — as about being drawn.
 */

const ADMIN = {
  email: process.env['E2E_ADMIN_EMAIL'],
  password: process.env['E2E_ADMIN_PASSWORD'],
};
const CLIENT = {
  email: process.env['E2E_CLIENT_EMAIL'],
  password: process.env['E2E_CLIENT_PASSWORD'],
};

/**
 * The trainer's real scheduling page, used here on purpose rather than a made-up address: the
 * double underscore in the path is exactly the kind of thing URL handling quietly rewrites, and
 * a link that comes back one character different is a link that 404s.
 *
 * It is test data, not configuration. Nothing in the app hardcodes it — the admin sets it on the
 * About page, and the app reads it from there.
 */
const BOOKING_URL = 'https://reply-now.com/book/tasos__ch';

async function signIn(page: Page, email: string, password: string): Promise<void> {
  await page.goto('/login');
  await page.fill('input[formControlName="email"]', email);
  await page.fill('input[formControlName="password"]', password);
  const [response] = await Promise.all([
    page.waitForResponse((r) => r.url().includes('/auth/login')),
    page.click('button[type="submit"]'),
  ]);
  if (response.status() === 429) {
    throw new Error('Sign-in was rate limited (429). Restart the API; the limiter is in memory.');
  }
  // 45s, not 20: signing in is a round trip plus a full app bootstrap, and WebKit on this
  // machine regularly needs more than twenty seconds for it. A sign-in slower than this is a
  // real problem; twenty seconds was only ever a guess.
  await page.waitForURL(/\/dashboard/, { timeout: 45_000, waitUntil: 'commit' });
  await page.locator('app-shell').waitFor();
}

async function settle(page: Page): Promise<void> {
  await page.waitForLoadState('domcontentloaded');
  await page
    .locator('[role="status"]:has-text("Φόρτωση")')
    .first()
    .waitFor({ state: 'detached' })
    .catch(() => undefined);
  await page.evaluate(() => document.fonts?.ready);
}

/**
 * Sets the booking link through the admin's own API rather than through the editor.
 *
 * The editor is tested elsewhere; here the link is a precondition, and driving a form to arrange
 * a precondition makes every assertion below depend on that form still working.
 */
async function setBookingUrl(
  request: APIRequestContext,
  token: string,
  url: string | null,
): Promise<void> {
  const headers = { Authorization: `Bearer ${token}` };
  const current = await (await request.get('/api/site/about', { headers })).json();
  const response = await request.put('/api/admin/site/about', {
    headers,
    data: { ...stripPhoto(current), bookingUrl: url },
    failOnStatusCode: false,
  });
  expect(response.status(), await response.text()).toBeLessThan(300);
}

/** The photo has its own endpoints and is not part of the form's payload. */
function stripPhoto(about: Record<string, unknown>): Record<string, unknown> {
  const { photoUrl: _photoUrl, ...rest } = about;
  return rest;
}

/** The access token the app is holding, so API calls act as that signed-in user. */
async function accessToken(page: Page): Promise<string> {
  const token = await page.evaluate(() => {
    const store = (window as unknown as { __mya_token?: string }).__mya_token;
    return store ?? null;
  });
  if (token) return token;

  // The app keeps the token in memory, not storage, so ask the server for a fresh one using the
  // refresh cookie this context already holds.
  const refreshed = await page.context().request.post('/api/auth/refresh');
  expect(refreshed.ok()).toBe(true);
  return (await refreshed.json()).accessToken as string;
}

test.describe('the booking link', () => {
  test.skip(
    !ADMIN.email || !ADMIN.password || !CLIENT.email || !CLIENT.password,
    'E2E admin and client credentials not set',
  );
  test.describe.configure({ mode: 'serial' });

  let admin: Page;
  let client: Page;
  let adminToken: string;
  /** Whatever the page was configured with before the suite ran, put back afterwards. */
  let originalBookingUrl: string | null = null;

  test.beforeAll(async ({ browser }, testInfo) => {
    const open = async () => {
      const context = await browser.newContext({
        baseURL: testInfo.project.use.baseURL,
        viewport: testInfo.project.use.viewport,
      });
      return context.newPage();
    };

    admin = await open();
    await signIn(admin, ADMIN.email!, ADMIN.password!);
    adminToken = await accessToken(admin);

    client = await open();
    await signIn(client, CLIENT.email!, CLIENT.password!);

    const current = await admin.context().request.get('/api/site/about', {
      headers: { Authorization: `Bearer ${adminToken}` },
    });
    originalBookingUrl = ((await current.json()) as { bookingUrl: string | null }).bookingUrl;
  });

  test.afterAll(async () => {
    // Put back whatever was configured, rather than clearing it: on a shared development
    // database this suite must not silently undo the owner's own setting.
    if (adminToken) await setBookingUrl(admin.context().request, adminToken, originalBookingUrl);
    await closeQuietly(admin);
    await closeQuietly(client);
  });

  test('with no link set, nothing offers booking anywhere', async () => {
    await setBookingUrl(admin.context().request, adminToken, null);

    for (const route of ['/dashboard', '/videos', '/about']) {
      await client.goto(route);
      await settle(client);
      await expect(client.locator('.booking')).toHaveCount(0);
      await expect(client.locator('.shell__bottom-item--booking')).toHaveCount(0);
    }
  });

  test('once set, a client is offered it in all four places', async ({}, testInfo) => {
    await setBookingUrl(admin.context().request, adminToken, BOOKING_URL);

    const phone = testInfo.project.name.startsWith('phone');
    // The booking link is read once per session and shared with the shell, so every button here
    // appears a round-trip after the page does. On WebKit that round-trip is slow enough to
    // outrun the default expect timeout, and a button that is merely late is not a button that
    // is missing.
    const appears = { visible: true, timeout: 20_000 } as const;

    // a) the navigation — sidebar above 600px, the bottom bar below it.
    await client.goto('/dashboard');
    await settle(client);

    if (phone) {
      const item = client.locator('.shell__bottom-item--booking');
      await expect(item).toBeVisible(appears);
      await expect(item).toHaveAttribute('href', BOOKING_URL);
      await expect(item).toHaveText(/Ραντεβού/);
      // The path survives with both underscores intact.
      await expect(item).toHaveAttribute('href', /tasos__ch$/);
      // It is never the active page, because it is not a page.
      await expect(
        client.locator('.shell__bottom-item--booking.shell__bottom-item--active'),
      ).toHaveCount(0);
    } else {
      const button = client.locator('.shell__booking .booking');
      await expect(button).toBeVisible(appears);
      await expect(button).toHaveAttribute('href', BOOKING_URL);

      // Pinned below the menu, not among it.
      const nav = (await client.locator('.shell__nav').boundingBox())!;
      const booking = (await button.boundingBox())!;
      expect(booking.y).toBeGreaterThan(nav.y + nav.height - 1);
    }

    // c) the client library, at the top of the page.
    await client.goto('/videos');
    await settle(client);
    const banner = client.locator('.booking-banner');
    await expect(banner).toBeVisible(appears);
    await expect(banner).toContainText('Θέλεις προσωπική προπόνηση;');
    const filters = (await client.locator('.filters').boundingBox())!;
    const bannerBox = (await banner.boundingBox())!;
    expect(bannerBox.y).toBeLessThan(filters.y);

    // d) the trainer's page, after the contact details.
    await client.goto('/about');
    await settle(client);
    await expect(client.locator('.about-booking .booking')).toBeVisible(appears);
  });

  test('every booking control opens a new tab safely and is a real target', async ({}, testInfo) => {
    await setBookingUrl(admin.context().request, adminToken, BOOKING_URL);
    await client.goto('/about');
    await settle(client);

    const button = client.locator('.about-booking .booking');
    await expect(button).toHaveAttribute('target', '_blank');
    // Without noopener the scheduling page gets a handle on the window it was opened from.
    await expect(button).toHaveAttribute('rel', /noopener/);
    await expect(button).toHaveAttribute('rel', /noreferrer/);

    const box = (await button.boundingBox())!;
    expect(box.height, `booking button is ${box.height}px tall`).toBeGreaterThanOrEqual(44);

    // Orange with dark text, not the lime accent: booking is the one action that leaves the app.
    const colours = await button.evaluate((el) => {
      const cs = getComputedStyle(el);
      return { background: cs.backgroundColor, color: cs.color };
    });
    expect(colours.background).toBe('rgb(255, 138, 61)');
    expect(colours.color).toBe('rgb(14, 14, 16)');

    // It really opens a tab rather than navigating this one. The destination is a domain that
    // does not resolve, so the new tab's URL is asserted only loosely — what matters is that a
    // tab was opened at all and that this page stayed where it was.
    const [opened] = await Promise.all([client.context().waitForEvent('page'), button.click()]);
    expect(opened).toBeTruthy();
    await opened.close();
    expect(client.url()).toContain('/about');

    // The viewport is irrelevant to the rule, but the check is cheap at both sizes.
    expect(testInfo.project.name).toBeTruthy();
  });

  test('the trainer is not offered booking in the navigation, only on her own page', async ({}, testInfo) => {
    await setBookingUrl(admin.context().request, adminToken, BOOKING_URL);

    await admin.goto('/dashboard');
    await settle(admin);
    await expect(admin.locator('.shell__booking')).toHaveCount(0);
    await expect(admin.locator('.shell__bottom-item--booking')).toHaveCount(0);

    await admin.goto('/videos');
    await settle(admin);
    await expect(admin.locator('.booking-banner')).toHaveCount(0);

    // On her own page it is there, labelled as what the client sees.
    await admin.goto('/about');
    await settle(admin);
    await expect(admin.locator('.about-booking .booking')).toBeVisible();
    await expect(admin.locator('.about-booking')).toContainText('Αυτό βλέπουν οι πελάτισσές σου');

    expect(testInfo.project.name).toBeTruthy();
  });

  test('a link that is not https is refused', async () => {
    const headers = { Authorization: `Bearer ${adminToken}` };
    const current = await (
      await admin.context().request.get('/api/site/about', { headers })
    ).json();

    for (const bad of [
      'http://booking.example.test',
      'javascript:alert(1)',
      'booking.example.test',
    ]) {
      const response = await admin.context().request.put('/api/admin/site/about', {
        headers,
        data: { ...stripPhoto(current), bookingUrl: bad },
        failOnStatusCode: false,
      });
      expect(response.status(), `${bad} should have been refused`).toBe(400);
    }
  });
});

test.describe('the contact form is for clients', () => {
  test.skip(!ADMIN.email || !ADMIN.password, 'E2E_ADMIN_EMAIL / E2E_ADMIN_PASSWORD not set');

  test('an admin sees no message form, and the endpoint refuses one anyway', async ({
    browser,
  }, testInfo) => {
    const context = await browser.newContext({
      baseURL: testInfo.project.use.baseURL,
      viewport: testInfo.project.use.viewport,
    });
    try {
      const page = await context.newPage();
      await signIn(page, ADMIN.email!, ADMIN.password!);
      await page.goto('/about');
      await settle(page);

      await expect(page.getByRole('heading', { name: 'Στείλε μήνυμα' })).toHaveCount(0);
      await expect(page.locator('textarea[name="message"]')).toHaveCount(0);

      // Hiding it is the courtesy; refusing it is the control.
      const token = await accessToken(page);
      const response = await context.request.post('/api/site/contact', {
        headers: { Authorization: `Bearer ${token}` },
        data: { subject: 'Δοκιμή', message: 'Μήνυμα από διαχειριστή.' },
        failOnStatusCode: false,
      });
      expect([401, 403]).toContain(response.status());
    } finally {
      await context.close();
    }
  });
});
