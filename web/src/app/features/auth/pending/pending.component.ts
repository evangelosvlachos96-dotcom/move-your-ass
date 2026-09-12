import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { RouterLink } from '@angular/router';

/**
 * Landing after registration and for ACCOUNT_PENDING on login. Nothing to poll and nothing to
 * click: the admin activates the account and the client gets an email.
 */
@Component({
  selector: 'app-pending',
  imports: [MatCardModule, MatButtonModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="auth-page">
      <div class="auth-page__panel">
        <img class="auth-page__logo" src="assets/brand/logo-stacked.svg" alt="MoveYourAss" width="160" height="92" />

        <mat-card class="auth-page__card" appearance="outlined">
          <mat-card-header class="auth-page__header">
            <mat-card-title>Ο λογαριασμός σου δημιουργήθηκε</mat-card-title>
          </mat-card-header>
          <mat-card-content>
            <p class="pending__text">
              Περιμένει να τον ενεργοποιήσει ο διαχειριστής. Θα λάβεις email μόλις είναι έτοιμος και μετά μπορείς
              να συνδεθείς.
            </p>
          </mat-card-content>
          <mat-card-actions>
            <a matButton routerLink="/login">Επιστροφή στη σύνδεση</a>
          </mat-card-actions>
        </mat-card>
      </div>
    </div>
  `,
  styles: `
    .pending__text {
      margin: 0;
      color: var(--brand-muted);
    }
  `,
})
export class PendingComponent {}
