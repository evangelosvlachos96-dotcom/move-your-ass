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
