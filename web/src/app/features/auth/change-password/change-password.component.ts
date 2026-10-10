import { ChangeDetectionStrategy, Component, ElementRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { Router, RouterLink } from '@angular/router';
import { EMPTY, catchError, finalize, of, switchMap, tap } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { AuthStore } from '../../../core/auth/auth.store';
import { ErrorCodes, problemCode } from '../../../core/http/problem-details';
import { NotifyService } from '../../../core/ui/notify.service';
import { focusControl, focusFirstInvalid } from '../../../shared/forms/focus-first-invalid';
import {
  PASSWORD_MAX_LENGTH,
  passwordPolicy,
  passwordsMatch,
} from '../../../shared/forms/password-rules';
import { CrossFieldErrorStateMatcher } from '../../../shared/forms/reward-early-punish-late-error-state-matcher';
import { ChevronLoaderComponent } from '../../../shared/ui/chevron-loader/chevron-loader.component';
import { PasswordChecklistComponent } from '../../../shared/ui/password-checklist/password-checklist.component';

/**
 * Two ways in: forced by mustChangePasswordGuard after an admin-created temporary password
 * (the shell hides navigation while `store.mustChangePassword()` is true), or voluntarily from
 * the user menu. Same form; different heading, and the forced path drops the token claim with
 * one refresh before moving on.
 */
@Component({
  selector: 'app-change-password',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    ChevronLoaderComponent,
    PasswordChecklistComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './change-password.component.html',
  styleUrl: './change-password.component.scss',
})
export class ChangePasswordComponent {
  private readonly auth = inject(AuthService);
  private readonly store = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly notify = inject(NotifyService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly forced = this.store.mustChangePassword;
  protected readonly submitting = signal(false);
  protected readonly hideCurrent = signal(true);
  protected readonly hideNew = signal(true);
  protected readonly passwordMaxLength = PASSWORD_MAX_LENGTH;

  protected readonly form = new FormGroup(
    {
      currentPassword: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required],
      }),
      newPassword: new FormControl('', {
        nonNullable: true,
        validators: [
          Validators.required,
          Validators.maxLength(PASSWORD_MAX_LENGTH),
          passwordPolicy(),
        ],
      }),
      confirmPassword: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required],
      }),
    },
    { validators: passwordsMatch('newPassword', 'confirmPassword') },
  );

  protected readonly confirmMatcher = new CrossFieldErrorStateMatcher(['passwordMismatch']);

  protected readonly newPasswordValue = toSignal(this.form.controls.newPassword.valueChanges, {
    initialValue: '',
  });

  constructor() {
    this.form.controls.currentPassword.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => {
      if (this.form.controls.newPassword.hasError('samePassword')) {
        this.form.controls.newPassword.updateValueAndValidity();
      }
    });
    // Forced change: the user logged in with the temporary password seconds ago and the admin
    // knows it anyway. The API confirms MustChangePassword from the database before accepting a
    // request without it, so the field is not just hidden, it is not sent.
    if (this.forced()) {
      this.form.controls.currentPassword.disable();
    }
  }

  protected submit(): void {
    if (this.submitting()) {
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      if (!focusFirstInvalid(this.form, this.host.nativeElement)) {
        focusControl(this.host.nativeElement, 'confirmPassword');
      }
      return;
    }

    const { currentPassword, newPassword } = this.form.getRawValue();
    const wasForced = this.forced();

    if (!wasForced && currentPassword === newPassword) {
      this.form.controls.newPassword.setErrors({ samePassword: true });
      this.form.controls.newPassword.markAsTouched();
      focusControl(this.host.nativeElement, 'newPassword');
      return;
    }
    let passwordChanged = false;
    this.submitting.set(true);
    this.auth
      .changePassword(wasForced ? { newPassword } : { currentPassword, newPassword })
      .pipe(
        tap(() => {
          passwordChanged = true;
        }),
        // Forced path: the access token still says must_change_password until refreshed.
        switchMap(() => (wasForced ? this.auth.clearMustChangePassword() : of(null))),
        catchError((error: unknown) => {
          if (problemCode(error) === ErrorCodes.CurrentPasswordWrong) {
            // Cleared by the next keystroke: the validators re-run and replace the error set.
            this.form.controls.currentPassword.setErrors({ wrong: true });
            focusControl(this.host.nativeElement, 'currentPassword');
            return EMPTY;
          }
          if (wasForced && passwordChanged) {
            // The password changed but the refresh did not come back: start over with the new one.
            this.auth.forceLogout('Ο κωδικός άλλαξε. Συνδέσου ξανά με τον νέο κωδικό.');
          }
          // Anything else was already reported by the error interceptor.
          return EMPTY;
        }),
        finalize(() => this.submitting.set(false)),
      )
      .subscribe(() => {
        this.notify.info('Ο κωδικός σου άλλαξε.');
        void this.router.navigateByUrl('/dashboard');
      });
  }
}
