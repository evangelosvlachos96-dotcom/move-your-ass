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
 * `compact` drops the chevron mark and keeps the wordmark: phones, where the mark is too small
 * to read as anything and only costs horizontal space.
 */
@Component({
  selector: 'app-brand-logo',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="logo" [class.logo--compact]="compact()" role="img" aria-label="Move Your Ass">
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
      <span class="logo__word">MoveYour<b>Ass</b></span>
    </span>
  `,
  styleUrl: './brand-logo.component.scss',
})
export class BrandLogoComponent {
  /** Wordmark only. Set on phones and anywhere horizontal space is tight. */
  readonly compact = input(false);
}
