import { FormGroup } from '@angular/forms';

/**
 * Moves keyboard focus to the first invalid control of `form`, in declaration order, looking the
 * matching element up by its `formControlName` inside `host`. Call after `markAllAsTouched()` on
 * a rejected submit so the user lands on the field that needs attention.
 *
 * Returns false when no control is individually invalid (only a form-level validator failed),
 * so the caller can pick the field that error belongs to.
 */
export function focusFirstInvalid(form: FormGroup, host: HTMLElement): boolean {
  for (const [name, control] of Object.entries(form.controls)) {
    if (!control.invalid) {
      continue;
    }
    focusControl(host, name);
    return true;
  }
  return false;
}

export function focusControl(host: HTMLElement, name: string): void {
  host.querySelector<HTMLElement>(`[formControlName="${name}"]`)?.focus();
}
