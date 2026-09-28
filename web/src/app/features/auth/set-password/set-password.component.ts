import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { SiteFooterComponent } from '../../../shared/ui/site-footer/site-footer.component';
import { TokenPageNotice } from '../../../core/auth/token-page-notice.service';
import { passwordPolicy, passwordsMatch, PASSWORD_MAX_LENGTH } from '../../../shared/forms/password-rules';

@Component({
  selector: 'app-set-password',
  imports: [SiteFooterComponent, ReactiveFormsModule, RouterLink, MatButtonModule, MatCardModule, MatFormFieldModule, MatInputModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <mat-card class="setup" appearance="outlined"><mat-card-content>
      <h1>Δημιουργία κωδικού</h1>
      @if (signedOutPrevious) {
        <p role="status" class="setup__notice">
          Αποσυνδεθήκατε από τον προηγούμενο λογαριασμό για να ορίσετε κωδικό.
        </p>
      }
      @if (done()) {
        <p role="status">Ο λογαριασμός σου ενεργοποιήθηκε. Μπορείς να συνδεθείς με τον νέο κωδικό.</p>
        <a mat-flat-button routerLink="/login">Σύνδεση</a>
      } @else if (!token) {
        <p role="alert">Ο σύνδεσμος δεν είναι έγκυρος. Άνοιξε τον σύνδεσμο από το email ή ζήτησε νέα πρόσκληση.</p>
      } @else {
        <p>Διάλεξε τον προσωπικό σου κωδικό για να ενεργοποιήσεις τον λογαριασμό σου.</p>
        <form [formGroup]="form" (ngSubmit)="submit()">
          <mat-form-field><mat-label>Νέος κωδικός</mat-label>
            <input matInput type="password" formControlName="password" autocomplete="new-password" maxlength="128">
            <mat-error>10–128 χαρακτήρες, με κεφαλαίο, πεζό και αριθμό.</mat-error>
          </mat-form-field>
          <mat-form-field><mat-label>Επιβεβαίωση κωδικού</mat-label>
            <input matInput type="password" formControlName="confirm" autocomplete="new-password" maxlength="128">
            <mat-error>Συμπλήρωσε ξανά τον κωδικό.</mat-error>
          </mat-form-field>
          @if (form.hasError('passwordMismatch') && form.controls.confirm.touched) {
            <p role="alert">Οι κωδικοί δεν ταιριάζουν.</p>
          }
          <button mat-flat-button type="submit" [disabled]="busy()">{{ busy() ? 'Αποθήκευση…' : 'Ενεργοποίηση λογαριασμού' }}</button>
        </form>
      }
    </mat-card-content></mat-card>
    <app-site-footer />
  `,
  styles: [`.setup { max-width: 480px; margin: 48px auto; padding: 16px; } form { display: grid; gap: 12px; } .setup__notice { color: var(--brand-muted); } @media(max-width: 520px) { .setup { margin: 24px 16px; } }`],
})
export class SetPasswordComponent {
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly token = new URLSearchParams(this.route.snapshot.fragment ?? '').get('token') ?? '';

  /**
   * True when the guard had to end somebody's session to show this page. Worth saying out loud:
   * the person following the link is usually not the person who was logged in.
   */
  protected readonly signedOutPrevious = inject(TokenPageNotice).consume();
  protected readonly busy = signal(false);
  protected readonly done = signal(false);
  protected readonly form = new FormGroup({
    password: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(PASSWORD_MAX_LENGTH), passwordPolicy()] }),
    confirm: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  }, { validators: passwordsMatch('password', 'confirm') });

  protected submit(): void {
    if (this.busy()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid || !this.token) return;
    this.busy.set(true);
    this.auth.acceptInvitation(this.token, this.form.getRawValue().password)
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({ next: () => {
        this.form.reset();
        this.done.set(true);
        void this.router.navigate([], { fragment: undefined, replaceUrl: true });
      }, error: () => { /* Reported by the error interceptor. */ } });
  }
}
