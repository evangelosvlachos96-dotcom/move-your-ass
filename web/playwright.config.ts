import { defineConfig, devices } from '@playwright/test';

/**
 * Smoke suite: loads the key routes at the three viewports the product is designed for and fails
 * on horizontal overflow or console errors. It is not a functional test suite — it is the check
 * that the layout has not broken somewhere nobody looked.
 *
 * It needs the app running. `npm start` in one terminal and `npm run e2e` in another, or set
 * E2E_BASE_URL at a deployed instance. Credentials come from E2E_ADMIN_EMAIL / E2E_ADMIN_PASSWORD
 * and E2E_CLIENT_EMAIL / E2E_CLIENT_PASSWORD; the suite skips the signed-in routes without them,
 * so CI without a database still runs the public ones.
 *
 * Each project signs in once per role. Sign-in is throttled to five attempts per (email, IP) per
 * fifteen minutes, so a full run costs three of them per account — fine once, but two runs
 * back to back will meet the limit and say so.
 */
export const VIEWPORTS = [
  { name: 'phone', width: 390, height: 844 },
  { name: 'tablet', width: 820, height: 1180 },
  { name: 'desktop', width: 1440, height: 900 },
] as const;

export default defineConfig({
  testDir: './e2e',
  timeout: 45_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  workers: 1,
  reporter: process.env['CI'] ? [['github'], ['list']] : [['list']],
  use: {
    baseURL: process.env['E2E_BASE_URL'] ?? 'http://localhost:4200',
    // Screenshots are deliberately not written into the repository.
    screenshot: 'off',
    video: 'off',
    trace: 'retain-on-failure',
  },
  projects: [
    ...VIEWPORTS.map((viewport) => ({
      name: viewport.name,
      use: {
        ...devices['Desktop Chrome'],
        viewport: { width: viewport.width, height: viewport.height },
      },
    })),
    // Safari's engine, at the two sizes clients actually hold. The trainer's clients are on
    // iPhones, where every browser is WebKit underneath, so Chromium alone would leave the most
    // common case untested. Desktop is left to Chromium: the admin screens are used on a laptop.
    ...VIEWPORTS.filter((viewport) => viewport.name !== 'desktop').map((viewport) => ({
      name: `${viewport.name}-webkit`,
      // WebKit on Windows is materially slower than Chromium at the same work — a run takes
      // roughly three times as long — and these tests measure layout, so a truncated one reports
      // a page that was still settling rather than a page that is wrong. The extra allowance
      // costs nothing when a test passes.
      timeout: 90_000,
      use: {
        ...devices['Desktop Safari'],
        viewport: { width: viewport.width, height: viewport.height },
      },
    })),
  ],
});
