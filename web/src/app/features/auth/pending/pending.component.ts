import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { RouterLink } from '@angular/router';

/** Landing for ACCOUNT_PENDING. The register flow that leads here arrives in phase 3b. */
@Component({
  selector: 'app-pending',
  imports: [MatCardModule, MatButtonModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="pending">
      <mat-card class="pending__card" appearance="outlined">
        <mat-card-header>
          <mat-card-title>Ο λογαριασμός σας εκκρεμεί</mat-card-title>
        </mat-card-header>
        <mat-card-content>
          <p>Αναμένεται επικοινωνία από τον διαχειριστή. Θα λάβετε email μόλις εγκριθεί η εγγραφή σας.</p>
        </mat-card-content>
        <mat-card-actions>
          <a matButton routerLink="/login">Επιστροφή στη σύνδεση</a>
        </mat-card-actions>
      </mat-card>
    </div>
  `,
  styles: `
    .pending {
      min-height: 100dvh;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 24px;
      box-sizing: border-box;
    }
    .pending__card {
      max-width: 420px;
      width: 100%;
      border-color: var(--brand-border);
    }
  `,
})
export class PendingComponent {}
