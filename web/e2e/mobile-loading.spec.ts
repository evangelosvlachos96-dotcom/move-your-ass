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
