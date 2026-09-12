export type UserRole = 'Admin' | 'Client';

export type UserStatus = 'PendingApproval' | 'Active' | 'Suspended' | 'Declined';

/** GET /auth/me and the `user` part of the login response. */
export interface User {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  role: UserRole;
  status: UserStatus;
  mustChangePassword: boolean;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  expiresIn: number;
  user: User;
}

export interface RefreshResponse {
  accessToken: string;
  expiresIn: number;
}

export interface RegisterRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
}

/** 202: nothing to log into yet. The admin has been notified and must approve first. */
export interface RegisterResponse {
  status: UserStatus;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface UpdateProfileRequest {
  firstName: string;
  lastName: string;
}
