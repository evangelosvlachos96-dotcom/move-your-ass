import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth/auth.service';
import { LoadingService } from './core/loading/loading.service';
import { ChevronLoaderComponent } from './shared/ui/chevron-loader/chevron-loader.component';

/**
 * Root: one global progress bar driven by the loading counter, then the router.
 *
 * Initial navigation is disabled in app.config so the silent refresh can run first, behind a
 * full-page brand loader. Guards then see the restored session; a reload keeps the user where
 * they were instead of bouncing through /login.
 */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, MatProgressBarModule, ChevronLoaderComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (loading.isLoading()) {
      <mat-progress-bar class="global-progress" mode="indeterminate" aria-label="Φόρτωση" />
    }
    @if (booting()) {
      <div class="boot">
        <app-chevron-loader size="lg" />
      </div>
    } @else {
      <router-outlet />
    }
  `,
  styles: `
    .boot {
      min-height: 100dvh;
      display: flex;
      align-items: center;
      justify-content: center;
      background: var(--brand-bg);
    }
  `,
})
export class App {
  protected readonly loading = inject(LoadingService);
  protected readonly booting = signal(true);

  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  constructor() {
    // restoreSession never throws; whatever the outcome, the app starts.
    this.auth.restoreSession().subscribe(() => {
      this.booting.set(false);
      this.router.initialNavigation();
    });
  }
}
