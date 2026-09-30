import { expect, test } from '@playwright/test';

/**
 * Chrome paints its own pale background behind an autofilled value and re-renders the text its
 * own way, which on a dark theme is a white box with near-invisible text that looks like a
 * different font. `styles.scss` overrides it with an inset box-shadow, a matching
 * `-webkit-text-fill-color`, and inherited font metrics.
 *
 * The autofill state cannot be triggered from script — it needs a real profile picking a real
 * suggestion — so this asserts the rules are present and correct rather than pretending to
 * reproduce the interaction. What it does check for real is the thing that actually goes wrong:
 * a typed value and its field looking the way the theme says they should, in both engines.
 */
test.describe('autofill styling', () => {
  test('the login fields keep the dark theme and the page font when filled', async ({ page }) => {
    await page.goto('/login');

    const email = page.locator('input[formControlName="email"]');
    await email.fill('someone@example.test');

    const style = await email.evaluate((node) => {
      const computed = getComputedStyle(node);
      return {
        color: computed.color,
        fontFamily: computed.fontFamily,
        fontSize: computed.fontSize,
        fontWeight: computed.fontWeight,
      };
    });

    // Bone on dark, not Chrome's black-on-pale-blue.
    expect(style.color).toBe('rgb(245, 245, 240)');
    expect(style.fontSize).not.toBe('');

    // The password field must match the email field exactly; a mismatch here is what reads as
    // "a different font" once one of the two has been autofilled.
    const password = page.locator('input[formControlName="password"]');
    await password.fill('Correct-Horse-9');
    const passwordStyle = await password.evaluate((node) => {
      const computed = getComputedStyle(node);
      return {
        color: computed.color,
        fontFamily: computed.fontFamily,
        fontSize: computed.fontSize,
        fontWeight: computed.fontWeight,
      };
    });

    expect(passwordStyle).toEqual(style);
  });

  test('an autofill override is present in the stylesheet', async ({ page }) => {
    await page.goto('/login');

    // Reads the loaded stylesheets rather than the source, so a build that dropped the rule fails.
    const rules = await page.evaluate(() => {
      const found: string[] = [];
      for (const sheet of Array.from(document.styleSheets)) {
        let cssRules: CSSRuleList;
        try {
          cssRules = sheet.cssRules;
        } catch {
          continue; // Cross-origin sheet; not ours.
        }
        for (const rule of Array.from(cssRules)) {
          if (rule.cssText.includes('autofill')) {
            found.push(rule.cssText);
          }
        }
      }
      return found;
    });

    expect(rules.length, 'no :autofill rules reached the browser').toBeGreaterThan(0);
    const all = rules.join(' ');
    // The three parts that matter: the painted background, the text colour, and the long
    // transition that suppresses Chrome's highlight animation.
    expect(all).toContain('inset');
    expect(all).toMatch(/text-fill-color/);
    expect(all).toMatch(/100000s|transition/);
  });
});
