import { Injectable, inject, signal } from '@angular/core';
import { catchError, map, of } from 'rxjs';
import { silent } from '../http/http-context';
import { ApiClient } from '../http/api-client.service';
import { AdminUser, PagedResult } from './models';

/**
 * Number of registrations waiting for the admin, shown as a badge on the Χρήστες nav item.
 * Fetched once when an admin session starts and again after every approve or decline. No
 * polling: nothing else changes it.
 */
@Injectable({ providedIn: 'root' })
export class PendingRegistrationsService {
  private readonly api = inject(ApiClient);
  private readonly _count = signal<number | null>(null);

  /** Null until the first fetch completes. */
  readonly count = this._count.asReadonly();

  refresh(): void {
    this.api
      .get<PagedResult<AdminUser>>('/admin/users', {
        params: { status: 'PendingApproval', page: 1, pageSize: 1 },
        context: silent(),
      })
      .pipe(
        map((page) => page.totalCount),
        catchError(() => of(null)),
      )
      .subscribe((count) => this._count.set(count));
  }

  set(count: number): void {
    this._count.set(count);
  }

  clear(): void {
    this._count.set(null);
  }
}
