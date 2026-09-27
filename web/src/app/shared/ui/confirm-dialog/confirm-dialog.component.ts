import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';

/** What a confirmation asks. `destructive` turns the confirm button red and defaults focus to Cancel. */
export interface ConfirmDialogData {
  title: string;
  message: string;
  /** Extra line in smaller type, for a consequence worth spelling out. */
  detail?: string;
  confirmLabel?: string;
  cancelLabel?: string;
  destructive?: boolean;
}

/**
 * The one confirmation dialog in the app. It exists because `window.confirm` cannot be styled,
 * cannot be translated, blocks the whole page, and looks like a browser warning rather than part
 * of the product — and on a phone it appears at the top of the screen, nowhere near the thumb
 * that has to answer it.
 *
 * Focus moves into the dialog and is trapped by Angular Material's overlay. Escape and a backdrop
 * click both cancel. For a destructive action the initial focus is **Cancel**, so a stray Enter
 * or a double tap on the button that opened it cannot delete anything.
 */
@Component({
  selector: 'app-confirm-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="confirm" [class.confirm--destructive]="data.destructive">
      <h2 class="confirm__title" id="confirm-title">{{ data.title }}</h2>
      <p class="confirm__message">{{ data.message }}</p>
      @if (data.detail) {
        <p class="confirm__detail">{{ data.detail }}</p>
      }
      <div class="confirm__actions">
        <button
          type="button"
          class="confirm__button confirm__button--cancel"
          [disabled]="busy()"
          (click)="close(false)"
        >
          {{ data.cancelLabel ?? 'Ακύρωση' }}
        </button>
        <button
          type="button"
          class="confirm__button confirm__button--confirm"
          [disabled]="busy()"
          (click)="close(true)"
        >
          {{ busy() ? 'Περίμενε…' : (data.confirmLabel ?? 'Επιβεβαίωση') }}
        </button>
      </div>
    </div>
  `,
  styleUrl: './confirm-dialog.component.scss',
})
export class ConfirmDialogComponent {
  protected readonly data = inject<ConfirmDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject<MatDialogRef<ConfirmDialogComponent, boolean>>(MatDialogRef);

  /** Set on the first answer, so a double click or a double tap cannot answer twice. */
  protected readonly busy = signal(false);

  protected close(confirmed: boolean): void {
    if (this.busy()) {
      return;
    }

    this.busy.set(true);
    this.ref.close(confirmed);
  }
}
