import { HttpContext, HttpContextToken } from '@angular/common/http';

/**
 * Marks a request whose expected failures should not produce a snackbar: the silent refresh on
 * bootstrap and logout. Account-state codes (suspended, superseded) are still surfaced.
 */
export const SILENT_REQUEST = new HttpContextToken<boolean>(() => false);

export const silent = (): HttpContext => new HttpContext().set(SILENT_REQUEST, true);
