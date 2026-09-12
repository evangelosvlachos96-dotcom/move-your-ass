import { FormGroup } from '@angular/forms';

/**
 * Moves keyboard focus to the first invalid control of `form`, in declaration order, looking the
 * matching element up by its `formControlName` inside `host`. Call after `markAllAsTouched()` on
 * a rejected submit so the user lands on the field that needs attention.
 */
export function focusFirstInvalid(form: FormGroup, host: HTMLElement): void {
  for (const [name, control] of Object.entries(form.controls)) {
    if (!control.invalid) {
      continue;
    }
    host.querySelector<HTMLElement>(`[formControlName="${name}"]`)?.focus();
    return;
  }
}
