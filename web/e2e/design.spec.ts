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
 * The things that are true of every screen rather than of one: the brand lockup, the height of a
 * form field, the shape of the admin user list, and the crop dialog that both picture uploads go
 * through.
 */

const ADMIN = {
  email: process.env['E2E_ADMIN_EMAIL'],
  password: process.env['E2E_ADMIN_PASSWORD'],
};
const SAMPLE_IMAGE = 'e2e/fixtures/sample-image.png';

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

function isPhone(testInfo: { project: { name: string } }): boolean {
  return testInfo.project.name.startsWith('phone');
}

/**
 * One signed-in page, shared by every test in this file.
 *
 * Sign-in is rate limited per (email, IP), which is the point of it — so a suite that signs in
 * once per test spends its budget fighting a production control instead of checking the product.
 */
let admin: Page;

test.beforeAll(async ({ browser }, testInfo) => {
  test.skip(!ADMIN.email || !ADMIN.password, 'E2E_ADMIN_EMAIL / E2E_ADMIN_PASSWORD not set');
  const context = await browser.newContext({
    baseURL: testInfo.project.use.baseURL,
    viewport: testInfo.project.use.viewport,
  });
  admin = await context.newPage();
  await signIn(admin, ADMIN.email!, ADMIN.password!);
});

test.afterAll(async () => {
  await closeQuietly(admin);
});

test.describe('the brand lockup', () => {
  test('the login page shows "powered by Tasos" under the wordmark, right-aligned', async ({
    page,
  }, testInfo) => {
    await page.goto('/login');
    await settle(page);

    await expect(page.locator('.auth-page__logo .logo__tagline')).toHaveText('powered by Tasos');

    // Measured inside toPass: text metrics change as the web font swaps in, and a single reading
    // taken mid-swap describes a layout that existed for one frame and never again.
    await expect(async () => {
      const box = await page.locator('.auth-page__logo').evaluate((el) => {
        const word = el.querySelector('.logo__word')!.getBoundingClientRect();
        const credit = el.querySelector('.logo__tagline')!.getBoundingClientRect();
        return {
          gap: credit.top - word.bottom,
          centreOffset: credit.left + credit.width / 2 - (word.left + word.width / 2),
          endOffset: credit.right - word.right,
        };
      });

      // 6px under the wordmark, as specified. One pixel for sub-pixel text metrics.
      expect(Math.abs(box.gap - 6), `gap ${box.gap}`).toBeLessThanOrEqual(1.5);

      if (isPhone(testInfo)) {
        expect(Math.abs(box.centreOffset), `centre offset ${box.centreOffset}`).toBeLessThanOrEqual(
          2,
        );
      } else {
        // Right-aligned to the end of "Ass".
        expect(Math.abs(box.endOffset), `end offset ${box.endOffset}`).toBeLessThanOrEqual(1.5);
      }
    }).toPass({ timeout: 10_000 });

    // Phones drop the chevron; wider screens keep it.
    await expect(page.locator('.auth-page__logo .logo__mark')).toBeVisible({
      visible: !isPhone(testInfo),
    });
  });

  test('it is the app font at 11px in the brand orange', async ({ page }) => {
    await page.goto('/login');
    await settle(page);

    const style = await page
      .locator('.logo__tagline')
      .first()
      .evaluate((el) => {
        const cs = getComputedStyle(el);
        const body = getComputedStyle(document.body);
        return {
          fontSize: cs.fontSize,
          fontWeight: cs.fontWeight,
          letterSpacing: cs.letterSpacing,
          color: cs.color,
          sameFamily: cs.fontFamily === body.fontFamily,
        };
      });

    expect(style.fontSize).toBe('11px');
    expect(style.fontWeight).toBe('500');
    // 0.02em of 11px.
    expect(parseFloat(style.letterSpacing)).toBeCloseTo(0.22, 1);
    expect(style.color).toBe('rgb(255, 138, 61)');
    expect(style.sameFamily).toBe(true);
  });

  test('the shell shows it too, and the frame still lines up', async ({}, testInfo) => {
    test.skip(isPhone(testInfo), 'the sidebar only exists above 600px');

    const page = admin;
    await page.goto('/dashboard');
    await settle(page);

    await expect(page.locator('.shell__sidebar .logo__tagline')).toHaveText('powered by Tasos');

    // The credit made the lockup taller; the sidebar header and the top bar must still end on
    // the same line, or the frame shows a step at the corner.
    const brand = (await page.locator('.shell__brand').boundingBox())!;
    const topbar = (await page.locator('.shell__topbar').boundingBox())!;
    expect(Math.abs(brand.y + brand.height - (topbar.y + topbar.height))).toBeLessThanOrEqual(1);
  });

  test('the phone top bar keeps the credit without growing the bar', async ({}, testInfo) => {
    test.skip(!isPhone(testInfo), 'phone only');

    const page = admin;
    await page.goto('/dashboard');
    await settle(page);

    await expect(page.locator('.shell__topbar .logo__tagline')).toBeVisible();
    const bar = (await page.locator('.shell__topbar').boundingBox())!;
    // The bar is 60px on a phone; the lockup has to live inside it.
    expect(bar.height).toBeLessThanOrEqual(61);
  });
});

test.describe('form fields', () => {
  test.skip(!ADMIN.email || !ADMIN.password, 'E2E_ADMIN_EMAIL / E2E_ADMIN_PASSWORD not set');

  test('every single-line field on a row is the same height', async ({}, testInfo) => {
    const page = admin;

    // The About editor is where this was reported: Phone and Instagram were taller than their
    // neighbours because each carried a hint underneath.
    await page.goto('/about/edit');
    await settle(page);
    // settle() only knows the loading state went away; the fields arrive with the next render.
    await page.locator('input[name="trainerName"]').waitFor({ state: 'visible' });

    const heights = await page
      .locator('.about-editor input:not([type="file"])')
      .evaluateAll((nodes) =>
        nodes
          .filter((n) => (n as HTMLElement).offsetParent !== null)
          .map((n) => Math.round(n.getBoundingClientRect().height)),
      );

    expect(heights.length).toBeGreaterThan(1);
    expect(new Set(heights).size, `field heights differ: ${heights.join(', ')}`).toBe(1);
    expect(heights[0]).toBe(44);
  });
});

test.describe('the admin user list', () => {
  test.skip(!ADMIN.email || !ADMIN.password, 'E2E_ADMIN_EMAIL / E2E_ADMIN_PASSWORD not set');

  test('is a scannable row with badges and a menu', async ({}, testInfo) => {
    const page = admin;
    await page.goto('/admin/users');
    await settle(page);

    const row = page.locator('.urow').first();
    await expect(row).toBeVisible();
    await expect(row.locator('.urow__avatar')).toBeVisible();
    await expect(row.locator('.urow__email')).toBeVisible();
    // A role badge and a status badge, not a paragraph of text.
    await expect(row.locator('.ubadge')).toHaveCount(2);

    // The column header is a wide-screen affordance. Below 900px — which includes a tablet in
    // portrait — each row becomes its own card and the header would be labelling nothing.
    const wide = (testInfo.project.use.viewport?.width ?? 0) >= 900;
    await expect(page.locator('.urows__head')).toBeVisible({ visible: wide });

    const more = row.locator('.urow__more');
    await expect(more).toHaveAttribute('aria-label', /Ενέργειες για /);
    await more.click();
    await expect(page.getByRole('menuitem', { name: 'Επεξεργασία' })).toBeVisible();
    await page.keyboard.press('Escape');
  });
});

test.describe('the crop dialog', () => {
  test.skip(!ADMIN.email || !ADMIN.password, 'E2E_ADMIN_EMAIL / E2E_ADMIN_PASSWORD not set');
  test.describe.configure({ mode: 'serial' });

  test('a video cover is framed 16:9 before it is uploaded', async ({}, testInfo) => {
    const page = admin;
    await page.goto('/admin/videos/new');
    await settle(page);

    await page.setInputFiles('input[type="file"][accept*="image"]', SAMPLE_IMAGE);

    const dialog = page.locator('.crop');
    await expect(dialog).toBeVisible();
    await expect(dialog).toContainText('Εξώφυλλο βίντεο');
    // A rectangular frame, not the circular one.
    await expect(page.locator('.crop__stage--round')).toHaveCount(0);

    // The preview only appears once the cropper has produced bytes, so it is the proof that
    // cropping actually happened rather than the dialog merely opening.
    await expect(page.locator('.crop__preview img')).toBeVisible({ timeout: 15_000 });

    await page.getByRole('button', { name: 'Χρήση εικόνας' }).click();
    await expect(dialog).toHaveCount(0);

    // Back on the editor, held as the pending cover for a video that does not exist yet.
    await expect(page.locator('.editor-cover__preview img')).toBeVisible();
    await expect(
      page.getByText('Δική σου εικόνα — θα ανέβει μόλις ολοκληρωθεί το βίντεο'),
    ).toBeVisible();
  });

  test('the trainer photo is framed as a circle and uploaded on confirm', async ({}, testInfo) => {
    const page = admin;
    await page.goto('/about/edit');
    await settle(page);

    await page.setInputFiles('.about-editor__filebutton input[type="file"]', SAMPLE_IMAGE);

    const dialog = page.locator('.crop');
    await expect(dialog).toBeVisible();
    await expect(dialog).toContainText('Η φωτογραφία σου');
    // Circular frame, and a circular preview to match what the page renders.
    await expect(page.locator('.crop__stage--round')).toBeVisible();
    await expect(page.locator('.crop__preview-frame--round')).toBeVisible();
    await expect(page.locator('.crop__preview img')).toBeVisible({ timeout: 15_000 });

    // Cancelling leaves the page exactly as it was: nothing is uploaded on the way out.
    // Scoped to the dialog — the editor behind it has a Cancel of its own, and the backdrop
    // makes clicking that one hang rather than fail.
    await dialog.getByRole('button', { name: 'Ακύρωση' }).click();
    await expect(dialog).toHaveCount(0);
  });

  test('a file that is not an image never opens the dialog', async ({}, testInfo) => {
    const page = admin;
    await page.goto('/about/edit');
    await settle(page);

    await page.setInputFiles('.about-editor__filebutton input[type="file"]', {
      name: 'notes.txt',
      mimeType: 'text/plain',
      buffer: Buffer.from('this is not a photograph'),
    });

    await expect(page.locator('.crop')).toHaveCount(0);
    await expect(page.getByText('Δεκτές εικόνες: JPG, PNG ή WebP.')).toBeVisible();
  });
});

test.describe('social links on the About editor', () => {
  test.skip(!ADMIN.email || !ADMIN.password, 'E2E_ADMIN_EMAIL / E2E_ADMIN_PASSWORD not set');

  test('a network is added from a dropdown, and cannot be added twice', async ({}, testInfo) => {
    const page = admin;
    await page.goto('/about/edit');
    await settle(page);

    const before = await page.locator('.about-editor__link').count();

    await page.selectOption('select[name="pendingNetwork"]', 'instagram');
    await page.getByRole('button', { name: 'Προσθήκη κοινωνικού δικτύου' }).click();
    await expect(page.locator('.about-editor__link')).toHaveCount(before + 1);

    // Instagram is now gone from the dropdown, so there is no way to add a second one.
    const remaining = await page.locator('select[name="pendingNetwork"] option').allInnerTexts();
    expect(remaining).not.toContain('Instagram');

    // The row carries its own remove and reorder controls, each naming its network.
    const row = page.locator('.about-editor__link').last();
    await expect(row.locator('button[aria-label^="Αφαίρεση"]')).toBeVisible();
    await expect(row.locator('button[aria-label^="Μετακίνηση πάνω"]')).toBeVisible();

    await row.locator('button[aria-label^="Αφαίρεση"]').click();
    await expect(page.locator('.about-editor__link')).toHaveCount(before);
  });
});
