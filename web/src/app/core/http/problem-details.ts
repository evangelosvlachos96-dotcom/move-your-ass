import { HttpErrorResponse } from '@angular/common/http';

/** RFC 7807 body as the API emits it, plus the stable `code` extension. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  code?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}

/** Mirrors ErrorCodes in Mya.Application. The interceptor switches on these, never on text. */
export const ErrorCodes = {
  AccountPending: 'ACCOUNT_PENDING',
  AccountDeclined: 'ACCOUNT_DECLINED',
  AccountSuspended: 'ACCOUNT_SUSPENDED',
  InvalidCredentials: 'INVALID_CREDENTIALS',
  SessionSuperseded: 'SESSION_SUPERSEDED',
  MustChangePassword: 'MUST_CHANGE_PASSWORD',
  CurrentPasswordWrong: 'CURRENT_PASSWORD_WRONG',
  EmailAlreadyExists: 'EMAIL_ALREADY_EXISTS',
  UserNotFound: 'USER_NOT_FOUND',
  UserNotPending: 'USER_NOT_PENDING',
  CannotDeleteSelf: 'CANNOT_DELETE_SELF',
  CannotModifySelf: 'CANNOT_MODIFY_SELF',
  CannotDeleteLastAdmin: 'CANNOT_DELETE_LAST_ADMIN',
  Unauthenticated: 'UNAUTHENTICATED',
  Forbidden: 'FORBIDDEN',
  ValidationFailed: 'VALIDATION_FAILED',
  RateLimited: 'RATE_LIMITED',
  InternalError: 'INTERNAL_ERROR',
} as const;

export type ErrorCode = (typeof ErrorCodes)[keyof typeof ErrorCodes];

export function problemOf(error: unknown): ProblemDetails | null {
  if (error instanceof HttpErrorResponse && error.error && typeof error.error === 'object') {
    return error.error as ProblemDetails;
  }
  return null;
}

export function problemCode(error: unknown): string | undefined {
  const code = problemOf(error)?.code;
  return typeof code === 'string' ? code : undefined;
}
