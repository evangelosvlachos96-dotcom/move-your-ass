import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { BrandLogoComponent } from '../../../shared/ui/brand-logo/brand-logo.component';
import {
  PASSWORD_MAX_LENGTH,
  passwordPolicy,
  passwordsMatch,
} from '../../../shared/forms/password-rules';

/**
 * Sets a new password from an emailed link. The token arrives in the URL fragment, not the query
 * string, so it stays out of server logs and referrer headers; it is cleared from the address bar
 * once spent.
 */
@Component({
  selector: 'app-reset-password',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    BrandLogoComponent,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="auth-page">
      <div class="auth-page__panel">
        <app-brand-logo class="auth-page__logo" />

        <mat-card class="auth-page__card" appearance="outlined">
          <mat-card-header class="auth-page__header">
            <mat-card-title>Νέος κωδικός</mat-card-title>
          </mat-card-header>

          <mat-card-content>
            @if (done()) {
              <p role="status">
                Ο κωδικός άλλαξε. Για ασφάλεια αποσυνδέθηκαν όλες οι συσκευές, οπότε συνδέσου ξανά.
              </p>
              <a mat-flat-button routerLink="/login">Σύνδεση</a>
            } @else if (!token) {
              <p role="alert">
                Ο σύνδεσμος δεν είναι έγκυρος. Άνοιξέ τον από το email, ή ζήτησε νέον.
              </p>
              <a mat-flat-button routerLink="/forgot-password">Νέος σύνδεσμος</a>
            } @else {
              <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
                <mat-form-field>
                  <mat-label>Νέος κωδικός</mat-label>
                  <input
                    matInput
                    type="password"
                    formControlName="password"
                    autocomplete="new-password"
                    maxlength="128"
                  />
                  <mat-error>10–128 χαρακτήρες, με κεφαλαίο, πεζό και αριθμό.</mat-error>
                </mat-form-field>
                <mat-form-field>
                  <mat-label>Επιβεβαίωση κωδικού</mat-label>
                  <input
                    matInput
                    type="password"
                    formControlName="confirm"
                    autocomplete="new-password"
                    maxlength="128"
                  />
                  <mat-error>Συμπλήρωσε ξανά τον κωδικό.</mat-error>
                </mat-form-field>
                @if (form.hasError('passwordMismatch') && form.controls.confirm.touched) {
                  <p role="alert">Οι κωδικοί δεν ταιριάζουν.</p>
                }
                <button mat-flat-button type="submit" [disabled]="busy()">
                  {{ busy() ? 'Αποθήκευση…' : 'Αποθήκευση κωδικού' }}
                </button>
              </form>
            }
          </mat-card-content>
        </mat-card>
      </div>
    </div>
  `,
  styles: [`form { display: grid; gap: 12px; } mat-form-field { width: 100%; }`],
})
export class ResetPasswordComponent {
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly token =
    new URLSearchParams(this.route.snapshot.fragment ?? '').get('token') ?? '';

  protected readonly busy = signal(false);
  protected readonly done = signal(false);

  protected readonly form = new FormGroup(
    {
      password: new FormControl('', {
        nonNullable: true,
        validators: [
          Validators.required,
          Validators.maxLength(PASSWORD_MAX_LENGTH),
          passwordPolicy(),
        ],
      }),
      confirm: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    },
    { validators: passwordsMatch('password', 'confirm') },
  );

  protected submit(): void {
    if (this.busy()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid || !this.token) return;

    this.busy.set(true);
    this.auth
      .resetPassword(this.token, this.form.getRawValue().password)
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: () => {
          this.form.reset();
          this.done.set(true);
          void this.router.navigate([], { fragment: undefined, replaceUrl: true });
        },
        error: () => {
          /* Reported by the error interceptor. */
        },
      });
  }
}
