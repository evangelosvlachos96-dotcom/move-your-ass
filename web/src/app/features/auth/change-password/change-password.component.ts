import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { AuthService } from '../../../core/auth/auth.service';

/**
 * Placeholder target for MUST_CHANGE_PASSWORD so the interceptor and guard have somewhere to
 * send the user. The real form lands in phase 3b.
 */
@Component({
  selector: 'app-change-password',
  imports: [MatCardModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <mat-card class="change-password" appearance="outlined">
      <mat-card-header>
        <mat-card-title>Απαιτείται αλλαγή κωδικού</mat-card-title>
      </mat-card-header>
      <mat-card-content>
        <p>Συνδεθήκατε με προσωρινό κωδικό. Η φόρμα αλλαγής κωδικού έρχεται στο επόμενο βήμα.</p>
      </mat-card-content>
      <mat-card-actions>
        <button matButton type="button" (click)="logout()">Αποσύνδεση</button>
      </mat-card-actions>
    </mat-card>
  `,
  styles: `
    .change-password {
      max-width: 520px;
      border-color: var(--brand-border);
    }
  `,
})
export class ChangePasswordComponent {
  private readonly auth = inject(AuthService);

  protected logout(): void {
    this.auth.logout().subscribe();
  }
}
