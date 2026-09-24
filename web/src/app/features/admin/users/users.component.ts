import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { catchError, combineLatest, debounceTime, distinctUntilChanged, map, of, Subject, startWith, switchMap, tap } from 'rxjs';
import { AdminUsersApi } from '../../../core/admin/admin-users.api';
import { AdminUser, fullName, USER_STATUSES, USER_STATUS_LABELS } from '../../../core/admin/models';
import { PendingRegistrationsService } from '../../../core/admin/pending-registrations.service';
import { UserStatus } from '../../../core/auth/models';
import { AuthStore } from '../../../core/auth/auth.store';
import { NotifyService } from '../../../core/ui/notify.service';
import { UserAction, UserDialogComponent } from './user-dialog.component';
import { AthensDatePipe } from '../../../shared/pipes/athens-date.pipe';

@Component({
  selector: 'app-users',
  imports: [ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatPaginatorModule, AthensDatePipe],
  templateUrl: './users.component.html',
  styleUrl: './users.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UsersComponent {
  private readonly api = inject(AdminUsersApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  private readonly pending = inject(PendingRegistrationsService);
  private readonly notify = inject(NotifyService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly refresh = new Subject<void>();
  protected readonly store = inject(AuthStore);
  protected readonly users = signal<AdminUser[]>([]);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  protected readonly total = signal(0);
  protected readonly pageIndex = signal(0);
  protected readonly pageSize = signal(20);
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly status = new FormControl<UserStatus | ''>('', { nonNullable: true });
  protected readonly statuses = USER_STATUSES;
  protected readonly labels = USER_STATUS_LABELS;
  protected readonly fullName = fullName;

  constructor() {
    combineLatest([this.route.queryParamMap, this.refresh.pipe(startWith(undefined))]).pipe(
      map(([params]) => {
        const rawStatus = params.get('status') as UserStatus;
        const status = USER_STATUSES.includes(rawStatus) ? rawStatus : '';
        const page = Math.max(1, Math.trunc(Number(params.get('page'))) || 1);
        const pageSize = [10, 20, 50].includes(Number(params.get('pageSize'))) ? Number(params.get('pageSize')) : 20;
        this.status.setValue(status, { emitEvent: false });
        this.search.setValue(params.get('search') ?? '', { emitEvent: false });
        this.pageIndex.set(page - 1);
        this.pageSize.set(pageSize);
        return { status: status || undefined, search: this.search.value, page, pageSize };
      }),
      tap(() => { this.loading.set(true); this.failed.set(false); this.users.set([]); }),
      switchMap(query => this.api.list(query).pipe(catchError(() => { this.failed.set(true); return of(null); }))),
      takeUntilDestroyed(),
    ).subscribe(result => {
      this.loading.set(false);
      this.users.set(result?.items ?? []);
      this.total.set(result?.totalCount ?? 0);
    });
    this.search.valueChanges.pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe(() => this.filter());
  }

  protected filter(): void { this.navigate(1, this.pageSize()); }
  protected paginate(event: PageEvent): void { this.navigate(event.pageIndex + 1, event.pageSize); }
  protected reload(): void { this.refresh.next(); }

  private navigate(page: number, pageSize: number): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: {
      status: this.status.value || null, search: this.search.value.trim() || null, page, pageSize,
    } }).then(changed => { if (!changed) this.refresh.next(); });
  }

  protected open(action: UserAction, user?: AdminUser): void {
    this.dialog.open(UserDialogComponent, { width: '480px', maxWidth: '95vw', data: { action, user } })
      .afterClosed().pipe(takeUntilDestroyed(this.destroyRef)).subscribe((changed: boolean) => {
        if (!changed) return;
        this.notify.info(action === 'create' || action === 'resend' ? 'Η πρόσκληση μπήκε στην ουρά αποστολής.' : 'Η αλλαγή αποθηκεύτηκε.');
        this.pending.refresh();
        // Return to the first page so deleting the final row cannot strand an empty last page.
        this.filter();
      });
  }
}
