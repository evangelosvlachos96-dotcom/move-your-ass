import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, firstValueFrom, from, map, of, shareReplay, switchMap, tap } from 'rxjs';
import { ApiClient } from '../http/api-client.service';
import { handles, silent } from '../http/http-context';
import { ErrorCodes } from '../http/problem-details';
import { NotifyService } from '../ui/notify.service';
import { AuthStore } from './auth.store';
import { SessionSyncService } from './session-sync.service';
import {
  ChangePasswordRequest,
  LoginRequest,
  LoginResponse,
  RefreshResponse,
  RegisterRequest,
  RegisterResponse,
  UpdateProfileRequest,
  User,
} from './models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiClient);
  private readonly store = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly notify = inject(NotifyService);
  private readonly sessionSync = inject(SessionSyncService);

  constructor() {
    // A sign-out in any tab ends the session in all of them. Without this, a second tab keeps
    // rendering a signed-in shell until its next request fails, which looks like a bug and lets
    // somebody act on a page they no longer have access to.
    this.sessionSync.whenSignedOutElsewhere(() => {
      if (this.store.isAuthenticated() || this.store.user()) {
        this.store.clear();
        void this.router.navigate(['/login']);
      }
    });
  }

  /** The one shared refresh call while it is in flight. See refresh(). */
  private refreshInFlight$: Observable<void> | null = null;

  /** INVALID_CREDENTIALS is rendered inline by the login form, so the interceptor stays quiet for it. */
  login(credentials: LoginRequest): Observable<User> {
    return this.api
      .post<LoginResponse>('/auth/login', credentials, { context: handles(ErrorCodes.InvalidCredentials) })
      .pipe(
        tap((response) => this.store.setSession(response.accessToken, response.user)),
        map((response) => response.user),
      );
  }

  /** 202 PendingApproval. EMAIL_ALREADY_EXISTS is shown under the email field by the register form. */
  register(request: RegisterRequest): Observable<RegisterResponse> {
    return this.api.post<RegisterResponse>('/auth/register', request, {
      context: handles(ErrorCodes.EmailAlreadyExists),
    });
  }

  acceptInvitation(token: string, newPassword: string): Observable<void> {
    return this.api.post<void>('/auth/accept-invitation', { token, newPassword });
  }

  /**
   * Always succeeds as far as the caller can tell: the server answers the same whether or not
   * the address exists, so that this form cannot be used to discover who has an account.
   */
  forgotPassword(email: string): Observable<void> {
    return this.api.post<void>('/auth/forgot-password', { email });
  }

  resetPassword(token: string, newPassword: string): Observable<void> {
    return this.api.post<void>('/auth/reset-password', { token, newPassword });
  }

  /**
   * Single-flight refresh, within this tab and across tabs.
   *
   * Within the tab: every caller arriving while a refresh runs subscribes to the same observable
   * and gets the same outcome. Across tabs: SessionSyncService serialises on a Web Lock and
   * shares the resulting access token, so a second tab reuses it rather than presenting the same
   * cookie again. Two parallel refreshes would otherwise look like token reuse to the server.
   */
  refresh(): Observable<void> {
    if (!this.refreshInFlight$) {
      this.refreshInFlight$ = from(
        this.sessionSync.coordinate(async () => {
          const response = await firstValueFrom(
            this.api.post<RefreshResponse>('/auth/refresh', undefined, { context: silent() }),
          );
          return response.accessToken;
        }),
      ).pipe(
        tap((accessToken) => this.store.setAccessToken(accessToken)),
        map(() => undefined),
        finalize(() => (this.refreshInFlight$ = null)),
        shareReplay({ bufferSize: 1, refCount: false }),
      );
    }
    return this.refreshInFlight$;
  }

  me(): Observable<User> {
    return this.api.get<User>('/auth/me').pipe(tap((user) => this.store.setUser(user)));
  }

  /** Bootstrap: try to resume the session from the refresh cookie. Never throws. */
  restoreSession(): Observable<boolean> {
    return this.refresh().pipe(
      switchMap(() => this.me()),
      map(() => true),
      catchError(() => of(false)),
    );
  }

  /** CURRENT_PASSWORD_WRONG is shown under the current-password field by the form. */
  changePassword(request: ChangePasswordRequest): Observable<void> {
    return this.api.post<void>('/auth/change-password', request, {
      context: handles(ErrorCodes.CurrentPasswordWrong),
    });
  }

  /**
   * After a forced change the access token still carries `must_change_password`. One refresh
   * drops the claim (CLAUDE.md "things that will bite you"); /auth/me then refreshes the user so
   * the guard lets them through.
   */
  clearMustChangePassword(): Observable<User> {
    return this.refresh().pipe(switchMap(() => this.me()));
  }

  updateProfile(request: UpdateProfileRequest): Observable<void> {
    return this.api.put<void>('/auth/profile', request).pipe(
      tap(() => {
        const user = this.store.user();
        if (user) {
          this.store.setUser({ ...user, ...request });
        }
      }),
    );
  }

  logout(): Observable<void> {
    return this.api.post<void>('/auth/logout', undefined, { context: silent() }).pipe(
      catchError(() => of(undefined)),
      finalize(() => this.forceLogout()),
    );
  }

  /**
   * Ends the session server-side without leaving the current page. The refresh cookie is revoked
   * by the endpoint, so a token page cannot be completed while an old session is still alive.
   */
  logoutQuietly(): Observable<void> {
    return this.api.post<void>('/auth/logout', undefined, { context: silent() }).pipe(
      catchError(() => of(undefined)),
      map(() => undefined),
      finalize(() => this.clearSession()),
    );
  }

  /** Drops the session locally and returns to the login page, optionally telling the user why. */
  forceLogout(message?: string): void {
    const wasAuthenticated = this.store.isAuthenticated();
    this.clearSession();
    if (message && wasAuthenticated) {
      this.notify.error(message);
    }
    void this.router.navigate(['/login']);
  }

  /**
   * Drops the session without navigating anywhere.
   *
   * Used where the destination is already decided — the password-token pages sign the previous
   * user out and then show themselves, and sending them to /login first would defeat the point.
   */
  clearSession(): void {
    this.store.clear();
    this.sessionSync.announceSignOut();
  }
}
