# 04 — Roadmap

Replaces the earlier phase-1 scope document, which was written for the booking product.
Delete `docs/04-phase-1-scope.md` when this lands.

---

## What changed

Session booking is dropped. The product is a private video library with two roles. See
`docs/06-video-catalogue.md` for the catalogue design.

Consequences:

- `docs/03` §5.1 (atomic slot `UPDATE`), §5.4 and the 5-booking cap no longer apply
- ADR-006 and ADR-007 are withdrawn — no booking means no slot race and no double-submit risk
  worth an idempotency table
- ADR-008 is superseded: video hosting moves from Blob + hand-encoded HLS to Bunny Stream,
  because the trainer uploads from a phone and will never run ffmpeg
- Auth, roles, and admin approval are unchanged and still the foundation

New decisions recorded here:

- **ADR-013 — email 2FA deferred.** It doubles the login surface and couples the first deploy to
  a working mail provider. `TwoFactorTicket` stays in the schema, so enabling it later is code
  only. Cost: password-only auth until it returns.
- **ADR-014 — automated tests deferred beyond the architecture tests.** Deliberate, to reach a
  deployed product faster. Cost: regressions are found by the trainer in production rather than
  by CI. Revisit once the catalogue has real content.

---

## Capabilities

| | Client | Admin |
|---|---|---|
| Register (self-service) | ✅ → pending | — |
| Log in | ✅ once active | ✅ |
| Change own password | ✅ | ✅ |
| Edit own first/last name | ✅ | ✅ |
| Change own email | ❌ | ❌ |
| Browse + filter video library | ✅ | ✅ |
| Approve / decline pending registrations | — | ✅ |
| Create a user directly with a temp password | — | ✅ |
| Edit / suspend / delete any user | — | ✅ |
| Add / edit / delete videos and tags | — | ✅ |

Every destructive admin action — decline, delete, unpublish — goes through a confirmation modal
naming the affected user or video. No bare icon buttons that delete on click.

---

## Registration paths

There are two, and they produce different initial states.

**Self-service.** Client registers → `Status = PendingApproval`, cannot log in → email fires to
the **admin**, not the client → admin approves or declines from the pending list → on approval
the client gets an email and can log in.

**Admin-created.** Admin fills first name, last name, email, and a temporary password →
`Status = Active`, `MustChangePassword = true` → admin passes the credentials to the client out
of band → on first login the client is forced to the change-password screen and cannot navigate
away until it is done.

`MustChangePassword` is enforced **server-side**: while it is true, every endpoint except
`/auth/me`, `/auth/change-password` and `/auth/logout` returns 403 `MUST_CHANGE_PASSWORD`. A
guard in Angular is a convenience, not the control.

---

## Schema changes needed

Against the current migration:

```
AspNetUsers
  - FullName                → replaced by
  + FirstName  nvarchar(80)  NOT NULL
  + LastName   nvarchar(80)  NOT NULL
  + MustChangePassword bit   NOT NULL DEFAULT 0
```

Plus `Video`, `Tag`, `VideoTag` from `docs/06` §3 when phase 5 lands.

Drop `IdempotencyRecord`? No — keep it. Video creation still benefits, and the table is free
when unused.

---

## Phases

Each phase is one Claude Code session, one branch, one PR. Do not merge two.

### Phase 1 — foundation ✅ done
Scaffold, layered projects, EF model, Identity, seeder, platform settings.

### Phase 2 — backend auth and user management
JWT issue/refresh/revoke, the two registration paths, password change, profile edit, admin user
CRUD, `IEmailSender` with a console implementation for local development.

Done when: every endpoint in §"API surface" below responds correctly from Swagger or curl.

### Phase 3 — Angular shell and auth UI
`ApiClient`, interceptors, guards, login, register, pending, forced password change, admin user
list with confirmation modals, placeholder client dashboard. Material, plain theme.

Done when: you can register, approve yourself as admin, log in as a client, and change your
password — in a browser.

### Phase 4 — deploy
App Service, SQL, Static Web Apps, CORS, real SMTP, custom domain. **Do this before writing a
line of video code.** A deployment problem found here costs an afternoon; found in phase 7 it
costs a week.

Done when: the trainer can log in from her own phone, on the real URL.

### Phase 5 — video backend
`Video`, `Tag`, `VideoTag`. Admin CRUD. Bunny Stream adapter behind `IVideoStorage`. Upload
credentials endpoint, webhook for transcode-complete. Filtered, paged client query.

### Phase 6 — video UI
Admin album: table, add/edit form with the four classifiers, upload with a real progress bar,
confirmation modals. Client library: filter bar, responsive card grid, pagination, player.

### Phase 7 — real content and handover
Trainer uploads real videos, first clients onboarded, admin account transferred to her.

---

## API surface after phase 2

```
# Auth — anonymous
POST   /api/auth/register                  202 { status: "PendingApproval" }
POST   /api/auth/login                     200 { accessToken, expiresIn, user } + refresh cookie
POST   /api/auth/refresh                   200 { accessToken, expiresIn }

# Auth — authenticated
GET    /api/auth/me                        200 { id, firstName, lastName, email, role,
                                                 status, mustChangePassword }
POST   /api/auth/change-password           204   { currentPassword, newPassword }
PUT    /api/auth/profile                   204   { firstName, lastName }
POST   /api/auth/logout                    204

# Admin
GET    /api/admin/users                    ?status=&search=&page=&pageSize=
POST   /api/admin/users                    201  create directly, MustChangePassword = true
PUT    /api/admin/users/{id}               204  first name, last name, role
POST   /api/admin/users/{id}/approve       204
POST   /api/admin/users/{id}/decline       204  { reason }
POST   /api/admin/users/{id}/suspend       204  { reason }
POST   /api/admin/users/{id}/reactivate    204
POST   /api/admin/users/{id}/reset-password 200 { temporaryPassword }
DELETE /api/admin/users/{id}               204  hard delete, admin-only, confirmed in UI

GET    /health                             200
```

`DELETE` is a real delete here, not a soft one — the earlier "never delete users" rule existed
to protect booking history, and there is no booking history any more. An admin cannot delete
themselves or the last remaining admin; the handler enforces both.

---

## Error codes

The Angular error interceptor switches on these, never on message text.

```
ACCOUNT_PENDING          403   registered, not yet approved
ACCOUNT_DECLINED         403   registration was rejected
ACCOUNT_SUSPENDED        403   access revoked by admin
INVALID_CREDENTIALS      401   wrong email or password — identical for unknown email; also
                               every refresh failure except the one below (unknown, expired,
                               reused token — deliberately vague)
SESSION_SUPERSEDED       401   on refresh: this session was replaced by a newer login on
                               another device (docs/03 §4.3, §5.4)
MUST_CHANGE_PASSWORD     403   temp password still in place
CURRENT_PASSWORD_WRONG   400   on change-password
EMAIL_ALREADY_EXISTS     409   on register or admin-create — except a Declined account, which
                               register resets back to PendingApproval and returns 202
USER_NOT_FOUND           404
USER_NOT_PENDING         409   approve/decline on a non-pending user
CANNOT_DELETE_SELF       409   delete on your own account
CANNOT_MODIFY_SELF       409   suspend on your own account
CANNOT_DELETE_LAST_ADMIN 409   delete or demote the last remaining Admin
```

Transport-level codes emitted by the host, not by handlers: `UNAUTHENTICATED` 401,
`FORBIDDEN` 403, `VALIDATION_FAILED` 400, `RATE_LIMITED` 429, `INTERNAL_ERROR` 500.

---

## Email

Three messages in phase 2, all through the existing outbox:

| Trigger | To | Contents |
|---|---|---|
| Client registers | **Admin** | who registered, link to the pending list |
| Admin approves | Client | you can now log in |
| Admin declines | Client | registration not accepted, optional reason |

Locally, `IEmailSender` writes to the Serilog console. Nothing about phase 2 or 3 should require
a real SMTP account — that arrives in phase 4.
