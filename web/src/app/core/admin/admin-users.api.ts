import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../http/api-client.service';
import { handles } from '../http/http-context';
import { ErrorCodes } from '../http/problem-details';
import {
  AdminUser,
  CreateUserRequest,
  CreateUserResponse,
  PagedResult,
  ResetPasswordResponse,
  UpdateUserRequest,
  UserListQuery,
} from './models';

/**
 * /api/admin/users. Approve and decline claim USER_NOT_PENDING so the list can explain a stale
 * view itself instead of the generic snackbar.
 */
@Injectable({ providedIn: 'root' })
export class AdminUsersApi {
  private readonly api = inject(ApiClient);

  list(query: UserListQuery): Observable<PagedResult<AdminUser>> {
    const params: Record<string, string | number> = { page: query.page, pageSize: query.pageSize };
    if (query.status) {
      params['status'] = query.status;
    }
    if (query.search) {
      params['search'] = query.search;
    }
    return this.api.get<PagedResult<AdminUser>>('/admin/users', { params });
  }

  create(request: CreateUserRequest): Observable<CreateUserResponse> {
    return this.api.post<CreateUserResponse>('/admin/users', request, {
      context: handles(ErrorCodes.EmailAlreadyExists),
    });
  }

  update(id: string, request: UpdateUserRequest): Observable<void> {
    return this.api.put<void>(`/admin/users/${id}`, request);
  }

  approve(id: string): Observable<AdminUser> {
    return this.api.post<AdminUser>(`/admin/users/${id}/approve`, undefined, {
      context: handles(ErrorCodes.UserNotPending),
    });
  }

  decline(id: string, reason: string | null): Observable<AdminUser> {
    return this.api.post<AdminUser>(`/admin/users/${id}/decline`, { reason }, {
      context: handles(ErrorCodes.UserNotPending),
    });
  }

  suspend(id: string, reason: string | null): Observable<AdminUser> {
    return this.api.post<AdminUser>(`/admin/users/${id}/suspend`, { reason });
  }

  reactivate(id: string): Observable<AdminUser> {
    return this.api.post<AdminUser>(`/admin/users/${id}/reactivate`);
  }

  resetPassword(id: string): Observable<ResetPasswordResponse> {
    return this.api.post<ResetPasswordResponse>(`/admin/users/${id}/reset-password`);
  }

  resendInvitation(id: string): Observable<void> {
    return this.api.post<void>(`/admin/users/${id}/resend-invitation`);
  }

  delete(id: string): Observable<void> {
    return this.api.delete<void>(`/admin/users/${id}`);
  }
}
