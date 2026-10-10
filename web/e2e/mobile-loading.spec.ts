import { expect, Page, test } from '@playwright/test';

// All API responses are local fixtures. This suite needs neither production access nor credentials.
const user = {
  id: 'ui-admin',
  firstName: 'Test',
  lastName: 'Administrator',
  email: 'admin@example.test',
  role: 'Admin',
  status: 'Active',
  mustChangePassword: false,
};
const users = Array.from({ length: 12 }, (_, i) => ({
  ...user,
  id: 'user-' + i,
  firstName: 'Δοκιμαστικός',
  lastName: 'Χρήστης ' + i,
  email: 'long.email.for.layout.' + i + '@example.test',
  role: i === 0 ? 'Admin' : 'Client',
  status: i === 1 ? 'PendingApproval' : 'Active',
  createdAtUtc: '2026-10-01T12:00:00Z',
}));
const video = {
  id: '11111111-1111-1111-1111-111111111111',
  title: 'Δοκιμαστική προπόνηση',
  description: 'Περιγραφή',
  audience: 'Both',
  bodyArea: 'FullBody',
  requiresEquipment: false,
  status: 'Ready',
  isPublished: true,
  sortOrder: 0,
  durationSeconds: 60,
  thumbnailUrl: null,
  tags: [],
  revision: 'revision',
  createdAtUtc: '2026-10-01T12:00:00Z',
};
function gate() {
  let release!: () => void;
  const promise = new Promise<void>((resolve) => (release = resolve));
  return { promise, release };
}
async function mock(
  page: Page,
  hold?: { matches: (path: string) => boolean; promise: Promise<void> },
) {
  // Playback bytes are outside this UI suite; keep the synthetic media request pending.
  await page.route('https://example.test/fixture.mp4', () => {});
  await page.route('**/api/**', async (route) => {
    const path = new URL(route.request().url()).pathname;
    if (hold?.matches(path)) await hold.promise;
    let json: unknown;
    if (path.endsWith('/auth/refresh')) json = { accessToken: 'local-ui-fixture', expiresIn: 900 };
    else if (path.endsWith('/auth/me')) json = user;
    else if (path.endsWith('/admin/users'))
      json = { items: users, totalCount: users.length, page: 1, pageSize: 20, totalPages: 1 };
    else if (path.endsWith('/site/about'))
      json = {
        trainerName: 'Test trainer',
        tagline: 'Training',
        aboutMarkdown: 'About',
        photoUrl: null,
        contactEmail: null,
        phone: null,
        bookingUrl: null,
        socialLinks: [],
        revision: 'revision',
      };
    else if (path.endsWith('/summary'))
      json = {
        total: 1,
        published: 1,
        processing: 0,
        failed: 0,
        providerConfigured: true,
        storage: { usedBytes: 0, capBytes: 1000 },
      };
    else if (path.endsWith('/filters') || path.endsWith('/tags')) json = [];
    else if (path.endsWith('/playback'))
      json = { url: 'https://example.test/fixture.mp4', expires: 9999999999 };
    else if (path.endsWith(video.id)) json = video;
    else if (path.endsWith('/videos'))
      json = { items: [video], totalCount: 1, page: 1, pageSize: 12, totalPages: 1 };
    else throw new Error('Unexpected API request ' + path);
    await route.fulfill({ json });
  });
}

test('boot is fixed and cannot scroll while session restoration waits', async ({ page }) => {
  const wait = gate();
  await mock(page, { matches: (p) => p.endsWith('/auth/refresh'), promise: wait.promise });
  await page.goto('/admin/users');
  await expect(page.locator('.boot')).toBeVisible();
  await page.evaluate(() => window.scrollTo(0, 500));
  expect(await page.evaluate(() => window.scrollY)).toBe(0);
  expect(await page.locator('body').evaluate((e) => getComputedStyle(e).position)).toBe('fixed');
  expect(await page.locator('.boot').evaluate((e) => getComputedStyle(e).position)).toBe('fixed');
  wait.release();
  await expect(page.locator('.urow')).toHaveCount(12);
  await expect(page.locator('body')).not.toHaveClass(/app-booting/);
  await page.locator('.urow').last().scrollIntoViewIfNeeded();
  expect(await page.evaluate(() => window.scrollY)).toBeGreaterThan(0);
});

test('users skeleton resolves to complete reachable cards and route changes start at the top', async ({
  page,
}) => {
  const wait = gate();
  await mock(page, { matches: (p) => p.endsWith('/admin/users'), promise: wait.promise });
  await page.goto('/admin/users');
  await expect(page.locator('app-users app-skeleton')).toBeVisible();
  await expect(page.locator('.urow')).toHaveCount(0);
  wait.release();
  await expect(page.locator('.urow')).toHaveCount(12);
  await expect(page.locator('app-users app-skeleton')).toHaveCount(0);
  if ((page.viewportSize()?.width ?? 0) < 900) {
    const row = page.locator('.urow').first();
    const identity = await row.locator('.urow__identity').boundingBox();
    const menu = await row.locator('.urow__actions').boundingBox();
    expect(Math.abs(identity!.y - menu!.y)).toBeLessThan(2);
    const badges = await row.locator('.urow__badges').boundingBox();
    expect(badges!.y).toBeGreaterThan(identity!.y);
  }
  // A DOM count alone misses off-screen content clipped by a viewport-height body.
  for (const row of await page.locator('.urow').all()) {
    await row.evaluate((e) => e.scrollIntoView({ block: 'center' }));
    await expect(row).toBeInViewport();
    expect(
      await row.evaluate((e) => {
        const r = e.getBoundingClientRect();
        const y = Math.max(80, Math.min(window.innerHeight - 100, r.y + r.height / 2));
        return e.contains(document.elementFromPoint(r.x + r.width / 2, y));
      }),
    ).toBe(true);
  }
  expect(
    await page.locator('body').evaluate((e) => e.getBoundingClientRect().height),
  ).toBeGreaterThan(page.viewportSize()!.height);
  const nav = page.locator('nav').getByRole('link', { name: 'Προπονήσεις', exact: true });
  await nav.click();
  await expect(page.locator('.video-card')).toHaveCount(1);
  await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(0);
  await page
    .locator('nav')
    .getByRole('link', { name: /Χρήστες/ })
    .click();
  await expect(
    page.getByRole('main').getByRole('heading', { name: 'Χρήστες', exact: true }),
  ).toBeInViewport();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= document.documentElement.clientWidth,
    ),
  ).toBe(true);
});

for (const path of [
  '/dashboard',
  '/videos',
  '/admin/videos',
  '/about',
  '/about/edit',
  '/admin/videos/' + video.id + '/edit',
  '/videos/' + video.id,
]) {
  test('content-shaped loading: ' + path, async ({ page }) => {
    const wait = gate();
    await mock(page, { matches: (p) => !p.includes('/auth/'), promise: wait.promise });
    await page.goto(path);
    await expect(page.locator('main app-skeleton').first()).toBeVisible();
    await expect(page.locator('main app-skeleton [aria-busy="true"]').first()).toBeVisible();
    await expect(
      page.getByText('Το ανέβασμα βίντεο είναι απενεργοποιημένο.', { exact: true }),
    ).toHaveCount(0);
    wait.release();
    await expect(page.locator('main app-skeleton')).toHaveCount(0);
  });
}

test('filter values fit, play icon is vector, and player back link is a control', async ({
  page,
}) => {
  await mock(page);
  await page.goto('/videos');
  await expect(page.locator('.video-card')).toHaveCount(1);
  await expect(page.locator('.play-mark svg')).toBeVisible();
  const equipment = page.getByRole('combobox', { name: 'Εξοπλισμός', exact: true });
  await equipment.click();
  await expect(page.getByRole('listbox')).toBeVisible();
  await page.getByRole('option', { name: 'Χωρίς εξοπλισμό', exact: true }).click();
  await expect(equipment).toContainText('Χωρίς εξοπλισμό');
  await expect(page).toHaveURL(/equipment=false/);
  await equipment.focus();
  await page.keyboard.press('Alt+ArrowDown');
  await expect(page.getByRole('listbox')).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page.getByRole('listbox')).toHaveCount(0);
  await page.locator('.video-card__link').click();
  await expect(page.locator('.back-link')).toBeVisible();
  await expect(page.locator('.back-link mat-icon')).toHaveText('arrow_back');
  await page.locator('.back-link').click();
  await expect(page).toHaveURL(/\/videos$/);
});

test('workouts and trainer share the shell top inset', async ({ page }, testInfo) => {
  await mock(page);
  await page.goto('/videos');
  await expect(page.locator('.video-card')).toHaveCount(1);
  const header = await page.locator('.shell__topbar').boundingBox();
  const heading = await page.locator('.video-heading').boundingBox();
  const gap = heading!.y - header!.y - header!.height;
  expect(gap).toBe((page.viewportSize()?.width ?? 0) < 600 ? 16 : 24);
  await expect(page.getByRole('link', { name: 'Evangelos Vlachos' })).toHaveAttribute(
    'href',
    'https://www.linkedin.com/in/evanvlac/',
  );
  await page.getByRole('combobox', { name: 'Περιοχή σώματος', exact: true }).click();
  await expect(page.getByRole('listbox')).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath('dropdown.png') });
  await page.keyboard.press('Escape');
  await page.goto('/about');
  await expect(page.locator('.about-hero')).toBeVisible();
  const trainer = await page.locator('.about-hero').boundingBox();
  const bar = await page.locator('.shell__topbar').boundingBox();
  expect(trainer!.y - bar!.y - bar!.height).toBe(gap);
});

test('saved thumbnail is shown in full in library, admin list and editor', async ({ page }) => {
  await mock(page);
  const artwork =
    'data:image/svg+xml,' +
    encodeURIComponent(
      '<svg xmlns="http://www.w3.org/2000/svg" width="1280" height="720"><rect width="1280" height="720" fill="black"/><text x="0" y="360" fill="white">LEFT EDGE</text><text x="1280" y="360" text-anchor="end" fill="white">RIGHT EDGE</text></svg>',
    );
  // Playback bytes are outside this UI suite; keep the synthetic media request pending.
  await page.route('https://example.test/fixture.mp4', () => {});
  await page.route('**/api/**', async (route) => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith('/videos'))
      await route.fulfill({
        json: {
          items: [{ ...video, thumbnailUrl: artwork }],
          totalCount: 1,
          page: 1,
          pageSize: 12,
          totalPages: 1,
        },
      });
    else if (path.endsWith(video.id))
      await route.fulfill({ json: { ...video, thumbnailUrl: artwork } });
    else await route.fallback();
  });
  for (const [url, frame] of [
    ['/videos', '.thumbnail'],
    ['/admin/videos', '.vcard__cover'],
    ['/admin/videos/' + video.id + '/edit', '.editor-cover__preview'],
  ]) {
    await page.goto(url);
    const container = page.locator(frame).first();
    const picture = container.locator('img');
    await expect(picture).toBeVisible();
    await expect.poll(() => picture.evaluate((e: HTMLImageElement) => e.naturalWidth)).toBe(1280);
    expect(await container.evaluate((e) => getComputedStyle(e).aspectRatio)).toBe('16 / 9');
    expect(await picture.evaluate((e) => getComputedStyle(e).objectFit)).toBe('contain');
    expect(await picture.evaluate((e) => getComputedStyle(e).display)).toBe('block');
  }
});

test('contact arrows stay alongside their text and pagination clears the last user', async ({
  page,
}) => {
  await mock(page);
  await page.route('**/api/site/about', (route) =>
    route.fulfill({
      json: {
        trainerName: 'Test trainer',
        tagline: 'Training',
        aboutMarkdown: 'About',
        photoUrl: null,
        contactEmail: 'trainer@example.test',
        phone: null,
        bookingUrl: null,
        socialLinks: [{ network: 'instagram', value: 'https://www.instagram.com/example/' }],
        revision: 'revision',
      },
    }),
  );
  await page.goto('/about');
  await expect(page.locator('.about__link').first()).toBeVisible();
  for (const link of await page.locator('.about__link').all()) {
    const arrow = await link.locator('.about__link-go').boundingBox();
    const label = await link.locator('.about__link-label').boundingBox();
    const value = await link.locator('.about__link-value').boundingBox();
    expect(arrow!.x).toBeGreaterThan(label!.x + label!.width);
    expect(arrow!.y).toBeLessThan(value!.y + value!.height);
    expect(arrow!.y + arrow!.height).toBeGreaterThan(label!.y);
  }
  await page.goto('/admin/users');
  await expect(page.locator('.urow')).toHaveCount(12);
  const last = await page.locator('.urow').last().boundingBox();
  const paginator = await page.locator('mat-paginator').boundingBox();
  expect(paginator!.y - last!.y - last!.height).toBeGreaterThanOrEqual(24);
});

test('October: invitation validation and compact mobile user metadata', async ({ page }) => {
  await mock(page);
  await page.goto('/admin/users');
  await expect(page.locator('.urow')).toHaveCount(12);
  if (page.viewportSize()!.width < 900) {
    const row = page.locator('.urow').first();
    const badges = await row.locator('.urow__badges').boundingBox();
    const date = await row.locator('.urow__cell--date').boundingBox();
    expect(date!.x).toBeGreaterThanOrEqual(badges!.x + badges!.width);
    expect(date!.y).toBeLessThan(badges!.y + badges!.height);
  }
  await page.getByRole('button', { name: 'Πρόσκληση χρήστη', exact: true }).click();
  const dialog = page.getByRole('dialog');
  const submit = dialog.getByRole('button', { name: 'Πρόσκληση νέου χρήστη', exact: true });
  await expect(submit).toBeDisabled();
  await dialog.getByLabel('Όνομα', { exact: true }).fill('Test');
  await dialog.getByLabel('Επώνυμο', { exact: true }).fill('Person');
  await dialog.getByLabel('Email', { exact: true }).fill('test@localhost');
  await expect(submit).toBeDisabled();
  await dialog.getByLabel('Email', { exact: true }).fill('test@example.test');
  await expect(submit).toBeEnabled();
  const label = dialog.locator('mat-label').filter({ hasText: /^Όνομα$/ });
  const labelBox = await label.boundingBox();
  const content = await dialog.locator('mat-dialog-content').boundingBox();
  expect(labelBox!.y).toBeGreaterThanOrEqual(content!.y);
});

test('October: empty user search has an empty state without pagination', async ({ page }) => {
  await mock(page);
  await page.route('**/api/admin/users*', (route) =>
    route.fulfill({ json: { items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 } }),
  );
  await page.goto('/admin/users');
  await expect(page.getByRole('heading', { name: 'Δεν βρέθηκαν χρήστες' })).toBeVisible();
  await expect(page.locator('mat-paginator')).toHaveCount(0);
});

test('October: login and reset request reject undotted email domains', async ({ page }) => {
  await page.route('**/api/auth/refresh', (route) =>
    route.fulfill({ status: 401, json: { code: 'UNAUTHENTICATED' } }),
  );
  await page.goto('/login');
  await page.getByLabel('Email', { exact: true }).fill('test@localhost');
  await page.getByRole('button', { name: 'Σύνδεση', exact: true }).click();
  await expect(page.getByText('Μη έγκυρο email', { exact: true })).toBeVisible();
  await page.goto('/forgot-password');
  const submit = page.getByRole('button', { name: 'Στείλε μου σύνδεσμο', exact: true });
  await expect(submit).toBeDisabled();
  await page.getByLabel('Email', { exact: true }).fill('test@localhost');
  await expect(submit).toBeDisabled();
  await page.getByLabel('Email', { exact: true }).fill('test@example.test');
  await expect(submit).toBeEnabled();
  await expect(page.getByRole('link', { name: 'Επιστροφή στη σύνδεση' })).toHaveClass(
    /mat-mdc-outlined-button/,
  );
});

test('October: repeated password has a clear inline error and backend code message', async ({
  page,
}) => {
  await mock(page);
  await page.goto('/change-password');
  await page.locator('[formControlName="currentPassword"]').fill('ExamplePassword42');
  await page.locator('[formControlName="newPassword"]').fill('ExamplePassword42');
  await page.locator('[formControlName="confirmPassword"]').fill('ExamplePassword42');
  await page.locator('button[type="submit"]').click();
  await expect(
    page.getByText('Ο νέος κωδικός πρέπει να διαφέρει από τον τρέχοντα.', { exact: true }),
  ).toBeVisible();
  await page.locator('[formControlName="currentPassword"]').fill('DifferentCurrent42');
  await expect(page.locator('mat-error')).toHaveCount(0);
  await page.route('**/api/auth/change-password', (route) =>
    route.fulfill({
      status: 400,
      json: { code: 'VALIDATION_FAILED', fieldCodes: { newPassword: ['PASSWORD_UNCHANGED'] } },
    }),
  );
  await page.locator('[formControlName="newPassword"]').fill('DifferentPassword42');
  await page.locator('[formControlName="confirmPassword"]').fill('DifferentPassword42');
  await page.locator('button[type="submit"]').click();
  await expect(page.locator('mat-snack-bar-container')).toContainText(
    'Ο νέος κωδικός πρέπει να διαφέρει',
  );
});

test('October: contact send is disabled until trimmed required content is valid', async ({
  page,
}) => {
  await mock(page);
  await page.route('**/api/auth/me', (route) =>
    route.fulfill({ json: { ...user, role: 'Client' } }),
  );
  await page.goto('/about');
  const send = page.getByRole('button', { name: 'Αποστολή', exact: true });
  await expect(send).toBeDisabled();
  await page.locator('[name="subject"]').fill('   ');
  await page.locator('[name="message"]').fill('            ');
  await expect(send).toBeDisabled();
  await page.locator('[name="subject"]').fill('Training');
  await page.locator('[name="message"]').fill('Please tell me more about training.');
  await expect(send).toBeEnabled();
});

test('October: video filters share height and descriptions display below playback', async ({
  page,
}) => {
  await mock(page);
  await page.goto('/videos');
  const search = await page.locator('.filters input').boundingBox();
  const audience = await page
    .getByRole('combobox', { name: 'Για ποιον', exact: true })
    .boundingBox();
  expect(Math.abs(search!.height - audience!.height)).toBeLessThanOrEqual(1);
  await page.goto('/videos/' + video.id);
  await expect(page.locator('.video-description')).toContainText(video.description);
});

test('October: editor actions remain in flow and duplicate tags give feedback', async ({
  page,
}) => {
  await mock(page);
  await page.route('**/api/**/tags*', (route) =>
    route.fulfill({ json: [{ id: 'tag-one', name: 'Pilates' }] }),
  );
  await page.goto('/admin/videos/' + video.id + '/edit');
  await expect(page.locator('.editor-actions')).toBeVisible();
  expect(await page.locator('.editor-actions').evaluate((e) => getComputedStyle(e).position)).toBe(
    'static',
  );
  const chip = page.locator('.editor-chip').first();
  expect(['flex', 'inline-flex']).toContain(
    await chip.evaluate((e) => getComputedStyle(e).display),
  );
  expect(await chip.locator('input').evaluate((e) => getComputedStyle(e).alignSelf)).toBe('center');
  await page.locator('[name="newTag"]').fill(' pilates ');
  await page.getByRole('button', { name: 'Προσθήκη ετικέτας', exact: true }).click();
  await expect(page.locator('.editor-card--note')).toContainText('Η ετικέτα υπάρχει ήδη');
  await expect(chip.locator('input')).toBeChecked();
  await expect(page.locator('textarea[name="description"]')).toHaveValue(video.description);
  await page.route('**/api/admin/videos/' + video.id, async (route) => {
    if (route.request().method() === 'PUT')
      await route.fulfill({ json: { success: true, traceId: 'ui-test' } });
    else await route.fallback();
  });
  await page.locator('textarea[name="description"]').fill('Updated workout description');
  const saved = page.waitForRequest(
    (request) => request.method() === 'PUT' && request.url().endsWith('/admin/videos/' + video.id),
  );
  await page.getByRole('button', { name: 'Αποθήκευση', exact: true }).click();
  expect((await saved).postDataJSON().description).toBe('Updated workout description');
});

test('Consistency: user pagination stays horizontal at narrow widths and changes pages', async ({
  page,
}, testInfo) => {
  await mock(page);
  await page.route('**/api/admin/users*', (route) => {
    const query = new URL(route.request().url()).searchParams;
    return route.fulfill({
      json: {
        items: users,
        totalCount: 43,
        page: Number(query.get('page') || 1),
        pageSize: Number(query.get('pageSize') || 20),
        totalPages: 3,
      },
    });
  });
  for (const width of [390, 320]) {
    await page.setViewportSize({ width, height: 844 });
    await page.goto('/admin/users');
    const pager = page.locator('mat-paginator');
    await expect(pager).toContainText('1–20 / 43');
    await pager.scrollIntoViewIfNeeded();
    const size = await pager.locator('.mat-mdc-paginator-page-size').boundingBox();
    const range = await pager.locator('.mat-mdc-paginator-range-label').boundingBox();
    const next = await pager
      .getByRole('button', { name: 'Επόμενη σελίδα', exact: true })
      .boundingBox();
    expect(Math.abs(size!.y + size!.height / 2 - range!.y - range!.height / 2)).toBeLessThanOrEqual(
      2,
    );
    expect(Math.abs(next!.y + next!.height / 2 - range!.y - range!.height / 2)).toBeLessThanOrEqual(
      2,
    );
    expect(range!.x).toBeGreaterThanOrEqual(size!.x + size!.width);
    expect(next!.x + next!.width).toBeLessThanOrEqual(width);
    expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(
      width,
    );
    await pager.getByRole('button', { name: 'Επόμενη σελίδα', exact: true }).click();
    await expect(pager).toContainText('21–40 / 43');
    const last = await page.locator('.urow').last().boundingBox();
    const box = await pager.boundingBox();
    expect(box!.y - last!.y - last!.height).toBeGreaterThanOrEqual(24);
    await pager.scrollIntoViewIfNeeded();
      await page.screenshot({ path: testInfo.outputPath('users-' + width + '.png') });
  }
});

test('Consistency: both video lists have compact separated pagination and hide it when empty', async ({
  page,
}) => {
  await mock(page);
  let empty = false;
  await page.route(/\/api\/(admin\/)?videos\?/, (route) =>
    route.fulfill({
      json: {
        items: empty ? [] : [video],
        totalCount: empty ? 0 : 13,
        page: 1,
        pageSize: 12,
        totalPages: empty ? 0 : 2,
      },
    }),
  );
  await page.setViewportSize({ width: 320, height: 844 });
  for (const url of ['/videos', '/admin/videos']) {
    empty = false;
    await page.goto(url);
    const pager = page.locator('.pagination');
    await expect(pager).toBeVisible();
    const controls = await pager.locator('button, span').all();
    const boxes = await Promise.all(controls.map((c) => c.boundingBox()));
    const center = boxes[0]!.y + boxes[0]!.height / 2;
    for (const box of boxes) {
      expect(Math.abs(box!.y + box!.height / 2 - center)).toBeLessThanOrEqual(2);
      expect(box!.x + box!.width).toBeLessThanOrEqual(320);
    }
    empty = true;
    await page.reload();
    await expect(pager).toHaveCount(0);
  }
});

test('Consistency: adjacent fields share heights throughout filters and editors', async ({
  page,
}) => {
  await mock(page);
  const groups = [
    ['/videos', '.filters input, .filters mat-select'],
    ['/admin/videos/' + video.id + '/edit', '.editor-grid select'],
    ['/about/edit', 'input[name="contactEmail"], input[name="phone"]'],
  ];
  for (const [url, selector] of groups) {
    await page.goto(url);
    await expect(page.locator(selector).first()).toBeVisible();
    const boxes = await Promise.all(
      (await page.locator(selector).all()).map((c) => c.boundingBox()),
    );
    expect(boxes.length).toBeGreaterThan(1);
    for (const box of boxes)
      expect(Math.abs(box!.height - boxes[0]!.height)).toBeLessThanOrEqual(1);
    for (let i = 1; i < boxes.length; i++) {
      if (boxes[i]!.x > boxes[i - 1]!.x + boxes[i - 1]!.width)
        expect(Math.abs(boxes[i]!.y - boxes[i - 1]!.y)).toBeLessThanOrEqual(1);
    }
  }
  expect(
    await page.locator('.about-editor__actions').evaluate((e) => getComputedStyle(e).position),
  ).toBe('static');
});
