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
    if (!control) {
      return false;
    }

    const errors = this.errorKeys(control);
    if (errors.length === 0) {
      return false;
    }

    const emptyAndOnlyRequired = errors.length === 1 && errors[0] === 'required' && isEmpty(control.value);
    const show = this.liveControls.has(control) || (form?.submitted ?? false) || (control.touched && !emptyAndOnlyRequired);

    if (show) {
      this.liveControls.add(control);
    }

    return show;
  }

  /** The error keys that count against this control. Subclasses may add cross-field errors. */
  protected errorKeys(control: AbstractControl): string[] {
    return Object.keys(control.errors ?? {});
  }
}

/**
 * Same rules, but the listed errors on the parent group also count against this control — a
 * confirm-password field showing the group's `passwordMismatch`, for example. Not injectable:
 * create one per field and bind it with `[errorStateMatcher]`.
 */
export class CrossFieldErrorStateMatcher extends RewardEarlyPunishLateErrorStateMatcher {
  constructor(private readonly parentErrorKeys: readonly string[]) {
    super();
  }

  protected override errorKeys(control: AbstractControl): string[] {
    const parent = control.parent;
    const inherited = parent ? this.parentErrorKeys.filter((key) => parent.hasError(key)) : [];
    return [...super.errorKeys(control), ...inherited];
  }
}

function isEmpty(value: unknown): boolean {
  return value == null || value === '' || (Array.isArray(value) && value.length === 0);
}
