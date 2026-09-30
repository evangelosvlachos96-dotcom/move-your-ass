import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * The only logo in the app. One mark, one wordmark, one arrangement — every other variant (the
 * stacked lockup, the spaced-capitals "MOVE YOUR ASS" eyebrow) is gone, because a brand that
 * appears twice on a screen in two styles reads as two brands.
 *
 * Drawn as inline SVG rather than an `<img>` so the wordmark inherits the page's font and the
 * accent comes from the brand token. The old SVG files positioned their text at `x=8` inside a
 * wide viewBox, which is why the logo always looked shoved against the left edge.
 *
 * The lockup is mark + (wordmark above tagline). The tagline is right-aligned to the end of
 * "Ass" and the mark is centred against the pair, so the whole thing stays one object. Defining
 * it here and nowhere else is what keeps every appearance identical.
 *
 * `compact` drops the chevron mark and keeps the wordmark: phones, where the mark is too small
 * to read as anything and only costs horizontal space.
 */
@Component({
  selector: 'app-brand-logo',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span
      class="logo"
      [class.logo--compact]="compact()"
      [class.logo--phone-compact]="phoneCompact()"
      role="img"
      [attr.aria-label]="showTagline() ? 'Move Your Ass, powered by Tasos' : 'Move Your Ass'"
    >
      @if (!compact()) {
        <span class="logo__mark" aria-hidden="true">
          <svg viewBox="0 0 34 34" focusable="false">
            <path
              d="M12 8 L22 17 L12 26"
              fill="none"
              stroke="currentColor"
              stroke-width="4"
              stroke-linecap="round"
              stroke-linejoin="round"
            />
          </svg>
        </span>
      }
      <span class="logo__lockup">
        <span class="logo__word">MoveYour<b>Ass</b></span>
        @if (showTagline()) {
          <span class="logo__tagline" aria-hidden="true">powered by Tasos</span>
        }
      </span>
    </span>
  `,
  styleUrl: './brand-logo.component.scss',
})
export class BrandLogoComponent {
  /** Wordmark only. Set on phones and anywhere horizontal space is tight. */
  readonly compact = input(false);

  /**
   * "powered by Tasos" under the wordmark. On by default: the credit belongs everywhere the
   * lockup appears, and an opt-in would drift the moment somebody forgot to set it.
   */
  readonly showTagline = input(true);

  /**
   * Drop the mark below 600px, and centre the credit under the wordmark there.
   *
   * A media query inside this component rather than a breakpoint signal in each page: three auth
   * pages asking the same question three times is three chances to answer it differently, and
   * view encapsulation means a parent cannot reach in and hide the mark itself.
   */
  readonly phoneCompact = input(false);
}
