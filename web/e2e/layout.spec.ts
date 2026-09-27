import { Browser, BrowserContext, Page, expect, test } from '@playwright/test';

/**
 * Two failures this suite exists to catch, on every key route at every viewport:
 *
 *  - **horizontal overflow**, which on a phone turns the whole page into a sideways scroll and is
 *    almost always a fixed width or a long unbroken string somewhere;
 *  - **console errors**, which are usually a template binding that only breaks at one size.
 *
 * It also checks the things that are easy to regress and invisible in a screenshot: the brand
 * appearing exactly once, tap targets big enough to hit, and no native `confirm` dialog.
 */

const ADMIN = {
  email: process.env['E2E_ADMIN_EMAIL'],
  password: process.env['E2E_ADMIN_PASSWORD'],
};

const CLIENT = {
  email: process.env['E2E_CLIENT_EMAIL'],
  password: process.env['E2E_CLIENT_PASSWORD'],
};

/** Routes reachable without signing in. */
const PUBLIC_ROUTES = ['/login', '/register', '/pending', '/forgot-password', '/reset-password'];

const ADMIN_ROUTES = ['/dashboard', '/videos', '/admin/videos', '/admin/users', '/profile', '/change-password'];

const CLIENT_ROUTES = ['/dashboard', '/videos', '/profile', '/change-password'];

/**
 * Console errors that mean something is wrong with the application.
 *
 * The browser writes "Failed to load resource" for every non-2xx response, and the app makes two
 * deliberately-failing calls on a signed-out visit: `/auth/refresh` and `/auth/me` probe for an
 * existing session and answer 401 when there is none. Those are the app working, not breaking, so
 * they are excluded — an uncaught exception or an application `console.error` still fails.
 */
const EXPECTED_CONSOLE_NOISE = [/Failed to load resource/i];

function watchConsole(page: Page): string[] {
  const errors: string[] = [];
  page.on('console', (message) => {
    const text = message.text();
    if (message.type() === 'error' && !EXPECTED_CONSOLE_NOISE.some((rule) => rule.test(text))) {
      errors.push(text);
    }
  });
  page.on('pageerror', (error) => errors.push(`uncaught: ${error.message}`));
  return errors;
}

async function expectNoHorizontalOverflow(page: Page): Promise<void> {
  const overflow = await page.evaluate(() => {
    const doc = document.documentElement;
    if (doc.scrollWidth <= doc.clientWidth) {
      return null;
    }

    // Name the widest offender, so a failure says what to fix rather than just that it broke.
    let worst = { selector: 'unknown', right: 0 };
    for (const element of Array.from(document.body.querySelectorAll<HTMLElement>('*'))) {
      const right = element.getBoundingClientRect().right;
      if (right > worst.right) {
        const id = element.id ? `#${element.id}` : '';
        const cls = typeof element.className === 'string' && element.className
          ? `.${element.className.trim().split(/\s+/).join('.')}`
          : '';
        worst = { selector: `${element.tagName.toLowerCase()}${id}${cls}`, right };
      }
    }

    return { scrollWidth: doc.scrollWidth, clientWidth: doc.clientWidth, worst };
  });

  expect(overflow, `horizontal overflow: ${JSON.stringify(overflow)}`).toBeNull();
}

/**
 * Waits for the route to have actually rendered.
 *
 * Deliberately not `networkidle`: the Angular dev server holds connections open for live reload,
 * so "no network activity for 500ms" is a condition that may never arrive, and waiting for it
 * turned a two-second check into a timeout.
 */
async function settle(page: Page): Promise<void> {
  await page.waitForLoadState('domcontentloaded');
  await page.locator('app-shell, .auth-page, mat-card').first().waitFor({ state: 'visible' });
  // One frame, so layout has been applied before anything is measured.
  await page.evaluate(() => new Promise(requestAnimationFrame));
}

/** A native dialog would hang the run; assert none can appear rather than discovering it. */
function failOnNativeDialog(page: Page): void {
  page.on('dialog', async (dialog) => {
    await dialog.dismiss();
    throw new Error(`native browser dialog appeared: ${dialog.type()} "${dialog.message()}"`);
  });
}

/**
 * One browser context per role per project, signed in once and reused by that role's tests.
 *
 * Two constraints shape this, both of them the application behaving correctly:
 *
 *  - **Refresh tokens rotate.** A context per test, or a saved storageState replayed by several
 *    projects, all present the *same* cookie; the first use rotates it and the rest are replaying
 *    a spent token, which the server is right to reject. One context per project means the cookie
 *    rotates in place, exactly as it does in a real browser.
 *  - **Sign-in is throttled** to five attempts per (email, IP) per fifteen minutes. Three
 *    projects means three logins per account per run, comfortably inside that; a login per test
 *    would not be.
 */
function signedInAs(credentials: { email?: string; password?: string }): () => Page {
  let context: BrowserContext;
  let page: Page;

  test.beforeAll(async ({ browser }: { browser: Browser }, testInfo) => {
    // newContext does NOT inherit the project's viewport, so it has to be passed through — without
    // this every viewport-specific assertion silently runs at the default 1280x720.
    context = await browser.newContext({
      baseURL: testInfo.project.use.baseURL,
      viewport: testInfo.project.use.viewport,
    });
    page = await context.newPage();

    await page.goto('/login');
    await page.fill('input[formControlName="email"]', credentials.email!);
    await page.fill('input[formControlName="password"]', credentials.password!);

    const [response] = await Promise.all([
      page.waitForResponse((r) => r.url().includes('/auth/login')),
      page.click('button[type="submit"]'),
    ]);

    if (response.status() === 429) {
      throw new Error(
        'Sign-in was rate limited (429). Five attempts per (email, IP) per fifteen minutes, so ' +
          'running this suite repeatedly trips it. Wait for the window, or restart the API — the ' +
          'limiter is in memory.',
      );
    }

    await page.waitForURL(/\/(dashboard|change-password)/, { timeout: 20_000, waitUntil: 'commit' });
    await settle(page);
  });

  test.afterAll(async () => {
    await context?.close();
  });

  return () => page;
}

async function checkRoute(page: Page, route: string): Promise<void> {
  const errors = watchConsole(page);
  failOnNativeDialog(page);

  await page.goto(route);
  await settle(page);

  await expectNoHorizontalOverflow(page);
  expect(errors, `console errors on ${route}: ${errors.join(' | ')}`).toEqual([]);
}

test.describe('public routes', () => {
  for (const route of PUBLIC_ROUTES) {
    test(`${route} lays out without overflow or console errors`, async ({ page }) => {
      await checkRoute(page, route);
    });
  }

  test('the login page shows the brand exactly once', async ({ page }) => {
    await page.goto('/login');
    await settle(page);
    // One logo component, and no second wordmark in another style beside it.
    await expect(page.locator('app-brand-logo')).toHaveCount(1);
    await expect(page.getByText('MOVE YOUR ASS', { exact: true })).toHaveCount(0);
  });

  test('the login page offers password recovery', async ({ page }) => {
    await page.goto('/login');
    await page.getByRole('link', { name: 'Ξέχασες τον κωδικό;' }).click();
    await expect(page).toHaveURL(/\/forgot-password/);
  });
});

test.describe('signed in as admin', () => {
  test.describe.configure({ mode: 'serial' });
  test.skip(!ADMIN.email || !ADMIN.password, 'E2E_ADMIN_EMAIL / E2E_ADMIN_PASSWORD not set');
  const admin = signedInAs(ADMIN);

  for (const route of ADMIN_ROUTES) {
    test(`${route} lays out without overflow or console errors`, async () => {
      await checkRoute(admin(), route);
    });
  }

  test('the brand appears exactly once in the shell', async () => {
    const page = admin();
    await page.goto('/dashboard');
    await settle(page);
    await expect(page.locator('app-brand-logo')).toHaveCount(1);
  });

  test('navigation is a bottom bar on phones and a sidebar above', async ({}, testInfo) => {
    const page = admin();
    await page.goto('/dashboard');
    await settle(page);

    if (testInfo.project.name === 'phone') {
      await expect(page.locator('.shell__bottom')).toBeVisible();
      await expect(page.locator('.shell__sidebar')).toHaveCount(0);
      // No hamburger anywhere. Exact, or it also matches the profile button's "Μενού χρήστη".
      await expect(page.getByRole('button', { name: 'Μενού', exact: true })).toHaveCount(0);
    } else {
      await expect(page.locator('.shell__sidebar')).toBeVisible();
      await expect(page.locator('.shell__bottom')).toHaveCount(0);
    }
  });

  test('every navigation target is at least 44px tall', async ({}, testInfo) => {
    const page = admin();
    await page.goto('/dashboard');
    await settle(page);
    const selector = testInfo.project.name === 'phone' ? '.shell__bottom-item' : '.shell__nav-item';
    const items = page.locator(selector);
    const count = await items.count();
    expect(count).toBeGreaterThan(0);

    for (let i = 0; i < count; i++) {
      const box = await items.nth(i).boundingBox();
      expect(box!.height, `${selector} #${i} is ${box!.height}px tall`).toBeGreaterThanOrEqual(44);
    }
  });

  test('a destructive action opens the branded dialog, never a browser one', async () => {
    const page = admin();
    failOnNativeDialog(page);
    await page.goto('/admin/videos');
    await settle(page);

    const deleteButton = page.getByRole('button', { name: 'Διαγραφή' }).first();
    test.skip(!(await deleteButton.count()), 'no video in the library to delete');

    await deleteButton.click();

    const dialog = page.locator('.confirm');
    await expect(dialog).toBeVisible();
    await expect(dialog).toHaveClass(/confirm--destructive/);
    // Cancel is focused, so Enter cannot delete.
    await expect(page.locator('.confirm__button--cancel')).toBeFocused();

    await page.keyboard.press('Escape');
    await expect(dialog).toHaveCount(0);
  });
});

test.describe('signed in as client', () => {
  test.describe.configure({ mode: 'serial' });
  test.skip(!CLIENT.email || !CLIENT.password, 'E2E_CLIENT_EMAIL / E2E_CLIENT_PASSWORD not set');
  const client = signedInAs(CLIENT);

  for (const route of CLIENT_ROUTES) {
    test(`${route} lays out without overflow or console errors`, async () => {
      await checkRoute(client(), route);
    });
  }

  test('admin destinations are absent from a client navigation', async () => {
    const page = client();
    await page.goto('/dashboard');
    await settle(page);
    await expect(page.getByRole('link', { name: 'Χρήστες' })).toHaveCount(0);
    await expect(page.getByRole('link', { name: 'Βίντεο', exact: true })).toHaveCount(0);
  });
});
