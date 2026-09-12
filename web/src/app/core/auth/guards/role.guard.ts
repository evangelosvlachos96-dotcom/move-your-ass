import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthStore } from '../auth.store';
import { UserRole } from '../models';

/**
 * A convenience for navigation, not the control: the API enforces roles on every request.
 * Usage: canActivate: [authGuard, roleGuard('Admin')]
 */
export function roleGuard(role: UserRole): CanActivateFn {
  return () => {
    const store = inject(AuthStore);
    const router = inject(Router);

    return store.user()?.role === role ? true : router.createUrlTree(['/dashboard']);
  };
}
