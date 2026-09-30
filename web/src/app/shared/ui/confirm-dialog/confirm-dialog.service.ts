import { Injectable, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { firstValueFrom } from 'rxjs';
import { ConfirmDialogComponent, ConfirmDialogData } from './confirm-dialog.component';

/**
 * Asks the user to confirm something, in the app's own voice.
 *
 * Replaces `window.confirm` everywhere. Awaiting a promise reads the same as the old blocking
 * call at the call site, so adopting it did not reshape the handlers that use it.
 */
@Injectable({ providedIn: 'root' })
export class ConfirmDialogService {
  private readonly dialog = inject(MatDialog);

  async confirm(data: ConfirmDialogData): Promise<boolean> {
    const phone = this.isPhone();
    const ref = this.dialog.open<ConfirmDialogComponent, ConfirmDialogData, boolean>(
      ConfirmDialogComponent,
      {
        data,
        panelClass: 'confirm-panel',
        // Phones get a bottom sheet within thumb reach; larger screens get a centred card.
        position: phone ? { bottom: '0' } : undefined,
        // 100% of the overlay, not 100vw: on iOS with viewport-fit=cover, 100vw is the full
        // screen including the safe-area insets, so a "full width" sheet is wider than the space
        // it is allowed to occupy and the page gains a horizontal scrollbar behind it.
        width: phone ? '100%' : '440px',
        maxWidth: '100%',
        // Cancel is first in the DOM. For a destructive action that is exactly where focus
        // should land, so a stray Enter cannot delete anything; otherwise focus the confirm.
        autoFocus: data.destructive ? 'first-tabbable' : '.confirm__button--confirm',
        restoreFocus: true,
        ariaLabelledBy: 'confirm-title',
      },
    );

    return (await firstValueFrom(ref.afterClosed())) === true;
  }

  /**
   * Guarded: a few embedded webviews have no `matchMedia`, and a confirmation that throws is a
   * confirmation nobody can answer. Falling back to the desktop layout is the safe direction.
   */
  private isPhone(): boolean {
    try {
      return typeof window.matchMedia === 'function' && window.matchMedia('(max-width: 599px)').matches;
    } catch {
      return false;
    }
  }
}
