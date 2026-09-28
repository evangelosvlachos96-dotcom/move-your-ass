import { inject } from '@angular/core';
import { CanActivateFn } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { AuthService } from '../auth.service';
import { AuthStore } from '../auth.store';
import { TokenPageNotice } from '../token-page-notice.service';

/**
 * Ends any active session before a password-token page is shown.
 *
 * The bug this exists for: an admin invites a client, opens the invitation link in the same
 * browser, sets the client's password, presses "log in" — and lands in the **admin's** dashboard,
 * because the admin's refresh cookie was still sitting there the whole time. The new account's
 * password had been set, but the browser was still somebody else.
 *
 * Signing out first is the fix, and it has to be a real sign-out: calling the endpoint so the
 * refresh token is revoked server-side, not merely dropping the token held in memory. Other tabs
 * find out through the same channel any logout uses.
 *
 * A failed call still clears local state and lets the page through. Someone holding a valid
 * reset link must never be stuck behind a network error, and the worst case is a cookie that the
 * server will reject anyway.
 */
export const signOutForTokenGuard: CanActivateFn = () => {
  const store = inject(AuthStore);
  const auth = inject(AuthService);
  const notice = inject(TokenPageNotice);

  if (!store.isAuthenticated() && !store.user()) {
    return true;
  }

  notice.raise();
  return auth.logoutQuietly().pipe(
    map(() => true),
    catchError(() => {
      auth.clearSession();
      return of(true);
    }),
  );
};
