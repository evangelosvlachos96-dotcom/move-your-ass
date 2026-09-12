import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterOutlet } from '@angular/router';
import { LoadingService } from './core/loading/loading.service';

/** Root: one global progress bar driven by the loading counter, then the router. */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, MatProgressBarModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (loading.isLoading()) {
      <mat-progress-bar class="global-progress" mode="indeterminate" aria-label="Φόρτωση" />
    }
    <router-outlet />
  `,
})
export class App {
  protected readonly loading = inject(LoadingService);
}
