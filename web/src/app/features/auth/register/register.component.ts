import { ChangeDetectionStrategy, Component, ElementRef, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { ErrorCodes, problemCode } from '../../../core/http/problem-details';
import { focusControl, focusFirstInvalid } from '../../../shared/forms/focus-first-invalid';
import { PASSWORD_MAX_LENGTH, passwordPolicy, passwordsMatch } from '../../../shared/forms/password-rules';
import { CrossFieldErrorStateMatcher } from '../../../shared/forms/reward-early-punish-late-error-state-matcher';
import { ChevronLoaderComponent } from '../../../shared/ui/chevron-loader/chevron-loader.component';
import { PasswordChecklistComponent } from '../../../shared/ui/password-checklist/password-checklist.component';

const NAME_MAX_LENGTH = 80;
const EMAIL_MAX_LENGTH = 256;

/** Self-service registration: lands on /pending, the admin approves from their side. */
@Component({
  selector: 'app-register',
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
  templateUrl: './register.component.html',
})
export class RegisterComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly submitting = signal(false);
  protected readonly hidePassword = signal(true);
  protected readonly nameMaxLength = NAME_MAX_LENGTH;
  protected readonly passwordMaxLength = PASSWORD_MAX_LENGTH;

  protected readonly form = new FormGroup(
    {
      firstName: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required, Validators.maxLength(NAME_MAX_LENGTH)],
      }),
      lastName: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required, Validators.maxLength(NAME_MAX_LENGTH)],
      }),
      email: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required, Validators.email, Validators.maxLength(EMAIL_MAX_LENGTH)],
      }),
      password: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required, Validators.maxLength(PASSWORD_MAX_LENGTH), passwordPolicy()],
      }),
      confirmPassword: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    },
    { validators: passwordsMatch('password', 'confirmPassword') },
  );

  /** The mismatch lives on the group; the confirm field is where it is shown. */
  protected readonly confirmMatcher = new CrossFieldErrorStateMatcher(['passwordMismatch']);

  protected readonly passwordValue = toSignal(this.form.controls.password.valueChanges, { initialValue: '' });

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

    const { firstName, lastName, email, password } = this.form.getRawValue();

    this.submitting.set(true);
    this.auth
      .register({ firstName, lastName, email, password })
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: () => void this.router.navigateByUrl('/pending'),
        error: (error: unknown) => {
          if (problemCode(error) === ErrorCodes.EmailAlreadyExists) {
            // Cleared by the next keystroke: the validators re-run and replace the error set.
            this.form.controls.email.setErrors({ emailTaken: true });
            focusControl(this.host.nativeElement, 'email');
          }
          // Anything else was already reported by the error interceptor.
        },
      });
  }
}
