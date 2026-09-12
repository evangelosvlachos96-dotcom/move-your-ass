import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { ErrorStateMatcher } from '@angular/material/core';
import { MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';
import { provideRouter, withDisabledInitialNavigation } from '@angular/router';
import { routes } from './app.routes';
import { authInterceptor } from './core/http/interceptors/auth.interceptor';
import { errorInterceptor } from './core/http/interceptors/error.interceptor';
import { loadingInterceptor } from './core/http/interceptors/loading.interceptor';
import { RewardEarlyPunishLateErrorStateMatcher } from './shared/forms/reward-early-punish-late-error-state-matcher';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),

    // The root component runs the silent /auth/refresh behind the boot loader and then calls
    // router.initialNavigation() itself, so guards see the restored session.
    provideRouter(routes, withDisabledInitialNavigation()),

    // Order matters: auth is innermost so it sees a 401 first and can refresh + replay before
    // the error interceptor gets a chance to report it.
    provideHttpClient(withInterceptors([loadingInterceptor, errorInterceptor, authInterceptor])),

    { provide: MAT_FORM_FIELD_DEFAULT_OPTIONS, useValue: { appearance: 'outline' } },

    // Errors appear on blur only when there is a wrong value, on submit for everything, then
    // live until fixed. See the matcher for the full rule.
    { provide: ErrorStateMatcher, useExisting: RewardEarlyPunishLateErrorStateMatcher },
  ],
};
