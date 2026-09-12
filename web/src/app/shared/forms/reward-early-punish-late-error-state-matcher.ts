import { Injectable } from '@angular/core';
import { AbstractControl, FormGroupDirective, NgForm } from '@angular/forms';
import { ErrorStateMatcher } from '@angular/material/core';

/**
 * "Reward early, punish late" error display.
 *
 * - Empty control, blurred, only `required` failing → nothing. Tabbing through a form is not a
 *   mistake.
 * - Control has a value and it is malformed, blurred → error shown immediately.
 * - Form submitted → every invalid control shows its error.
 * - Once a control has shown an error it stays in "live" mode: it re-evaluates on every
 *   keystroke, so the message disappears the instant the value is fixed and comes straight
 *   back if it is broken again.
 *
 * Validation itself always runs on change (Angular default); this only decides when the result
 * is *displayed*. Provided app-wide in app.config.ts.
 */
@Injectable({ providedIn: 'root' })
export class RewardEarlyPunishLateErrorStateMatcher implements ErrorStateMatcher {
  /** Controls that have displayed an error at least once. WeakSet so destroyed forms are GC'd. */
  private readonly liveControls = new WeakSet<AbstractControl>();

  isErrorState(control: AbstractControl | null, form: FormGroupDirective | NgForm | null): boolean {
    if (!control || control.valid) {
      return false;
    }

    const show =
      this.liveControls.has(control) ||
      (form?.submitted ?? false) ||
      (control.touched && !this.isEmptyAndOnlyRequired(control));

    if (show) {
      this.liveControls.add(control);
    }

    return show;
  }

  private isEmptyAndOnlyRequired(control: AbstractControl): boolean {
    const errors = Object.keys(control.errors ?? {});
    const onlyRequired = errors.length === 1 && errors[0] === 'required';
    const value: unknown = control.value;
    const empty = value == null || value === '' || (Array.isArray(value) && value.length === 0);
    return empty && onlyRequired;
  }
}
