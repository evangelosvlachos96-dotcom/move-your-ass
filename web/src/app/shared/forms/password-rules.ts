import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * Mirrors the API password policy (ValidationRules.Password / IdentityOptionsSetup): 10+
 * characters, an ASCII uppercase, an ASCII lowercase, a digit. Kept ASCII on purpose so the
 * client never accepts a password the server would reject.
 */
export interface PasswordRule {
  key: string;
  label: string;
  test: (value: string) => boolean;
}

export const PASSWORD_RULES: readonly PasswordRule[] = [
  { key: 'minLength', label: 'Τουλάχιστον 10 χαρακτήρες', test: (value) => value.length >= 10 },
  { key: 'uppercase', label: 'Ένα κεφαλαίο γράμμα (A-Z)', test: (value) => /[A-Z]/.test(value) },
  { key: 'lowercase', label: 'Ένα πεζό γράμμα (a-z)', test: (value) => /[a-z]/.test(value) },
  { key: 'digit', label: 'Έναν αριθμό (0-9)', test: (value) => /[0-9]/.test(value) },
];

export const PASSWORD_MAX_LENGTH = 128;

/** `{ passwordPolicy: string[] }` listing the failed rule keys. Empty values are left to `required`. */
export function passwordPolicy(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value: unknown = control.value;
    if (typeof value !== 'string' || value === '') {
      return null;
    }
    const failed = PASSWORD_RULES.filter((rule) => !rule.test(value)).map((rule) => rule.key);
    return failed.length > 0 ? { passwordPolicy: failed } : null;
  };
}

/**
 * Form-level validator: `{ passwordMismatch: true }` when both fields have a value and differ.
 * An empty confirm field is the confirm field's own `required` problem, not a mismatch.
 */
export function passwordsMatch(passwordKey: string, confirmKey: string): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    const password: unknown = group.get(passwordKey)?.value;
    const confirm: unknown = group.get(confirmKey)?.value;
    if (!password || !confirm) {
      return null;
    }
    return password === confirm ? null : { passwordMismatch: true };
  };
}
