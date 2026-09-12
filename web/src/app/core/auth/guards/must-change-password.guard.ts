import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthStore } from '../auth.store';

/**
 * Keeps a user with a temporary password on the change-password screen. A convenience only:
 * the API returns 403 MUST_CHANGE_PASSWORD on everything else regardless (CLAUDE.md rule 4).
 */
export const mustChangePasswordGuard: CanActivateFn = () => {
  const store = inject(AuthStore);
  const router = inject(Router);

  return store.mustChangePassword() ? router.createUrlTree(['/change-password']) : true;
};
