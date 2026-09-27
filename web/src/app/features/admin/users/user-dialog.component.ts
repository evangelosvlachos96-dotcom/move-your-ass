import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { finalize, Observable } from 'rxjs';
import { AdminUsersApi } from '../../../core/admin/admin-users.api';
import { AdminUser, fullName } from '../../../core/admin/models';
import { UserRole } from '../../../core/auth/models';
import { ErrorCodes, problemCode } from '../../../core/http/problem-details';
import { AuthStore } from '../../../core/auth/auth.store';
import { ConfirmDialogService } from '../../../shared/ui/confirm-dialog/confirm-dialog.service';

const ROLE_LABELS: Record<UserRole, string> = { Admin: 'Διαχειριστής', Client: 'Πελάτης' };

export type UserAction = 'create' | 'edit' | 'approve' | 'decline' | 'suspend' | 'reactivate' | 'delete' | 'resend';
export interface UserDialogData { action: UserAction; user?: AdminUser }

const LABELS: Record<UserAction, string> = {
  create: 'Πρόσκληση νέου χρήστη', edit: 'Επεξεργασία χρήστη', approve: 'Έγκριση εγγραφής',
  decline: 'Απόρριψη εγγραφής', suspend: 'Αναστολή πρόσβασης', reactivate: 'Ενεργοποίηση πρόσβασης',
  delete: 'Διαγραφή χρήστη', resend: 'Νέα πρόσκληση',
};

@Component({
  selector: 'app-user-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ title }}</h2>
    <mat-dialog-content>
      @if (data.user) { <p><strong>{{ name }}</strong><br>{{ data.user.email }}</p> }
      @if (isForm) {
        <form id="user-form" [formGroup]="form" (ngSubmit)="submit()">
          <mat-form-field><mat-label>Όνομα</mat-label><input matInput formControlName="firstName" maxlength="80"><mat-error>Συμπλήρωσε όνομα.</mat-error></mat-form-field>
          <mat-form-field><mat-label>Επώνυμο</mat-label><input matInput formControlName="lastName" maxlength="80"><mat-error>Συμπλήρωσε επώνυμο.</mat-error></mat-form-field>
          @if (data.action === 'create') {
            <mat-form-field><mat-label>Email</mat-label><input matInput formControlName="email" type="email" maxlength="256"><mat-error>Συμπλήρωσε έγκυρο email.</mat-error></mat-form-field>
            <p>Θα σταλεί email για δημιουργία κωδικού. Η πρόσβαση ενεργοποιείται μόλις ολοκληρωθεί αυτό το βήμα.</p>
          } @else {
            <mat-form-field><mat-label>Ρόλος</mat-label><mat-select formControlName="role"><mat-option value="Client">Πελάτης</mat-option><mat-option value="Admin">Διαχειριστής</mat-option></mat-select></mat-form-field>
            @if (isSelf) {
              <p class="user-dialog__note">Δεν μπορείς να αλλάξεις τον δικό σου ρόλο.</p>
            }
          }
        </form>
      } @else {
        <p>{{ explanation }}</p>
        @if (data.action === 'delete') {
          <mat-form-field><mat-label>Πληκτρολόγησε το email για επιβεβαίωση</mat-label><input matInput [formControl]="deleteEmail" autocomplete="off"></mat-form-field>
        }
        @if (data.action === 'decline' || data.action === 'suspend') {
          <mat-form-field><mat-label>Αιτία (προαιρετικά)</mat-label><textarea matInput [formControl]="reason" maxlength="500"></textarea></mat-form-field>
        }
      }
      @if (error()) { <p role="alert">{{ error() }}</p> }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" [disabled]="busy()" (click)="cancel()">Ακύρωση</button>
      <button mat-flat-button type="button" [disabled]="busy() || (data.action === 'delete' && deleteEmail.value !== data.user?.email)" (click)="submit()">{{ busy() ? 'Αποθήκευση…' : title }}</button>
    </mat-dialog-actions>
  `,
  styles: [`form { display: grid; gap: 8px; } mat-form-field { width: 100%; } .user-dialog__note { color: var(--brand-muted); font-size: 0.875rem; margin: 0; }`],
})
export class UserDialogComponent {
  protected readonly data = inject<UserDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<UserDialogComponent>);
  private readonly api = inject(AdminUsersApi);
  protected readonly title = LABELS[this.data.action];
  protected readonly name = this.data.user ? fullName(this.data.user) : '';
  protected readonly isForm = this.data.action === 'create' || this.data.action === 'edit';
  protected readonly explanation = this.data.action === 'delete'
    ? 'Ο λογαριασμός θα διαγραφεί οριστικά. Η ενέργεια δεν αναιρείται.'
    : this.data.action === 'resend' ? 'Θα σταλεί νέος σύνδεσμος δημιουργίας κωδικού. Ο προηγούμενος παύει να ισχύει.'
    : 'Επιβεβαίωσε την ενέργεια για τον παραπάνω χρήστη.';
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly store = inject(AuthStore);

  /**
   * An admin may not change their own role. The server refuses it with CANNOT_MODIFY_SELF; this
   * disables the control so the refusal is never a surprise.
   */
  protected readonly isSelf = this.data.user?.id === this.store.user()?.id;

  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly reason = new FormControl('', { nonNullable: true, validators: [Validators.maxLength(500)] });
  protected readonly deleteEmail = new FormControl('', { nonNullable: true });
  protected readonly form = new FormGroup({
    firstName: new FormControl(this.data.user?.firstName ?? '', { nonNullable: true, validators: [Validators.required, Validators.pattern(/\S/), Validators.maxLength(80)] }),
    lastName: new FormControl(this.data.user?.lastName ?? '', { nonNullable: true, validators: [Validators.required, Validators.pattern(/\S/), Validators.maxLength(80)] }),
    email: new FormControl(this.data.user?.email ?? '', { nonNullable: true, validators: [Validators.required, Validators.email, Validators.maxLength(256)] }),
    role: new FormControl<UserRole>({ value: this.data.user?.role ?? 'Client', disabled: this.data.user?.id === inject(AuthStore).user()?.id }, { nonNullable: true }),
  });

  protected cancel(): void { this.ref.close(false); }

  protected async submit(): Promise<void> {
    if (this.busy()) return;
    if (this.data.action === 'delete' && this.deleteEmail.value !== this.data.user?.email) return;
    this.form.markAllAsTouched();
    if ((this.isForm && this.form.invalid) || this.reason.invalid) return;
    const values = this.form.getRawValue();
    const id = this.data.user?.id ?? '';

    // A role change is the one edit that silently changes what someone can do, so it is named
    // out loud — old role, new role, and whose — before anything is sent.
    if (this.data.action === 'edit' && this.data.user && values.role !== this.data.user.role) {
      const promoting = values.role === 'Admin';
      const confirmed = await this.confirmDialog.confirm({
        title: 'Αλλαγή ρόλου',
        message: `Αλλαγή ρόλου του ${this.name} από ${ROLE_LABELS[this.data.user.role]} σε ${ROLE_LABELS[values.role]};`,
        detail: promoting
          ? 'Ο διαχειριστής βλέπει και αλλάζει όλους τους χρήστες και όλα τα βίντεο.'
          : 'Θα χάσει αμέσως την πρόσβαση διαχειριστή και θα χρειαστεί να συνδεθεί ξανά.',
        confirmLabel: 'Αλλαγή ρόλου',
        destructive: !promoting,
      });
      if (!confirmed) return;
    }
    let request: Observable<unknown>;
    switch (this.data.action) {
      case 'create': request = this.api.create({ firstName: values.firstName.trim(), lastName: values.lastName.trim(), email: values.email.trim() }); break;
      case 'edit': request = this.api.update(id, { firstName: values.firstName.trim(), lastName: values.lastName.trim(), role: values.role }); break;
      case 'approve': request = this.api.approve(id); break;
      case 'decline': request = this.api.decline(id, this.reason.value.trim() || null); break;
      case 'suspend': request = this.api.suspend(id, this.reason.value.trim() || null); break;
      case 'reactivate': request = this.api.reactivate(id); break;
      case 'delete': request = this.api.delete(id); break;
      case 'resend': request = this.api.resendInvitation(id); break;
    }
    this.busy.set(true);
    this.ref.disableClose = true;
    this.error.set('');
    request.pipe(finalize(() => { this.busy.set(false); this.ref.disableClose = false; })).subscribe({
      next: () => this.ref.close(true),
      error: (error: unknown) => {
        this.error.set(problemCode(error) === ErrorCodes.EmailAlreadyExists ? 'Το email χρησιμοποιείται ήδη.'
          : problemCode(error) === ErrorCodes.UserNotPending ? 'Η εγγραφή έχει ήδη εξεταστεί. Κλείσε και ανανέωσε τη λίστα.'
          : 'Η ενέργεια δεν ολοκληρώθηκε. Έλεγξε τα στοιχεία ή δοκίμασε ξανά.');
      },
    });
  }
}
