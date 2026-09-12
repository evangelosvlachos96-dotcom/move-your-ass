import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export type ChevronLoaderSize = 'sm' | 'md' | 'lg';

/**
 * Brand loader: three chevrons in the style of the mark, filling left to right in the accent,
 * looping. Inline SVG and CSS only. Sizes: sm 20px, md 40px, lg 64px (height; width is 2.5×).
 *
 * Inside a filled (accent) button the chevrons take the button's text colour instead, or they
 * would vanish against the accent background — see styles.scss.
 *
 * Not for the global progress bar; that stays a Material progress bar.
 */
@Component({
  selector: 'app-chevron-loader',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    role: 'status',
    'aria-label': 'Φόρτωση',
    '[class]': '"chevron-loader chevron-loader--" + size()',
  },
  template: `
    <svg viewBox="0 0 100 40" aria-hidden="true" focusable="false">
      <path class="chevron-loader__chevron" d="M12 6 L26 20 L12 34" />
      <path class="chevron-loader__chevron" d="M43 6 L57 20 L43 34" />
      <path class="chevron-loader__chevron" d="M74 6 L88 20 L74 34" />
    </svg>
  `,
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      line-height: 0;
      color: var(--brand-accent);
    }

    svg {
      display: block;
      fill: none;
      stroke: currentColor;
      stroke-width: 8;
      stroke-linecap: round;
      stroke-linejoin: round;
    }

    :host(.chevron-loader--sm) svg {
      width: 50px;
      height: 20px;
    }

    :host(.chevron-loader--md) svg {
      width: 100px;
      height: 40px;
    }

    :host(.chevron-loader--lg) svg {
      width: 160px;
      height: 64px;
    }

    .chevron-loader__chevron {
      opacity: 0.2;
      animation: chevron-fill 1.2s ease-in-out infinite;
    }

    .chevron-loader__chevron:nth-child(2) {
      animation-delay: 0.2s;
    }

    .chevron-loader__chevron:nth-child(3) {
      animation-delay: 0.4s;
    }

    @keyframes chevron-fill {
      0%,
      60%,
      100% {
        opacity: 0.2;
      }

      20%,
      40% {
        opacity: 1;
      }
    }

    @media (prefers-reduced-motion: reduce) {
      .chevron-loader__chevron {
        animation: none;
        opacity: 0.45;
      }
    }
  `,
})
export class ChevronLoaderComponent {
  readonly size = input<ChevronLoaderSize>('md');
}
