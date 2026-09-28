import { Routes } from '@angular/router';
import { roleGuard } from './core/auth/guards/role.guard';
import { authGuard } from './core/auth/guards/auth.guard';
import { guestGuard } from './core/auth/guards/guest.guard';
import { mustChangePasswordGuard } from './core/auth/guards/must-change-password.guard';
import { signOutForTokenGuard } from './core/auth/guards/sign-out-for-token.guard';

export const routes: Routes = [
  {
    // Signs out whoever is currently logged in first: a token link opened in a browser that is
    // already somebody else must not finish inside that somebody else's account.
    path: 'set-password',
    canActivate: [signOutForTokenGuard],
    loadComponent: () => import('./features/auth/set-password/set-password.component').then(m => m.SetPasswordComponent),
  },
  {
    path: 'forgot-password',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/forgot-password/forgot-password.component').then((m) => m.ForgotPasswordComponent),
  },
  {
    path: 'reset-password',
    canActivate: [signOutForTokenGuard],
    loadComponent: () => import('./features/auth/forgot-password/reset-password.component').then((m) => m.ResetPasswordComponent),
  },
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'register',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/register/register.component').then((m) => m.RegisterComponent),
  },
  {
    path: 'pending',
    loadComponent: () => import('./features/auth/pending/pending.component').then((m) => m.PendingComponent),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell.component').then((m) => m.ShellComponent),
    children: [
      { path: 'about', canActivate: [mustChangePasswordGuard], loadComponent: () => import('./features/about/about.component').then(m => m.AboutComponent) },
      { path: 'videos', canActivate: [mustChangePasswordGuard], loadComponent: () => import('./features/videos/library.component').then(m => m.VideoLibraryComponent) },
      { path: 'videos/:id', canActivate: [mustChangePasswordGuard], loadComponent: () => import('./features/videos/player.component').then(m => m.VideoPlayerComponent) },
      { path: 'admin/videos', canActivate: [mustChangePasswordGuard, roleGuard('Admin')], canDeactivate: [(component: { canLeave(): Promise<boolean> }) => component.canLeave()], loadComponent: () => import('./features/videos/admin-videos.component').then(m => m.AdminVideosComponent) },
      { path: 'admin/videos/:id', data: { admin: true }, canActivate: [mustChangePasswordGuard, roleGuard('Admin')], loadComponent: () => import('./features/videos/player.component').then(m => m.VideoPlayerComponent) },
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'admin/users',
        canActivate: [mustChangePasswordGuard, roleGuard('Admin')],
        loadComponent: () => import('./features/admin/users/users.component').then(m => m.UsersComponent),
      },
      {
        path: 'dashboard',
        canActivate: [mustChangePasswordGuard],
        loadComponent: () =>
          import('./features/dashboard/dashboard.component').then((m) => m.DashboardComponent),
      },
      {
        path: 'profile',
        canActivate: [mustChangePasswordGuard],
        loadComponent: () => import('./features/auth/profile/profile.component').then((m) => m.ProfileComponent),
      },
      {
        path: 'change-password',
        loadComponent: () =>
          import('./features/auth/change-password/change-password.component').then(
            (m) => m.ChangePasswordComponent,
          ),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
