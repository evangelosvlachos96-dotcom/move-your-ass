import { Page, expect, test } from '@playwright/test';

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
 * The trainer's editing surfaces, after the redesign:
 *
 *  - the video list is cards with one primary action and a "⋮" menu, not a row of ten buttons;
 *  - editing is a page of its own, reachable from the list, from the client-facing library and
 *    from the player, and it comes back to wherever it was opened from;
 *  - a client sees none of it, and the routes refuse them.
 *
 * Runs once per engine at phone size and once at desktop, because "the menu instead of rows of
 * buttons" is a phone requirement and "returns where it came from" is not size-dependent.
 */

const ADMIN = {
  email: process.env['E2E_ADMIN_EMAIL'],
  password: process.env['E2E_ADMIN_PASSWORD'],
};
const CLIENT = {
  email: process.env['E2E_CLIENT_EMAIL'],
  password: process.env['E2E_CLIENT_PASSWORD'],
};

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
}

test.describe('the admin video list and editor', () => {
  test.skip(!ADMIN.email || !ADMIN.password, 'E2E_ADMIN_EMAIL / E2E_ADMIN_PASSWORD not set');
  test.describe.configure({ mode: 'serial' });

  let page: Page;

  test.beforeAll(async ({ browser }, testInfo) => {
    const context = await browser.newContext({
      baseURL: testInfo.project.use.baseURL,
      viewport: testInfo.project.use.viewport,
    });
    page = await context.newPage();
    await signIn(page, ADMIN.email!, ADMIN.password!);
  });

  test.afterAll(async () => {
    await closeQuietly(page);
  });

  test('"Νέο βίντεο" opens the editor as a page of its own', async () => {
    await page.goto('/admin/videos');
    await settle(page);

    await page.getByRole('link', { name: 'Νέο βίντεο' }).click();
    await expect(page).toHaveURL(/\/admin\/videos\/new/);

    // The four groups the trainer was promised, each an actual heading.
    for (const section of ['Βασικά στοιχεία', 'Κατηγοριοποίηση', 'Εξώφυλλο', 'Αρχείο βίντεο']) {
      await expect(page.getByRole('heading', { name: section })).toBeVisible();
    }

    // One Save, one Cancel — not a scattering of buttons.
    await expect(page.getByRole('button', { name: 'Ανέβασμα βίντεο' })).toHaveCount(1);
    await expect(page.getByRole('button', { name: 'Ακύρωση' })).toHaveCount(1);
  });

  test('the cover section is offered while creating, before the video exists', async () => {
    await page.goto('/admin/videos/new');
    await settle(page);

    // Both the upload control and a statement of which of the three covers is in play.
    await expect(page.getByText('Ανέβασμα εικόνας')).toBeVisible();
    await expect(page.getByText('Χωρίς εικόνα — θα εμφανίζεται το λογότυπο')).toBeVisible();

    await page.setInputFiles(
      'input[type="file"][accept*="image"]',
      'e2e/fixtures/sample-image.png',
    );

    // The picture is framed first; the crop dialog is covered in detail in design.spec.ts.
    await expect(page.locator('.crop')).toBeVisible();
    await expect(page.locator('.crop__preview img')).toBeVisible({ timeout: 15_000 });
    await page.getByRole('button', { name: 'Χρήση εικόνας' }).click();

    // Chosen, previewed, and explicitly described as not sent yet.
    await expect(page.locator('.editor-cover__preview img')).toBeVisible();
    await expect(
      page.getByText('Δική σου εικόνα — θα ανέβει μόλις ολοκληρωθεί το βίντεο'),
    ).toBeVisible();
  });

  test('the upload wording says what to do, not how the plumbing works', async () => {
    await page.goto('/admin/videos/new');
    await settle(page);

    await expect(
      page.getByText('Κράτα τη σελίδα ανοιχτή μέχρι να ολοκληρωθεί το ανέβασμα.'),
    ).toBeVisible();
    // The sentence that confused the trainer: she is using the site, so "not through the site"
    // reads as a contradiction.
    await expect(page.getByText('όχι μέσω του site')).toHaveCount(0);
  });

  test('leaving the editor with unsaved changes asks first, in the branded dialog', async () => {
    await page.goto('/admin/videos/new');
    await settle(page);

    await page.fill('input[name="title"]', 'Κάτι που δεν θα αποθηκευτεί');
    await page.getByRole('button', { name: 'Πίσω' }).click();

    const dialog = page.locator('.confirm');
    await expect(dialog).toBeVisible();
    await expect(dialog).toContainText('Μη αποθηκευμένες αλλαγές');

    // Staying means staying.
    await page.locator('.confirm__button--cancel').click();
    await expect(page).toHaveURL(/\/admin\/videos\/new/);
  });

  test('each card carries one primary action and a labelled menu', async () => {
    await page.goto('/admin/videos');
    await settle(page);

    const cards = page.locator('.vcard');
    test.skip(!(await cards.count()), 'no video in the library');

    // The card is a visible block, not a run of text: it has its own border.
    const border = await cards.first().evaluate((el) => getComputedStyle(el).borderTopWidth);
    expect(border).not.toBe('0px');

    const more = cards.first().locator('.vcard__more');
    // Icon-only, so its accessible name has to name the video it acts on.
    await expect(more).toHaveAttribute('aria-label', /Ενέργειες για /);
    await more.click();

    // Every item is an icon *and* a label.
    for (const item of ['Επεξεργασία', 'Μετακίνηση πάνω', 'Μετακίνηση κάτω', 'Διαγραφή']) {
      await expect(page.getByRole('menuitem', { name: item })).toBeVisible();
    }
    await page.keyboard.press('Escape');
  });

  test('the menu opens that video in the editor', async () => {
    await page.goto('/admin/videos');
    await settle(page);

    const cards = page.locator('.vcard');
    test.skip(!(await cards.count()), 'no video in the library');

    const title = (await cards.first().locator('.vcard__title').innerText()).trim();
    await cards.first().locator('.vcard__more').click();
    await page.getByRole('menuitem', { name: 'Επεξεργασία' }).click();

    await expect(page).toHaveURL(/\/admin\/videos\/[^/]+\/edit/);
    await expect(page.locator('input[name="title"]')).toHaveValue(title);
  });
});

test.describe('editing from the client-facing pages', () => {
  test.skip(!ADMIN.email || !ADMIN.password, 'E2E_ADMIN_EMAIL / E2E_ADMIN_PASSWORD not set');
  test.describe.configure({ mode: 'serial' });

  let page: Page;

  test.beforeAll(async ({ browser }, testInfo) => {
    const context = await browser.newContext({
      baseURL: testInfo.project.use.baseURL,
      viewport: testInfo.project.use.viewport,
    });
    page = await context.newPage();
    await signIn(page, ADMIN.email!, ADMIN.password!);
  });

  test.afterAll(async () => {
    await closeQuietly(page);
  });

  test('an admin gets a pencil on each library card, and it comes back here', async () => {
    await page.goto('/videos');
    await settle(page);

    const pencil = page.locator('.video-card__edit').first();
    test.skip(!(await pencil.count()), 'no published video in the library');

    await expect(pencil).toHaveAttribute('aria-label', /^Επεξεργασία: /);
    await pencil.click();
    await expect(page).toHaveURL(/\/admin\/videos\/[^/]+\/edit\?returnUrl=%2Fvideos/);

    // Cancel returns to where the pencil was pressed, not to the management list.
    await page.getByRole('button', { name: 'Ακύρωση' }).click();
    await expect(page).toHaveURL(/\/videos$/);
  });

  test('an admin gets an edit button on the player, and it comes back here', async () => {
    await page.goto('/videos');
    await settle(page);

    const card = page.locator('.video-card__link').first();
    test.skip(!(await card.count()), 'no published video in the library');
    await card.click();
    await expect(page).toHaveURL(/\/videos\/[^/]+$/);
    const watching = new URL(page.url()).pathname;

    await page.getByRole('link', { name: 'Επεξεργασία' }).click();
    await expect(page).toHaveURL(/\/admin\/videos\/[^/]+\/edit/);

    await page.getByRole('button', { name: 'Πίσω' }).click();
    await expect(page).toHaveURL(new RegExp(`${watching}$`));
  });
});

test.describe('a client sees none of the editing', () => {
  test.skip(!CLIENT.email || !CLIENT.password, 'E2E_CLIENT_EMAIL / E2E_CLIENT_PASSWORD not set');

  test('no pencil, no edit button, and the editor routes refuse them', async ({
    browser,
  }, testInfo) => {
    const context = await browser.newContext({
      baseURL: testInfo.project.use.baseURL,
      viewport: testInfo.project.use.viewport,
    });
    try {
      const page = await context.newPage();
      await signIn(page, CLIENT.email!, CLIENT.password!);

      await page.goto('/videos');
      await settle(page);
      await expect(page.locator('.video-card__edit')).toHaveCount(0);

      const card = page.locator('.video-card__link').first();
      if (await card.count()) {
        await card.click();
        await expect(page).toHaveURL(/\/videos\/[^/]+$/);
        await expect(page.getByRole('link', { name: 'Επεξεργασία' })).toHaveCount(0);
      }

      // The route guard, not just the absence of a button.
      await page.goto('/admin/videos/new');
      await expect(page).not.toHaveURL(/\/admin\/videos\/new/);
      await page.goto('/about/edit');
      await expect(page).not.toHaveURL(/\/about\/edit/);

      // And the server refuses regardless of what the browser was shown.
      const refused = await context.request.get('/api/admin/videos/summary', {
        failOnStatusCode: false,
      });
      expect([401, 403]).toContain(refused.status());
    } finally {
      await context.close();
    }
  });
});

test.describe('the About editor', () => {
  test.skip(!ADMIN.email || !ADMIN.password, 'E2E_ADMIN_EMAIL / E2E_ADMIN_PASSWORD not set');

  test("is a form page with the app's own fields and one pair of actions", async ({
    browser,
  }, testInfo) => {
    const context = await browser.newContext({
      baseURL: testInfo.project.use.baseURL,
      viewport: testInfo.project.use.viewport,
    });
    try {
      const page = await context.newPage();
      await signIn(page, ADMIN.email!, ADMIN.password!);

      await page.goto('/about/edit');
      await settle(page);

      for (const section of [
        'Φωτογραφία',
        'Βασικά στοιχεία',
        'Σχετικά με εμένα',
        'Στοιχεία επικοινωνίας',
      ]) {
        await expect(page.getByRole('heading', { name: section })).toBeVisible();
      }

      await expect(page.getByRole('button', { name: 'Αποθήκευση' })).toHaveCount(1);
      await expect(page.getByRole('button', { name: 'Ακύρωση' })).toHaveCount(1);

      // The biography preview renders through the same whitelist the page uses.
      await page.fill('textarea[name="aboutMarkdown"]', '**Έντονα** και μια γραμμή.');
      await expect(page.locator('.about-editor__preview strong')).toHaveText('Έντονα');

      // Leaving with changes asks, in the app's dialog.
      await page.getByRole('button', { name: 'Πίσω' }).click();
      await expect(page.locator('.confirm')).toContainText('Μη αποθηκευμένες αλλαγές');
      await page.locator('.confirm__button--confirm').click();
      await expect(page).toHaveURL(/\/about$/);
    } finally {
      await context.close();
    }
  });
});
