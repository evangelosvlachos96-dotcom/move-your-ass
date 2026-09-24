import { UserRole, UserStatus } from '../auth/models';

/** UserDto as the admin endpoints return it. Dates are UTC ISO strings; convert in the UI only. */
export interface AdminUser {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  role: UserRole;
  status: UserStatus;
  mustChangePassword: boolean;
  createdAtUtc: string;
  approvedAtUtc: string | null;
  suspendedAtUtc: string | null;
  suspensionReason: string | null;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface UserListQuery {
  status?: UserStatus;
  search?: string;
  page: number;
  pageSize: number;
}

export interface CreateUserRequest {
  firstName: string;
  lastName: string;
  email: string;
}

/** Creation queues an invitation; credentials are never returned to the admin. */
export interface CreateUserResponse {
  id: string;
}

export interface UpdateUserRequest {
  firstName: string;
  lastName: string;
  role: UserRole;
}

export interface ResetPasswordResponse {
  temporaryPassword: string;
}

export const USER_STATUSES: readonly UserStatus[] = ['PendingApproval', 'Active', 'Suspended', 'Declined', 'Invited'];

export const USER_STATUS_LABELS: Record<UserStatus, string> = {
  Invited: 'Πρόσκληση εστάλη',
  PendingApproval: 'Σε αναμονή',
  Active: 'Ενεργός',
  Suspended: 'Σε αναστολή',
  Declined: 'Απορρίφθηκε',
};

export function fullName(user: Pick<AdminUser, 'firstName' | 'lastName'>): string {
  return `${user.firstName} ${user.lastName}`.trim();
}
