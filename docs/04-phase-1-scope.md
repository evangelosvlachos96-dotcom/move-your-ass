# 04 — Phase 1 Scope

**Goal:** a deployed, working authentication system on Azure with two roles and a deliberately
plain UI. Nothing else.

**Why this first:** deployment is where projects die. Doing it on day three with 6 endpoints is
a morning's work. Doing it on day sixty with a video pipeline, a calendar, and an outbox is a
week of misery. Get the pipeline green while the app is trivial.

## 1. In scope

**Backend**
- Solution skeleton, 4 projects + 3 test projects
- EF Core model: `AppUser`, `RefreshToken`, `OutboxMessage`, `IdempotencyRecord`, `TwoFactorTicket`
- Identity, roles, seeded admin
- Register → email confirm → admin approve → login → email 2FA → access + refresh
- Admin: list users (filter by status), approve, suspend, reactivate
- Outbox + SMTP email sender (3 templates: confirm, approved, 2FA code)
- FluentValidation on all commands
- `ProblemDetails` error model with stable codes
- Health endpoint (`/health` — liveness only, does not touch the DB, so keep-alive pings stay cheap)
- ~20 tests per `docs/02` §9

**Frontend**
- Shell: toolbar, sidenav, router outlet
- `ApiClient` + 3 interceptors (auth, error, loading)
- Guards: `authGuard`, `roleGuard`, `pendingGuard`
- Pages: login, 2FA code, register, pending-approval, confirm-email, admin user list, placeholder
  client dashboard
- One Material theme, default typography, no custom design work
- ~10 tests

**Infra**
- Resource group, App Service (F1), SQL free-offer DB, Static Web App, Key Vault, App Insights
- Two GitHub Actions workflows
- Budget alert at €1

## 2. Explicitly out of scope

Booking, calendar, slots, change requests, videos, blob storage, HLS, SMS, push notifications,
password reset (phase 2), profile editing, i18n framework, dark mode, custom branding.

If a task does not appear in §1, it is not phase 1. Write it in `docs/backlog.md` and move on.

## 3. API surface

```
POST   /api/auth/register              anon    202  {status}
GET    /api/auth/confirm-email         anon    302  → SPA
POST   /api/auth/login                 anon    200  {requiresTwoFactor, ticket}
POST   /api/auth/2fa/verify            anon    200  {accessToken, expiresIn, user} + Set-Cookie
POST   /api/auth/2fa/resend            anon    204
POST   /api/auth/refresh               cookie  200  {accessToken, expiresIn}
POST   /api/auth/logout                auth    204  revokes refresh + clears ActiveSessionId
GET    /api/auth/me                    auth    200  {id, fullName, email, role, status}

GET    /api/admin/users                Admin   200  paged, ?status=&search=
POST   /api/admin/users/{id}/approve   Admin   204
POST   /api/admin/users/{id}/suspend   Admin   204
POST   /api/admin/users/{id}/reactivate Admin  204

GET    /health                         anon    200
```

Ten endpoints. If this list grows during phase 1, something has gone wrong.

## 4. Phase 1 data model

```sql
AspNetUsers (Identity defaults) plus:
  FullName                    nvarchar(120)   NOT NULL
  Status                      int             NOT NULL  -- 0 Pending, 1 Active, 2 Suspended
  CreatedAtUtc                datetime2(3)    NOT NULL
  ApprovedAtUtc               datetime2(3)    NULL
  ApprovedByUserId            nvarchar(450)   NULL
  SuspendedAtUtc              datetime2(3)    NULL
  SuspensionReason            nvarchar(500)   NULL
  ActiveSessionId             uniqueidentifier NULL
  ActiveSessionStartedAtUtc   datetime2(3)    NULL
  ActiveSessionUserAgent      nvarchar(256)   NULL

RefreshToken
  Id                uniqueidentifier PK
  UserId            nvarchar(450)    FK → AspNetUsers
  TokenHash         binary(32)       NOT NULL     -- SHA-256, never the raw token
  SessionId         uniqueidentifier NOT NULL
  FamilyId          uniqueidentifier NOT NULL     -- rotation chain, for reuse detection
  CreatedAtUtc      datetime2(3)     NOT NULL
  ExpiresAtUtc      datetime2(3)     NOT NULL
  RevokedAtUtc      datetime2(3)     NULL
  ReplacedByTokenId uniqueidentifier NULL
  CreatedByIp       nvarchar(45)     NULL

TwoFactorTicket
  Id            uniqueidentifier PK
  UserId        nvarchar(450)    FK
  CodeHash      binary(32)       NOT NULL
  ExpiresAtUtc  datetime2(3)     NOT NULL
  Attempts      int              NOT NULL DEFAULT 0
  ConsumedAtUtc datetime2(3)     NULL

OutboxMessage
  Id, Type, PayloadJson, CreatedAtUtc, ProcessedAtUtc, Attempts, LastError, LockedUntilUtc

IdempotencyRecord
  Id, UserId, [Key] nvarchar(100), StatusCode, ResponseJson, CreatedAtUtc, ExpiresAtUtc
```

```sql
CREATE UNIQUE INDEX UX_Idempotency_User_Key ON IdempotencyRecord(UserId, [Key]);
CREATE INDEX IX_RefreshToken_UserActive ON RefreshToken(UserId) WHERE RevokedAtUtc IS NULL;
CREATE INDEX IX_RefreshToken_Family      ON RefreshToken(FamilyId);
CREATE INDEX IX_Outbox_Pending           ON OutboxMessage(CreatedAtUtc) WHERE ProcessedAtUtc IS NULL;
CREATE INDEX IX_User_Status              ON AspNetUsers(Status);
```

`Slot`, `Booking`, `ChangeRequest`, and `Video` are **not** created in phase 1. They arrive with
their own migrations when their feature is built.

## 5. UI for phase 1

Deliberately plain. One primary colour, Material defaults everywhere else.

```scss
// styles/_theme.scss
@use '@angular/material' as mat;

html {
  @include mat.theme((
    color:      (primary: mat.$azure-palette, tertiary: mat.$blue-palette),
    typography: Roboto,
    density:    0,
  ));
}
```

Layout rules:
- `mat-toolbar` (app name + user menu) + `mat-sidenav`
- `< 960px` → sidenav `mode="over"`, closed by default; `≥ 960px` → `mode="side"`, open
- Forms: `mat-card`, `max-width: 420px`, centred, `mat-form-field appearance="outline"`
- Every page has a loading state, an empty state, and an error state. All three.
- Greek copy throughout, hardcoded for now. Extract to i18n in a later phase.

No logo, no illustrations, no custom components, no animations beyond Material's defaults.
Aesthetics come after the system works.

## 6. Acceptance criteria

Phase 1 is done when, **against the deployed Azure environment**, all of these pass:

1. A new visitor registers and sees "Αναμένεται επικοινωνία από τον διαχειριστή."
2. They receive a confirmation email and the link activates `EmailConfirmed`.
3. Attempting to log in before approval returns 403 `ACCOUNT_PENDING` and the SPA shows the
   pending page, not a generic error.
4. The admin sees the user in the pending list and approves them.
5. The user receives an approval email.
6. The user logs in, receives a 6-digit code by email, enters it, and lands on the dashboard.
7. Reloading the page keeps them logged in (silent refresh via cookie).
8. The access token is not present in `localStorage` or `sessionStorage`.
9. Logging in on a second browser invalidates the first session within 15 minutes.
10. A Client navigating to `/admin` is redirected, and `GET /api/admin/users` returns 403.
11. The admin suspends the user; within 15 minutes the user is logged out and cannot log back in.
12. The app is reachable over HTTPS on the SWA URL and the API rejects requests from other origins.
13. `dotnet test` and `npm run test` are green in CI.
14. Cold-start latency after 30 minutes idle is measured and written down in this doc.

Item 14 matters: it tells you whether you can ship on F1 or need to spend the €12 on B1.

## 7. Build order

| # | Step | Done when |
|---|---|---|
| 1 | Repo, `.gitignore`, `.editorconfig`, `CLAUDE.md`, `docs/` | pushed |
| 2 | Solution + 4 projects + DI wiring + `/health` | `dotnet run` returns 200 |
| 3 | `NetArchTest` layering tests | green, and they fail if you break a rule |
| 4 | EF model + migration + seeder (roles + admin) | `database update` succeeds locally |
| 5 | **Deploy the skeleton to Azure** | `/health` reachable over HTTPS on App Service |
| 6 | `ApiClient` + interceptors + shell + login page (mocked) | SPA deployed to SWA, calls `/health` |
| 7 | Register + confirm email + outbox + SMTP | email lands in a real inbox |
| 8 | Login + 2FA + tokens + refresh rotation | criteria 6 & 7 pass locally |
| 9 | Session control (`ActiveSessionId`) | criterion 9 passes |
| 10 | Admin list / approve / suspend | criteria 4, 10, 11 pass |
| 11 | Guards, error interceptor mapping, Greek copy | criterion 3 passes |
| 12 | Tests to the §9 list in `docs/02` | green in CI |
| 13 | Re-run all 14 acceptance criteria against Azure | phase 1 closed |

**Step 5 is the point of this whole phase.** Deploy something that does almost nothing, as early
as possible. Everything after it is incremental.

## 8. Working with Claude Code on this

Suggested session boundaries — one per step above, each a branch and a PR:

```
feat/01-scaffold
feat/02-ef-model
chore/03-azure-infra
feat/04-registration
feat/05-login-2fa
feat/06-sessions
feat/07-admin-users
feat/08-frontend-shell
```

Start each session by pointing at `CLAUDE.md` and the relevant `docs/` file. Keep the diffs
small enough to actually review — the value of the design docs evaporates if a 4,000-line PR
lands that quietly ignores them.
