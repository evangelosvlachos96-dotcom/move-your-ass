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

const ADMIN_ROUTES = [
  '/dashboard',
  '/videos',
  '/about',
  '/about/edit',
  '/admin/videos',
  '/admin/videos/new',
  '/admin/users',
  '/profile',
  '/change-password',
];

const CLIENT_ROUTES = ['/dashboard', '/videos', '/about', '/profile', '/change-password'];

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
        const cls =
          typeof element.className === 'string' && element.className
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
  await page
    .locator('app-shell, .auth-page, mat-card, .about, .editor-page')
    .first()
    .waitFor({ state: 'visible' });
  // A route inside the shell renders its own content after the shell appears, so waiting for
  // the shell alone can measure an empty page. Wait for the loading state to clear too.
  await page
    .locator('main [role="status"]:has-text("Φόρτωση")')
    .first()
    .waitFor({ state: 'detached' })
    .catch(() => undefined);
  // The icon font changes what a mat-icon measures: before it loads the element holds the
  // literal text "account_circle", which is a different size from the glyph it becomes.
  await page.evaluate(() => document.fonts?.ready);
  // One frame, so layout has been applied before anything is measured.
  await page.evaluate(() => new Promise(requestAnimationFrame));
}

/** True for the phone projects in either engine: 'phone' and 'phone-webkit'. */
function isPhone(testInfo: { project: { name: string } }): boolean {
  return testInfo.project.name.startsWith('phone');
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

    await page.waitForURL(/\/(dashboard|change-password)/, {
      timeout: 20_000,
      waitUntil: 'commit',
    });
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

    // startsWith, not ===: the WebKit projects are 'phone-webkit' and 'tablet-webkit'.
    if (isPhone(testInfo)) {
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
    const selector = isPhone(testInfo) ? '.shell__bottom-item' : '.shell__nav-item';
    const items = page.locator(selector);
    const count = await items.count();
    expect(count).toBeGreaterThan(0);

    for (let i = 0; i < count; i++) {
      const box = await items.nth(i).boundingBox();
      expect(box!.height, `${selector} #${i} is ${box!.height}px tall`).toBeGreaterThanOrEqual(44);
    }
  });

  test('the footer credits the author and clears the bottom bar', async ({}, testInfo) => {
    const page = admin();
    await page.goto('/dashboard');
    await settle(page);

    const link = page.getByRole('link', { name: 'Evangelos Vlachos' });
    await expect(link).toBeVisible();
    await expect(link).toHaveAttribute('href', 'https://www.linkedin.com/in/evanvlac/');
    await expect(link).toHaveAttribute('target', '_blank');
    // Without noopener the opened page gets a handle on this one.
    await expect(link).toHaveAttribute('rel', /noopener/);
    await expect(link).toHaveAttribute('rel', /noreferrer/);

    if (isPhone(testInfo)) {
      // The bottom bar is fixed over the page, so the question is whether the credit line can
      // ever be read — which is only decided once the page is scrolled all the way down. Left
      // unscrolled the footer simply sits below the fold, which proves nothing either way.
      await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight));
      await page.locator('.site-footer small').scrollIntoViewIfNeeded();

      // Measured on the text rather than the footer box, because the box carries the clearance.
      const credit = await page.locator('.site-footer small').boundingBox();
      const nav = await page.locator('.shell__bottom').boundingBox();
      expect(credit!.y + credit!.height).toBeLessThanOrEqual(nav!.y);
    }
  });

  test('the profile icon is centred inside its round button', async () => {
    const page = admin();
    await page.goto('/dashboard');
    await settle(page);

    // Measured as the four gaps between the glyph and the circle around it, rather than as two
    // centre points: when this fails, the gaps say which side it is leaning to.
    const gaps = await page.locator('.shell__user').evaluate((el) => {
      const outer = el.getBoundingClientRect();
      const inner = el.querySelector('mat-icon')!.getBoundingClientRect();
      const round = (n: number) => Math.round(n * 100) / 100;
      return {
        left: round(inner.left - outer.left),
        right: round(outer.right - inner.right),
        top: round(inner.top - outer.top),
        bottom: round(outer.bottom - inner.bottom),
        button: round(outer.height),
        icon: round(inner.height),
      };
    });

    const detail = JSON.stringify(gaps);
    // One pixel of tolerance for sub-pixel layout; anything more is visible as an off-centre glyph.
    expect(Math.abs(gaps.left - gaps.right), detail).toBeLessThanOrEqual(1);
    expect(Math.abs(gaps.top - gaps.bottom), detail).toBeLessThanOrEqual(1);

    // And it is the generic account icon, never a photograph.
    await expect(page.locator('.shell__user-icon')).toHaveText('account_circle');
    await expect(page.locator('.shell__user img')).toHaveCount(0);
  });

  test('the About page is editable by an admin and shows no trainer photo in the top bar', async () => {
    const page = admin();
    await page.goto('/about');
    await settle(page);

    // Editing is its own route now, so this is a link rather than a mode toggle.
    const edit = page.getByRole('link', { name: 'Επεξεργασία' });
    await expect(edit).toBeVisible();
    // The portrait belongs to the page, not the shell.
    await expect(page.locator('.about-hero__portrait')).toBeVisible();
    await expect(page.locator('.shell__user img')).toHaveCount(0);

    await edit.click();
    await expect(page).toHaveURL(/\/about\/edit/);
    await expect(page.getByRole('heading', { name: 'Επεξεργασία σελίδας' })).toBeVisible();

    // And there is a way back that is not the browser's own button.
    await page.getByRole('button', { name: 'Πίσω' }).click();
    await expect(page).toHaveURL(/\/about$/);
  });

  test('a destructive action opens the branded dialog, never a browser one', async () => {
    const page = admin();
    failOnNativeDialog(page);
    await page.goto('/admin/videos');
    await settle(page);

    const menu = page.locator('.vcard__more').first();
    test.skip(!(await menu.count()), 'no video in the library to delete');

    await menu.click();
    const deleteButton = page.getByRole('menuitem', { name: 'Διαγραφή' });
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

  test('the About page is read-only for a client and offers the contact form', async () => {
    const page = client();
    await page.goto('/about');
    await settle(page);

    await expect(page.getByRole('link', { name: 'Επεξεργασία' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Αλλαγή φωτογραφίας' })).toHaveCount(0);
    await expect(page.getByRole('textbox', { name: 'Θέμα' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Αποστολή' })).toBeVisible();
  });

  test('admin destinations are absent from a client navigation', async () => {
    const page = client();
    await page.goto('/dashboard');
    await settle(page);
    await expect(page.getByRole('link', { name: 'Χρήστες' })).toHaveCount(0);
    await expect(page.getByRole('link', { name: 'Βίντεο', exact: true })).toHaveCount(0);
  });
});
