import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../../auth/auth.service';
import { AuthStore } from '../../auth/auth.store';
import { ErrorCodes, problemCode } from '../problem-details';

/** Endpoints whose 401 means "the credentials themselves were rejected", never "refresh and retry". */
const NO_RETRY_PATHS = ['/auth/login', '/auth/refresh', '/auth/register'];

/**
 * Attaches the bearer token. On a 401 for an authenticated request it runs the single-flight
 * refresh (AuthService.refresh) and replays the request with the new token. Innermost
 * interceptor, so the error interceptor only ever sees 401s that survived a refresh attempt.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const store = inject(AuthStore);
  const auth = inject(AuthService);

  return next(withBearer(req, store.accessToken())).pipe(
    catchError((error: unknown) => {
      if (!shouldRefresh(error, req, store)) {
        return throwError(() => error);
      }

      return auth.refresh().pipe(
        catchError((refreshError: unknown) => {
          // The refresh itself failed: the session is gone. Account-state codes are already
          // reported by the error interceptor on the refresh call; only the plain expiry needs words.
          const code = problemCode(refreshError);
          const explained =
            code === ErrorCodes.SessionSuperseded ||
            code === ErrorCodes.AccountSuspended ||
            code === ErrorCodes.AccountDeclined ||
            code === ErrorCodes.AccountPending;
          auth.forceLogout(explained ? undefined : 'Η σύνδεση έληξε. Συνδέσου ξανά.');
          return throwError(() => error);
        }),
        switchMap(() => next(withBearer(req, store.accessToken()))),
      );
    }),
  );
};

function withBearer(req: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  return token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
}

function shouldRefresh(error: unknown, req: HttpRequest<unknown>, store: AuthStore): boolean {
  return (
    error instanceof HttpErrorResponse &&
    error.status === 401 &&
    store.isAuthenticated() &&
    !NO_RETRY_PATHS.some((path) => req.url.includes(path))
  );
}
