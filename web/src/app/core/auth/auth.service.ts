import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, map, of, shareReplay, switchMap, tap } from 'rxjs';
import { ApiClient } from '../http/api-client.service';
import { silent } from '../http/http-context';
import { NotifyService } from '../ui/notify.service';
import { AuthStore } from './auth.store';
import { LoginRequest, LoginResponse, RefreshResponse, User } from './models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiClient);
  private readonly store = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly notify = inject(NotifyService);

  /** The one shared refresh call while it is in flight. See refresh(). */
  private refreshInFlight$: Observable<void> | null = null;

  login(credentials: LoginRequest): Observable<User> {
    return this.api.post<LoginResponse>('/auth/login', credentials).pipe(
      tap((response) => this.store.setSession(response.accessToken, response.user)),
      map((response) => response.user),
    );
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
