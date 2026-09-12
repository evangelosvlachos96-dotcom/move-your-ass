import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../../auth/auth.service';
import { AuthStore } from '../../auth/auth.store';
import { NotifyService } from '../../ui/notify.service';
import { SILENT_REQUEST } from '../http-context';
import { ErrorCodes, problemCode } from '../problem-details';

/**
 * Maps the ProblemDetails `code` to behaviour. Never looks at message text: titles change and
 * get translated, codes do not.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const notify = inject(NotifyService);
  const auth = inject(AuthService);
  const store = inject(AuthStore);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) {
        handle(error, req.context.get(SILENT_REQUEST), { router, notify, auth, store });
      }
      return throwError(() => error);
    }),
  );
};

interface Deps {
  router: Router;
  notify: NotifyService;
  auth: AuthService;
  store: AuthStore;
}

function handle(error: HttpErrorResponse, isSilent: boolean, { router, notify, auth, store }: Deps): void {
  switch (problemCode(error)) {
    case ErrorCodes.AccountPending:
      void router.navigateByUrl('/pending');
      return;

    case ErrorCodes.AccountSuspended:
      auth.forceLogout();
      notify.error('Ο λογαριασμός σας έχει ανασταλεί. Επικοινωνήστε με τον διαχειριστή.');
      return;

    case ErrorCodes.AccountDeclined:
      notify.error('Η εγγραφή σας δεν έγινε δεκτή.');
      return;

    case ErrorCodes.MustChangePassword:
      void router.navigateByUrl('/change-password');
      return;

    case ErrorCodes.SessionSuperseded:
      auth.forceLogout();
      notify.error('Έγινε αποσύνδεση από την άλλη συσκευή.');
      return;

    default:
      if (isSilent) {
        return;
      }
      // A 401 after the session was dropped is already explained by the auth interceptor.
      if (error.status === 401 && !store.isAuthenticated()) {
        return;
      }
      notify.error(messageFor(error));
  }
}

function messageFor(error: HttpErrorResponse): string {
  switch (problemCode(error)) {
    case ErrorCodes.InvalidCredentials:
      return 'Λάθος email ή κωδικός.';
    case ErrorCodes.CurrentPasswordWrong:
      return 'Ο τρέχων κωδικός δεν είναι σωστός.';
    case ErrorCodes.EmailAlreadyExists:
      return 'Το email χρησιμοποιείται ήδη.';
    case ErrorCodes.ValidationFailed:
      return 'Ελέγξτε τα στοιχεία που συμπληρώσατε.';
    case ErrorCodes.RateLimited:
      return 'Πολλές προσπάθειες. Δοκιμάστε ξανά σε λίγο.';
    case ErrorCodes.Forbidden:
      return 'Δεν έχετε δικαίωμα για αυτή την ενέργεια.';
    case ErrorCodes.Unauthenticated:
      return 'Απαιτείται σύνδεση.';
    case ErrorCodes.UserNotFound:
      return 'Ο χρήστης δεν βρέθηκε.';
    case ErrorCodes.UserNotPending:
    case ErrorCodes.CannotDeleteSelf:
    case ErrorCodes.CannotModifySelf:
    case ErrorCodes.CannotDeleteLastAdmin:
      return 'Η ενέργεια δεν επιτρέπεται.';
    default:
      return error.status === 0
        ? 'Δεν υπάρχει σύνδεση με τον διακομιστή.'
        : 'Κάτι πήγε στραβά. Δοκιμάστε ξανά.';
  }
}
