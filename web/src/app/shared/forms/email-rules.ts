import { ValidatorFn, Validators } from '@angular/forms';
/** Public email addresses require a non-empty dotted domain, in addition to Angular syntax checks. */
export const emailAddress: ValidatorFn = (control) => {
  if (!control.value) return null;
  return (
    Validators.email(control) ||
    (/^[^\s@]+@[^\s@.]+(?:\.[^\s@.]+)+$/.test(control.value) ? null : { email: true })
  );
};
