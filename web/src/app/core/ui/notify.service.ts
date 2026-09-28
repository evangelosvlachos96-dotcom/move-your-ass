import { Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';

/** Greek user-facing notices. Components never talk to MatSnackBar directly. */
@Injectable({ providedIn: 'root' })
export class NotifyService {
  private readonly snackBar = inject(MatSnackBar);

  info(message: string): void {
    this.snackBar.open(message, 'OK', { duration: 4000 });
  }

  /** A confirmation that something worked. Styled apart from a plain notice. */
  success(message: string): void {
    this.snackBar.open(message, 'OK', { duration: 4000, panelClass: 'snack-success' });
  }

  error(message: string): void {
    this.snackBar.open(message, 'OK', { duration: 6000, panelClass: 'snack-error' });
  }
}
