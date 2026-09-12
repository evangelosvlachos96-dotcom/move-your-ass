import { BreakpointObserver } from '@angular/cdk/layout';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from '../core/auth/auth.service';
import { AuthStore } from '../core/auth/auth.store';

interface NavItem {
  label: string;
  icon: string;
  link: string;
}

/** Toolbar + sidenav + outlet. Sidenav is a drawer under 960px and a fixed column above. */
@Component({
  selector: 'app-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatSidenavModule,
    MatListModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
})
export class ShellComponent {
  private readonly breakpoints = inject(BreakpointObserver);
  private readonly auth = inject(AuthService);

  protected readonly store = inject(AuthStore);

  protected readonly isWide = toSignal(
    this.breakpoints.observe('(min-width: 960px)').pipe(map((result) => result.matches)),
    { initialValue: false },
  );

  /** Drawer state in narrow mode; ignored in wide mode where the nav is always open. */
  protected readonly drawerOpen = signal(false);

  protected readonly navItems: readonly NavItem[] = [{ label: 'Πίνακας', icon: 'dashboard', link: '/dashboard' }];

  protected toggleDrawer(): void {
    this.drawerOpen.update((open) => !open);
  }

  protected closeDrawerIfNarrow(): void {
    if (!this.isWide()) {
      this.drawerOpen.set(false);
    }
  }

  protected logout(): void {
    this.auth.logout().subscribe();
  }
}
