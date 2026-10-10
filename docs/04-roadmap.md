# 04 — Roadmap

This file is the single milestone sequence. The product is a private workout video library for
one trainer and approved clients. No booking, slots, calendar or booking change requests.

## Checkpoint — 2026-09-24

Account onboarding and admin UI implementation is ready for owner review and commit on
feat/web-admin. Backend build, existing tests, Angular development build and lint passed;
35 API/database checks passed in an isolated LocalDB database. Core admin browser interactions
were exercised. See docs/08-milestone-handover.md for the exact evidence and remaining checks.

The owner committed this checkpoint and gave the green light on 2026-09-25. Current work is
Phase 4 on `feature/prod-readiness`; see `docs/08-milestone-handover.md` for the current position
and `docs/10-production.md` for the runbook.
The app is deployed and live at https://moveyourass.gr. Video moved to Backblaze B2 (ADR-019);
live video acceptance and real-inbox email verification remain. See docs/08.

## Phase 1 — Foundation

Layered .NET projects, EF Core/Identity, migrations, seeder, Angular scaffold. Implemented.

## Phase 2 — Accounts and email backend

Implementation complete at this checkpoint; live email provider configuration remains Phase 4 work.

Registration with admin approval; password login, JWT/refresh/logout; profile and password changes;
admin user management. Admin-created users are invited by email and cannot log in until they set
their password using a single-use, expiring link. Resending invalidates previous links.
Console and provider delivery share the transactional outbox. No login 2FA in this scope.
The provider was SMTP at this checkpoint and became the Resend API in phase 4 (ADR-017).

## Phase 3 — Browser onboarding and administration

Implementation checkpoint complete. Remaining browser regression checks are recorded in the handover.

Login/register/pending/profile screens and protected shell; invitation password setup; admin dashboard
with pending registrations; searchable, paged user list with approve/decline, invite/resend, edit,
suspend/reactivate and confirmed deletion. The client dashboard now embeds the filtered video library.

Done when the manual checklist passes for both account creation paths, invalid/expired invitation
links, admin/client access boundaries and responsive screens. Build/lint are not a substitute for
these checks. The owner reviews, commits and pushes this milestone before the next phase.

## Phase 4 — Deployment and real delivery verification (done)

Move the stack from Azure to Render + Neon + Resend (ADR-017) and get it live on
`https://moveyourass.gr`. Runbook: `docs/10-production.md`. Current position, always:
`docs/08-milestone-handover.md` §"Current state".

Four checkpoints. On 2026-09-26 the owner authorized checkpoint 4 and video phases 5–6 together on the same branch, with one final review:

1. **Documentation and decision.** ADR-017, the merged runbook, and every doc that named Azure.
2. **SQL Server to PostgreSQL.** Npgsql provider, one fresh `InitialCreate`, and every
   provider-specific spot: the outbox claim query, UTC column types, duplicate-email detection,
   case-insensitive search, filtered indexes and check constraints.
3. **Resend email sender.** A typed `HttpClient` against Resend's send endpoint, with the outbox
   message Id as the idempotency key. SMTP and MailKit are removed; Render free blocks the ports.
4. **Container for Render.** Multi-stage Dockerfile, non-root, listening on Render's `PORT`,
   forwarded headers, `App:PublicOrigin`, and CI that builds the image without pushing it.

Then provision the environment by the runbook, apply migrations, seed the Admin and verify real
delivery to an actual inbox. Done when the trainer completes onboarding and logs in from her phone
on the real URL. The owner authorized video code work before live deployment; production acceptance still remains mandatory.

## Phase 5 — Video backend

Implemented; local automated verification passes. Live Backblaze acceptance remains in docs/11.

Video/Tag/VideoTag and migrations, admin CRUD, an S3-compatible adapter behind IVideoStorage,
presigned multipart upload URLs, HEAD-based completion instead of a webhook, published/ready-only
client query and presigned playback. Idempotency for video creation. See docs/06-video-catalogue.md.

## Phase 6 — Video UI

Implemented; automated tests and local browser checks are recorded in docs/08.

Admin uploads with progress, classifiers/tags and publishing; client filters/search, responsive
cards, pagination and player. No fictional usage/progress metrics.

## Phase 7 — Content and handover

Trainer uploads real workouts; onboard initial clients; transfer admin ownership and verify operations. The operator guide is docs/12-trainer-guide.md. Real content and operational sign-off remain owner/trainer work.

## Phase 8 — B2 video, cleanup, auth robustness and UI (current)

On `feature/b2-video-and-polish`, in four parts, each reviewed and committed separately.

- **A — Backblaze B2 video (ADR-019).** Bunny Stream removed; a provider-neutral S3-compatible
  adapter, presigned multipart uploads, HEAD verification instead of a webhook, a storage cap,
  and presigned playback in a native player.
- **B — cleanup.** Branded HTML and plain-text emails, the Npgsql GSSAPI log noise, the
  AutoMapper Community licence, and dead references left by the provider change.
- **D — authentication and robustness.** Two tabs on one device must stop logging each other
  out while one-session-per-user stays the rule; forgotten-password recovery; safer lockout;
  concurrency and double-submit coverage; encrypted weekly backups; index review.
- **C — logo, navigation and a responsive pass** with Playwright at three viewports, last, so
  the screenshots include everything above.

Current position, always: `docs/08-milestone-handover.md` §"Current state".

## Account API

| Method | Path | Result |
|---|---|---|
| POST | /api/auth/register | 202 PendingApproval; admin notification queued |
| POST | /api/auth/accept-invitation | 200 + success/traceId; token + newPassword; activates invited account |
| POST | /api/auth/login | Access token + user + refresh cookie |
| POST | /api/auth/refresh | Rotated refresh cookie and access token |
| GET | /api/auth/me | Current user |
| POST | /api/auth/change-password | 200 + success/traceId; currentPassword required except forced change |
| PUT | /api/auth/profile | 200 + success/traceId; own names |
| POST | /api/auth/logout | 200 + success/traceId |
| GET | /api/admin/users | Paged list; status/search/page/pageSize |
| POST | /api/admin/users | 201 { id }; creates Invited and queues setup email |
| PUT | /api/admin/users/{id} | 200 + success/traceId; names/role |
| POST | /api/admin/users/{id}/approve | Updated UserDto |
| POST | /api/admin/users/{id}/decline | Optional reason; updated UserDto |
| POST | /api/admin/users/{id}/suspend | Optional reason; updated UserDto |
| POST | /api/admin/users/{id}/reactivate | Updated UserDto |
| POST | /api/admin/users/{id}/resend-invitation | 200 + success/traceId; Invited accounts only |
| POST | /api/admin/users/{id}/reset-password | Existing active-user temporary-password reset |
| DELETE | /api/admin/users/{id} | 200 + success/traceId; confirmed hard delete |
| GET | /health | Health response |

Error codes live in Application/Common/Results/ErrorCodes.cs. New onboarding codes:
INVALID_INVITATION (400), ACCOUNT_INVITED (403), INVALID_USER_STATE (409).
Invited is enum value 4; existing persisted status values remain unchanged.

## Email events

Self-registration → admin; approval/decline → client; admin create/resend → client password setup link.
Default expiry is 24 hours. Development logs to the console and production sends through the
Resend API; real delivery still requires a verified sending domain and an API key.
See docs/09-email-setup.md.
