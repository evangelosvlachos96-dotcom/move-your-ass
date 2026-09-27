import { BreakpointObserver } from '@angular/cdk/layout';
import { ChangeDetectionStrategy, Component, computed, effect, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, map, startWith } from 'rxjs';
import { AuthService } from '../core/auth/auth.service';
import { AuthStore } from '../core/auth/auth.store';
import { PendingRegistrationsService } from '../core/admin/pending-registrations.service';
import { BrandLogoComponent } from '../shared/ui/brand-logo/brand-logo.component';
import { ConfirmDialogService } from '../shared/ui/confirm-dialog/confirm-dialog.service';

interface NavItem {
  label: string;
  /** Bottom-bar label. Short enough not to wrap on a 390px screen split four ways. */
  short: string;
  icon: string;
  link: string;
}

/**
 * The application frame: a sidebar on tablet and desktop, a bottom bar on phones, and a top bar
 * that carries the page title and the profile menu. Sidebar and top bar share one surface and
 * one border so they read as a single L-shaped frame rather than two panels meeting at a seam.
 *
 * There is no hamburger. On a phone the four destinations are always visible at the bottom,
 * within thumb reach, which is both fewer taps and a clearer sense of where you are.
 *
 * While the user must change a temporary password (`forced`) there is nowhere else to go: the
 * navigation and the menu shortcuts are hidden and only "log out" remains. The API enforces this
 * regardless (403 MUST_CHANGE_PASSWORD); the UI just stops pretending otherwise.
 */
@Component({
  selector: 'app-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatDividerModule,
    BrandLogoComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
})
export class ShellComponent {
  private readonly breakpoints = inject(BreakpointObserver);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly confirmDialog = inject(ConfirmDialogService);

  protected readonly store = inject(AuthStore);
  protected readonly pending = inject(PendingRegistrationsService);
  protected readonly forced = this.store.mustChangePassword;

  /** Below 600px the sidebar is replaced by the bottom bar. */
  protected readonly isPhone = toSignal(
    this.breakpoints.observe('(max-width: 599px)').pipe(map((result) => result.matches)),
    { initialValue: false },
  );

  protected readonly navItems = computed<readonly NavItem[]>(() => [
    { label: 'Πίνακας', short: 'Πίνακας', icon: 'dashboard', link: '/dashboard' },
    { label: 'Προπονήσεις', short: 'Προπονήσεις', icon: 'play_circle', link: '/videos' },
    ...(this.store.user()?.role === 'Admin'
      ? [
          { label: 'Βίντεο', short: 'Βίντεο', icon: 'video_library', link: '/admin/videos' },
          { label: 'Χρήστες', short: 'Χρήστες', icon: 'group', link: '/admin/users' },
        ]
      : []),
  ]);

  /**
   * The top bar names the current page. The brand does not appear here on desktop — it is in the
   * sidebar, once.
   */
  protected readonly pageTitle = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      startWith(null),
      map(() => TITLES[stripQuery(this.router.url)] ?? 'Move Your Ass'),
    ),
    { initialValue: 'Move Your Ass' },
  );

  constructor() {
    effect(() => {
      if (this.store.user()?.role === 'Admin' && !this.forced()) this.pending.refresh();
      else this.pending.clear();
    });
  }

  protected async logout(): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      title: 'Αποσύνδεση',
      message: 'Θέλεις να αποσυνδεθείς;',
      confirmLabel: 'Αποσύνδεση',
    });

    if (ok) {
      this.auth.logout().subscribe();
    }
  }
}

const TITLES: Record<string, string> = {
  '/dashboard': 'Πίνακας',
  '/videos': 'Προπονήσεις',
  '/admin/videos': 'Διαχείριση βίντεο',
  '/admin/users': 'Χρήστες',
  '/profile': 'Προφίλ',
  '/change-password': 'Αλλαγή κωδικού',
};

/** `/admin/users?status=...` is still the users page. Sub-routes fall back to the brand name. */
function stripQuery(url: string): string {
  return url.split('?')[0];
}
