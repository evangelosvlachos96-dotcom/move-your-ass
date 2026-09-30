import { BrowserContext, Page, expect, test } from '@playwright/test';

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
 * Nothing scrolls sideways, on any route, at any width this product is used at.
 *
 * Reported from a real iPhone: every page but the login page was cut off and had to be dragged
 * horizontally. Two causes, and only one of them is visible to a desktop browser pretending to
 * be a phone:
 *
 *  1. **Real overflow** — an element wider than the viewport. `documentElement.scrollWidth`
 *     answers that, and this file asserts it on every route, for both roles, at four widths, in
 *     both engines, with dialogs open, with the editor open, and with content long enough to be
 *     awkward.
 *
 *  2. **iOS Safari's focus zoom** — tapping a control whose computed font-size is under 16px
 *     zooms the page in and never zooms back out, after which everything is cut off and scrolls
 *     sideways. **No emulator reproduces this**, including Playwright's WebKit: it is a mobile
 *     Safari behaviour, not a rendering-engine one. What can be tested is the condition that
 *     triggers it, so `every control is at least 16px on a phone` is asserted directly. That
 *     check failed on the code that produced the report — the video filters were 13.6px and the
 *     About form 13px, while every Material field was already 16px, which is exactly why the
 *     login page was the one page that looked right.
 *
 * Runs in the two phone projects only, and makes its own contexts at each width: the alternative
 * is five project runs of the same assertions, which costs minutes and finds nothing extra.
 */

const ADMIN = {
  email: process.env['E2E_ADMIN_EMAIL'],
  password: process.env['E2E_ADMIN_PASSWORD'],
};
const CLIENT = {
  email: process.env['E2E_CLIENT_EMAIL'],
  password: process.env['E2E_CLIENT_PASSWORD'],
};

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

/** The two iPhone widths in use, plus a tablet and a laptop so the check is not phone-only. */
const WIDTHS = [
  { name: '375', width: 375, height: 667 },
  { name: '390', width: 390, height: 844 },
  { name: '820', width: 820, height: 1180 },
  { name: '1440', width: 1440, height: 900 },
] as const;

/** Anything sticking out past the viewport, so a failure names the element instead of a number. */
async function overflowReport(
  page: Page,
): Promise<{ scrollWidth: number; clientWidth: number; worst: string[] }> {
  return page.evaluate(() => {
    const clientWidth = document.documentElement.clientWidth;
    const worst: string[] = [];
    for (const el of Array.from(document.querySelectorAll('*'))) {
      const r = el.getBoundingClientRect();
      if (r.width === 0 && r.height === 0) continue;
      if (r.right <= clientWidth + 0.5 && r.left >= -0.5) continue;
      const name =
        el.tagName.toLowerCase() +
        (el.id ? `#${el.id}` : '') +
        (typeof el.className === 'string' && el.className
          ? `.${el.className.trim().split(/\s+/).slice(0, 3).join('.')}`
          : '');
      worst.push(`${name} [${Math.round(r.left)}..${Math.round(r.right)}]`);
    }
    return {
      scrollWidth: document.documentElement.scrollWidth,
      clientWidth,
      // The deepest few are the ones worth reading; an ancestor is usually just carrying a child.
      worst: worst.slice(-6),
    };
  });
}

async function expectNoSidewaysScroll(page: Page, where: string): Promise<void> {
  const report = await overflowReport(page);
  expect(
    report.scrollWidth,
    `${where} scrolls sideways (${report.scrollWidth} > ${report.clientWidth}). Widest elements: ${
      report.worst.join(' | ') || 'none found — check a fixed or transformed element'
    }`,
  ).toBeLessThanOrEqual(report.clientWidth);
}

/** Every visible control, with the computed font-size iOS decides to zoom on. */
async function smallControls(page: Page): Promise<string[]> {
  return page.evaluate(() => {
    const small: string[] = [];
    for (const el of Array.from(document.querySelectorAll('input, select, textarea'))) {
      const input = el as HTMLInputElement;
      if (input.type === 'hidden' || input.type === 'checkbox' || input.type === 'radio') continue;
      if (!el.getClientRects().length) continue;
      const size = parseFloat(getComputedStyle(el).fontSize);
      if (size < 16) {
        small.push(
          `${el.tagName.toLowerCase()}[${input.type || ''}]` +
            `${input.name ? ` name=${input.name}` : ''} -> ${size}px`,
        );
      }
    }
    return small;
  });
}

async function expectNoZoomingControls(page: Page, where: string): Promise<void> {
  const small = await smallControls(page);
  expect(
    small,
    `${where} has controls under 16px, which makes iOS Safari zoom the page in on focus and never back out: ${small.join(
      ', ',
    )}`,
  ).toEqual([]);
}

async function settle(page: Page): Promise<void> {
  await page.waitForLoadState('domcontentloaded');
  await page
    .locator('app-shell, .auth-page, mat-card, .about, .editor-page')
    .first()
    .waitFor({ state: 'visible' });
  await page
    .locator('[role="status"]:has-text("Φόρτωση")')
    .first()
    .waitFor({ state: 'detached' })
    .catch(() => undefined);
  // Web fonts change text metrics, and a page measured mid-swap measures the fallback.
  await page.evaluate(() => document.fonts?.ready);
}

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

test.describe('no horizontal scrolling', () => {
  // The suite runs five projects; these assertions are width-driven and make their own contexts,
  // so running them once per engine covers everything the other three would. In a beforeEach
  // rather than a describe-level skip, because only the hook is handed the project.
  test.beforeEach(({}, testInfo) => {
    test.skip(
      !['phone', 'phone-webkit'].includes(testInfo.project.name),
      'runs once per engine, at its own widths',
    );
  });
  test.describe.configure({ mode: 'serial' });

  // One signed-in page per role for the whole file, resized between widths rather than signed in
  // again for each. Sign-in is rate limited on purpose, and four widths times two roles times two
  // engines is twenty attempts spent proving nothing about the layout.
  let anonymous: Page;
  let adminPage: Page;
  let clientPage: Page;

  test.beforeAll(async ({ browser }, testInfo) => {
    const open = async () => {
      const context = await browser.newContext({
        baseURL: testInfo.project.use.baseURL,
        viewport: { width: WIDTHS[0].width, height: WIDTHS[0].height },
      });
      return context.newPage();
    };

    anonymous = await open();

    if (ADMIN.email && ADMIN.password) {
      adminPage = await open();
      await signIn(adminPage, ADMIN.email, ADMIN.password);
    }
    if (CLIENT.email && CLIENT.password) {
      clientPage = await open();
      await signIn(clientPage, CLIENT.email, CLIENT.password);
    }
  });

  test.afterAll(async () => {
    for (const page of [anonymous, adminPage, clientPage]) {
      await closeQuietly(page);
    }
  });

  for (const size of WIDTHS) {
    test(`public routes fit at ${size.name}px`, async () => {
      test.setTimeout(30_000 + PUBLIC_ROUTES.length * 20_000);
      await anonymous.setViewportSize({ width: size.width, height: size.height });
      for (const route of PUBLIC_ROUTES) {
        await anonymous.goto(route);
        await settle(anonymous);
        await expectNoSidewaysScroll(anonymous, `${route} at ${size.name}px`);
        await expectNoZoomingControls(anonymous, `${route} at ${size.name}px`);
      }
    });
  }

  for (const [role, credentials, routes] of [
    ['admin', ADMIN, ADMIN_ROUTES],
    ['client', CLIENT, CLIENT_ROUTES],
  ] as const) {
    for (const size of WIDTHS) {
      test(`${role} routes fit at ${size.name}px`, async () => {
        test.skip(!credentials.email || !credentials.password, 'credentials not set');

        // One test walks every route at one width, and each load in WebKit on Windows costs
        // several seconds. The allowance is therefore sized to the work rather than left at the
        // suite default, which a nine-route pass outgrew.
        test.setTimeout(30_000 + routes.length * 20_000);

        const page = role === 'admin' ? adminPage : clientPage;
        await page.setViewportSize({ width: size.width, height: size.height });

        for (const route of routes) {
          await page.goto(route);
          await settle(page);
          await expectNoSidewaysScroll(page, `${role} ${route} at ${size.name}px`);
          await expectNoZoomingControls(page, `${role} ${route} at ${size.name}px`);
        }
      });
    }
  }

  test('an open dialog does not widen the page', async () => {
    test.skip(!ADMIN.email || !ADMIN.password, 'credentials not set');

    const page = adminPage;
    await page.setViewportSize({ width: 375, height: 667 });
    {
      // The tag manager's delete is reachable without a video in the library, so this works on
      // an empty catalogue too. Any branded dialog exercises the same overlay.
      await page.goto('/admin/videos');
      await settle(page);
      // A <summary> is not reliably exposed as a button across engines; open the disclosure itself.
      await page.locator('.tag-manager summary').click();

      const remove = page.locator('.chips button:not([disabled])').first();
      test.skip(!(await remove.count()), 'no unused tag to open a dialog with');
      await remove.click();
      await expect(page.locator('.confirm')).toBeVisible();

      await expectNoSidewaysScroll(page, 'a dialog open at 375px');
      await page.keyboard.press('Escape');
    }
  });

  test('long titles, emails and file names do not widen anything', async () => {
    test.skip(!ADMIN.email || !ADMIN.password, 'credentials not set');

    const page = adminPage;
    await page.setViewportSize({ width: 375, height: 667 });
    {
      await seedLongData(page.context());

      // The video editor, carrying a long Greek title and a long file name at once.
      await page.goto('/admin/videos/new');
      await settle(page);
      await page.fill('input[name="title"]', LONG_TITLE);
      await page.fill('textarea[name="description"]', `${LONG_WORD} ${LONG_URL}`);
      await page.setInputFiles('input[type="file"][accept*="video"]', {
        name: LONG_FILE_NAME,
        mimeType: 'video/mp4',
        buffer: Buffer.from('not a real recording, only a name to render'),
      });
      await expect(page.getByText(LONG_FILE_NAME)).toBeVisible();
      await expectNoSidewaysScroll(page, 'the editor with a long title and file name');

      // The user list, carrying a long email address.
      await page.goto('/admin/users');
      await settle(page);
      await expectNoSidewaysScroll(page, 'the user list with a long email address');

      // The About editor, carrying a long URL in every link it has. Social links are added one
      // at a time now, so this adds them the way the trainer would.
      await page.goto('/about/edit');
      await settle(page);
      await page.locator('input[name="trainerName"]').waitFor({ state: 'visible' });

      for (const network of ['instagram', 'youtube', 'website']) {
        const dropdown = page.locator('select[name="pendingNetwork"]');
        if (!(await dropdown.locator(`option[value="${network}"]`).count())) continue;
        await dropdown.selectOption(network);
        await page.getByRole('button', { name: 'Προσθήκη κοινωνικού δικτύου' }).click();
        await page.fill(`input[name="link-${network}"]`, LONG_URL);
      }

      await page.fill('textarea[name="aboutMarkdown"]', `${LONG_WORD}\n\n- ${LONG_URL}`);
      await expectNoSidewaysScroll(page, 'the About editor with long links');
    }
  });
});

const LONG_TITLE =
  'Ολοκληρωμένη προπόνηση ενδυνάμωσης για όλο το σώμα με αλτήρες και λάστιχα αντίστασης, επίπεδο προχωρημένο';
const LONG_WORD = 'Αντιπαραθεσιολογικοαναλυτικοσυμπερασματολογικός';
const LONG_URL =
  'https://www.example.com/a-very-long-profile-address/that-keeps-going/and-going/forever';
const LONG_FILE_NAME = 'προπόνηση-ολοκληρωμένη-ενδυνάμωση-όλο-το-σώμα-2026-09-28-τελική-έκδοση.mp4';

/**
 * A client whose email address is 40 characters, because a realistic address is the longest
 * unbroken string the admin list will ever be asked to lay out. Created through the app's own
 * API, and left in place: it is a development database, and the next run reuses it.
 */
async function seedLongData(context: BrowserContext): Promise<void> {
  const email = 'onomatepwnymo.makriemail.2026@example.com';
  const response = await context.request.post('/api/admin/users', {
    data: {
      email,
      firstName: 'Μακρυνόματη',
      lastName: 'Παπαδοπούλου-Κωνσταντινίδου',
      role: 'Client',
    },
    failOnStatusCode: false,
  });
  // 409 means it is already there from an earlier run, which is exactly as good.
  if (![200, 201, 204, 409].includes(response.status())) {
    test.info().annotations.push({
      type: 'note',
      description: `could not seed the long email (${response.status()}); the list check still ran`,
    });
  }
}
