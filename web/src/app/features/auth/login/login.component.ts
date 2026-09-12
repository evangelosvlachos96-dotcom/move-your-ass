import { ChangeDetectionStrategy, Component, ElementRef, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, FormGroupDirective, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { ErrorCodes, problemCode } from '../../../core/http/problem-details';
import { focusControl, focusFirstInvalid } from '../../../shared/forms/focus-first-invalid';
import { ChevronLoaderComponent } from '../../../shared/ui/chevron-loader/chevron-loader.component';

@Component({
  selector: 'app-login',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    ChevronLoaderComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './login.component.html',
})
export class LoginComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly submitting = signal(false);
  protected readonly hidePassword = signal(true);

  /** Form-level message for INVALID_CREDENTIALS. Deliberately does not say which one was wrong. */
  protected readonly loginError = signal<string | null>(null);

  protected readonly form = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  private readonly formDirective = viewChild.required(FormGroupDirective);

  constructor() {
    // The message answers one attempt; editing either field starts the next one.
    this.form.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => this.loginError.set(null));
  }

  protected submit(): void {
    if (this.submitting()) {
      return;
    }

    if (this.form.invalid) {
      // The error state matcher shows every error once the form is submitted; this just puts
      // the cursor where the first fix is needed.
      this.form.markAllAsTouched();
      focusFirstInvalid(this.form, this.host.nativeElement);
      return;
    }

    this.submitting.set(true);
    this.auth
      .login(this.form.getRawValue())
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: (user) => {
          const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
          void this.router.navigateByUrl(user.mustChangePassword ? '/change-password' : (returnUrl ?? '/dashboard'));
        },
        error: (error: unknown) => {
          // Clear the password through the directive so `submitted` resets too; otherwise the
          // now-empty field would go red with "required" next to the message below it.
          this.formDirective().resetForm({ email: this.form.controls.email.value, password: '' });
          if (problemCode(error) === ErrorCodes.InvalidCredentials) {
            this.loginError.set('Λάθος email ή κωδικός.');
            focusControl(this.host.nativeElement, 'password');
          }
          // Anything else was already reported by the error interceptor.
        },
      });
  }
}
