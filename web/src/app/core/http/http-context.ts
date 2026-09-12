import { HttpContext, HttpContextToken } from '@angular/common/http';

/**
 * Marks a request whose expected failures should not produce a snackbar: the silent refresh on
 * bootstrap and logout. Account-state codes (suspended, superseded) are still surfaced.
 */
export const SILENT_REQUEST = new HttpContextToken<boolean>(() => false);

export const silent = (): HttpContext => new HttpContext().set(SILENT_REQUEST, true);

/**
 * Error codes the caller renders itself, inline in a form (EMAIL_ALREADY_EXISTS under the email
 * field, CURRENT_PASSWORD_WRONG under the current-password field). The error interceptor skips
 * the snackbar for these and only these; everything else on the same request is still reported.
 */
export const HANDLED_ERROR_CODES = new HttpContextToken<readonly string[]>(() => []);

export const handles = (...codes: readonly string[]): HttpContext =>
  new HttpContext().set(HANDLED_ERROR_CODES, codes);
