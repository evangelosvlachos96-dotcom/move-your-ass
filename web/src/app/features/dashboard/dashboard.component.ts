import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../core/auth/auth.store';
import { AdminUsersApi } from '../../core/admin/admin-users.api';
import { AdminUser, fullName } from '../../core/admin/models';

@Component({
  selector: 'app-dashboard',
  imports: [MatCardModule, MatButtonModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent {
  protected readonly store = inject(AuthStore);
  private readonly api = inject(AdminUsersApi);
  protected readonly pending = signal<AdminUser[]>([]);
  protected readonly count = signal<number | null>(null);
  protected readonly failed = signal(false);
  protected readonly fullName = fullName;

  constructor() {
    if (this.store.user()?.role === 'Admin') {
      this.api.list({ status: 'PendingApproval', page: 1, pageSize: 5 }).pipe(takeUntilDestroyed()).subscribe({
        next: result => { this.pending.set(result.items); this.count.set(result.totalCount); },
        error: () => this.failed.set(true),
      });
    }
  }
}
