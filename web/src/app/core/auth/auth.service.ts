import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, map, of, shareReplay, switchMap, tap } from 'rxjs';
import { ApiClient } from '../http/api-client.service';
import { handles, silent } from '../http/http-context';
import { ErrorCodes } from '../http/problem-details';
import { NotifyService } from '../ui/notify.service';
import { AuthStore } from './auth.store';
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

  /**
   * Single-flight refresh. Every caller that arrives while a refresh is running subscribes to
   * the same observable and gets the same outcome. Two parallel refreshes would present the
   * same cookie twice and trip the backend's reuse detection, which revokes the whole token
   * family and logs the user out at random.
   */
  refresh(): Observable<void> {
    if (!this.refreshInFlight$) {
      this.refreshInFlight$ = this.api
        .post<RefreshResponse>('/auth/refresh', undefined, { context: silent() })
        .pipe(
          tap((response) => this.store.setAccessToken(response.accessToken)),
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

  /** Drops the session locally and returns to the login page, optionally telling the user why. */
  forceLogout(message?: string): void {
    const wasAuthenticated = this.store.isAuthenticated();
    this.store.clear();
    if (message && wasAuthenticated) {
      this.notify.error(message);
    }
    void this.router.navigate(['/login']);
  }
}
