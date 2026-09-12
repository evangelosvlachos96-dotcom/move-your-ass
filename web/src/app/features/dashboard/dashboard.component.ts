import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { AuthStore } from '../../core/auth/auth.store';

/** Placeholder home: proves who is logged in until the video library arrives. */
@Component({
  selector: 'app-dashboard',
  imports: [MatCardModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent {
  protected readonly store = inject(AuthStore);

  protected readonly roleLabel = computed(() => (this.store.user()?.role === 'Admin' ? 'Διαχειριστής' : 'Πελάτης'));
}
