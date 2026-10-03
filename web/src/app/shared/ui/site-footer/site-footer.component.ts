import { ChangeDetectionStrategy, Component } from '@angular/core';

/**
 * Credit line, on every page including the signed-out ones.
 *
 * `rel="noopener noreferrer"` on a `target="_blank"` link is not optional: without `noopener` the
 * opened page gets a handle on this one through `window.opener`.
 */
@Component({
  selector: 'app-site-footer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <footer class="site-footer">
      <small>
        Created by
        <a href="https://www.linkedin.com/in/evanvlac/" target="_blank" rel="noopener noreferrer"
          >Evangelos Vlachos</a
        >
        · {{ year }}
      </small>
    </footer>
  `,
  styleUrl: './site-footer.component.scss',
})
export class SiteFooterComponent {
  protected readonly year = new Date().getFullYear();
}
