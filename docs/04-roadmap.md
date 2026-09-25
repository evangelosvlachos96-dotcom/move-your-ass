# 04 — Roadmap

This file is the single milestone sequence. The product is a private workout video library for
one trainer and approved clients. No booking, slots, calendar or booking change requests.

## Checkpoint — 2026-09-24

Account onboarding and admin UI implementation is ready for owner review and commit on
feat/web-admin. Backend build, existing tests, Angular development build and lint passed;
35 API/database checks passed in an isolated LocalDB database. Core admin browser interactions
were exercised. See docs/08-milestone-handover.md for the exact evidence and remaining checks.

The owner committed this checkpoint and gave the green light on 2026-09-25. Current work is
Phase 4 on `feature/prod-readiness`; see `docs/10-production-readiness.md`.
Real SMTP delivery still needs credentials and inbox verification. Video implementation has not started.

## Phase 1 — Foundation

Layered .NET projects, EF Core/Identity, migrations, seeder, Angular scaffold. Implemented.

## Phase 2 — Accounts and email backend

Implementation complete at this checkpoint; live SMTP provider configuration remains Phase 4 work.

Registration with admin approval; password login, JWT/refresh/logout; profile and password changes;
admin user management. Admin-created users are invited by email and cannot log in until they set
their password using a single-use, expiring link. Resending invalidates previous links.
Console and SMTP delivery share the transactional outbox. No login 2FA in this scope.

## Phase 3 — Browser onboarding and administration

Implementation checkpoint complete. Remaining browser regression checks are recorded in the handover.

Login/register/pending/profile screens and protected shell; invitation password setup; admin dashboard
with pending registrations; searchable, paged user list with approve/decline, invite/resend, edit,
suspend/reactivate and confirmed deletion. The client dashboard remains a video-library placeholder.

Done when the manual checklist passes for both account creation paths, invalid/expired invitation
links, admin/client access boundaries and responsive screens. Build/lint are not a substitute for
these checks. The owner reviews, commits and pushes this milestone before the next phase.

## Phase 4 — Deployment and real delivery verification (current)

Provision/configure App Service, Azure SQL and Static Web Apps; migrations, HTTPS, CORS, production
seeding, real SMTP credentials, domain and monitoring. Verify delivery to an actual inbox.
Done when the trainer completes onboarding and logs in from her phone on the real URL.
Complete this before video implementation.

## Phase 5 — Video backend

Video/Tag/VideoTag and migrations, admin CRUD, Bunny Stream adapter behind IVideoStorage,
upload credentials, authenticated provider webhook, published/ready-only client query and playback.
Idempotency for video creation. See docs/06-video-catalogue.md.

## Phase 6 — Video UI

Admin uploads with progress, classifiers/tags and publishing; client filters/search, responsive
cards, pagination and player. No fictional usage/progress metrics.

## Phase 7 — Content and handover

Trainer uploads real workouts; onboard initial clients; transfer admin ownership and verify operations.

## Account API

| Method | Path | Result |
|---|---|---|
| POST | /api/auth/register | 202 PendingApproval; admin notification queued |
| POST | /api/auth/accept-invitation | 204; token + newPassword; activates invited account |
| POST | /api/auth/login | Access token + user + refresh cookie |
| POST | /api/auth/refresh | Rotated refresh cookie and access token |
| GET | /api/auth/me | Current user |
| POST | /api/auth/change-password | 204; currentPassword required except forced change |
| PUT | /api/auth/profile | 204; own names |
| POST | /api/auth/logout | 204 |
| GET | /api/admin/users | Paged list; status/search/page/pageSize |
| POST | /api/admin/users | 201 { id }; creates Invited and queues setup email |
| PUT | /api/admin/users/{id} | 204; names/role |
| POST | /api/admin/users/{id}/approve | Updated UserDto |
| POST | /api/admin/users/{id}/decline | Optional reason; updated UserDto |
| POST | /api/admin/users/{id}/suspend | Optional reason; updated UserDto |
| POST | /api/admin/users/{id}/reactivate | Updated UserDto |
| POST | /api/admin/users/{id}/resend-invitation | 204; Invited accounts only |
| POST | /api/admin/users/{id}/reset-password | Existing active-user temporary-password reset |
| DELETE | /api/admin/users/{id} | 204; confirmed hard delete |
| GET | /health | Health response |

Error codes live in Application/Common/Results/ErrorCodes.cs. New onboarding codes:
INVALID_INVITATION (400), ACCOUNT_INVITED (403), INVALID_USER_STATE (409).
Invited is enum value 4; existing persisted status values remain unchanged.

## Email events

Self-registration → admin; approval/decline → client; admin create/resend → client password setup link.
Default expiry is 24 hours. Development logs and configurable SMTP are supported; real delivery
still requires provider credentials. See docs/09-email-setup.md.
