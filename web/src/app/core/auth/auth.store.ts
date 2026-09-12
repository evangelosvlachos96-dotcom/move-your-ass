import { Injectable, computed, signal } from '@angular/core';
import { User } from './models';

/**
 * Session state as signals. The access token lives in memory only — never localStorage or
 * sessionStorage (ADR-004). A reload gets it back through the silent refresh, not from storage.
 */
@Injectable({ providedIn: 'root' })
export class AuthStore {
  private readonly _user = signal<User | null>(null);
  private readonly _accessToken = signal<string | null>(null);

  readonly user = this._user.asReadonly();
  readonly accessToken = this._accessToken.asReadonly();

  readonly isAuthenticated = computed(() => this._accessToken() !== null);
  readonly isAdmin = computed(() => this._user()?.role === 'Admin');
  readonly mustChangePassword = computed(() => this._user()?.mustChangePassword === true);
  readonly fullName = computed(() => {
    const user = this._user();
    return user ? `${user.firstName} ${user.lastName}`.trim() : '';
  });

  setSession(accessToken: string, user: User): void {
    this._accessToken.set(accessToken);
    this._user.set(user);
  }

  setAccessToken(accessToken: string): void {
    this._accessToken.set(accessToken);
  }

  setUser(user: User): void {
    this._user.set(user);
  }

  clear(): void {
    this._accessToken.set(null);
    this._user.set(null);
  }
}
