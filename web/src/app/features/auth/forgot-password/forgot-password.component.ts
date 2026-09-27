import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { BrandLogoComponent } from '../../../shared/ui/brand-logo/brand-logo.component';

/**
 * Asks for a reset link.
 *
 * The confirmation says a link "has been sent if the address belongs to an account" and says the
 * same thing whatever happened, because the server answers the same either way. Saying "no such
 * account" here would turn this form into a way to find out who the trainer's clients are.
 */
@Component({
  selector: 'app-forgot-password',
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
            <mat-card-title>Ξέχασες τον κωδικό;</mat-card-title>
            <mat-card-subtitle>Θα σου στείλουμε σύνδεσμο για νέο κωδικό.</mat-card-subtitle>
          </mat-card-header>

          <mat-card-content>
            @if (sent()) {
              <p role="status">
                Αν η διεύθυνση αντιστοιχεί σε λογαριασμό, στάλθηκε email με σύνδεσμο για νέο
                κωδικό. Ο σύνδεσμος λήγει σε μία ώρα.
              </p>
              <p>Δεν ήρθε; Έλεγξε τα ανεπιθύμητα, ή δοκίμασε ξανά σε λίγο.</p>
            } @else {
              <form (ngSubmit)="submit()" novalidate>
                <mat-form-field>
                  <mat-label>Email</mat-label>
                  <input
                    matInput
                    type="email"
                    [formControl]="email"
                    autocomplete="email"
                    maxlength="256"
                  />
                  <mat-error>Συμπλήρωσε έγκυρο email.</mat-error>
                </mat-form-field>
                <button mat-flat-button type="submit" [disabled]="busy()">
                  {{ busy() ? 'Αποστολή…' : 'Στείλε μου σύνδεσμο' }}
                </button>
              </form>
            }
            <p class="auth-page__alt"><a routerLink="/login">Επιστροφή στη σύνδεση</a></p>
          </mat-card-content>
        </mat-card>
      </div>
    </div>
  `,
  styles: [`form { display: grid; gap: 12px; } mat-form-field { width: 100%; } .auth-page__alt { margin-block-start: 16px; }`],
})
export class ForgotPasswordComponent {
  private readonly auth = inject(AuthService);

  protected readonly busy = signal(false);
  protected readonly sent = signal(false);

  protected readonly email = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.email, Validators.maxLength(256)],
  });

  protected submit(): void {
    if (this.busy()) return;
    this.email.markAsTouched();
    if (this.email.invalid) return;

    this.busy.set(true);
    this.auth
      .forgotPassword(this.email.value.trim())
      .pipe(finalize(() => this.busy.set(false)))
      // Success and failure look the same to the user on purpose; a rate-limit refusal is the
      // one case the interceptor still surfaces, because retrying immediately will not help.
      .subscribe({ next: () => this.sent.set(true), error: () => this.sent.set(true) });
  }
}
