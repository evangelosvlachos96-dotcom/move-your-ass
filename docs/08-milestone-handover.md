# 08 — Milestone handover

# Current state

Updated 2026-09-26. Branch: **feature/prod-readiness**. Last owner commit: **a5a181b**.
Checkpoint 4 and video backend/UI changes are uncommitted. The owner authorized continuing
through phases 5–6 without intermediate review, on this branch, including automated tests.
Do not stage, commit, push, merge, delete branches, or provision paid services.

| Work | Status |
|---|---|
| Phase 4 checkpoint 1 | Documentation committed |
| Checkpoint 2 | PostgreSQL committed; earlier 18 API checks passed on Neon dev |
| Checkpoint 3 | Resend committed; owner confirmed receipt of admin notification |
| Checkpoint 4 | Container/PORT/proxy/SPA/CI implemented and locally verified |
| Phase 5 | Video model, additive migration, APIs, Bunny adapter implemented; final review in progress |
| Phase 6 | Admin dashboard/upload/catalogue and client library/player implemented; browser verification in progress |
| Phase 7 | Guides being completed; real trainer content and operational handover still require owner |

## Verified

- Original container checks: non-root UID 1654, custom PORT, database-free health against an
  unreachable database, SPA deep links, API 404, immutable bundles, uncached index, no production
  CORS, and separate forwarded-IP rate-limit buckets. Image builds with Angular and API together.
- Release build and 29 backend tests pass. New tests exercise draft privacy, active-user checks,
  stale revisions, idempotent creation, normalized Greek tags, webhook signatures, malformed/replayed
  events, provider failures and retryable deletion. SQLite handler tests do not prove PostgreSQL semantics.
- VideoCatalogue migration applied to isolated PostgreSQL 17 database mya_video_check; EF reports
  no pending model changes. Existing InitialCreate was preserved. No video migration applied to Neon.
- 17 live API checks pass against that local PostgreSQL database (validation, filtering, tag
  uniqueness, account approval, client/admin boundaries, disabled provider, unsigned webhook).
- Angular lint, production build and three frontend tests pass. The TUS library's url-parse
  CommonJS dependency is explicitly allowlisted; no general suppression of compiler warnings.
- Browser: admin dashboard counts, video status list and metadata edit/save verified using
  synthetic local records. Final browser and container pass remains the immediate next step.
- Docker Desktop works; the old WSL/daemon blocker is resolved.

## Not verified / owner setup

- No Render deployment, production migration/seed, DNS cutover, UptimeRobot or phone verification.
- Neon suspend/resume and client invitation inbox/browser completion remain unverified. The owner
  confirmed Resend domain verification and actual receipt of an admin registration notification.
- No Bunny account/library/credentials have been configured by this agent. Real phone upload,
  transcoding, signed webhooks, protected playback and provider billing remain live acceptance tasks.
  The adapter uses official Bunny contracts; mocks and local fixtures are not live provider evidence.
- Video:Bunny:Enabled defaults false. Account features remain usable; uploads show a clear disabled state.
- Local test video rows are synthetic UI fixtures, not real workouts or playable videos.

## Exact next step

Finish browser checks and final build/container regression, then update this section with final
evidence. Give the owner one combined report and suggested commit message. Owner reviews/commits
before deployment. Follow docs/10-production.md and docs/11-video-operations.md for live setup.

## Settings and boundaries

| Setting | Where |
|---|---|
| ConnectionStrings:Default | local user-secrets: Neon dev pooled; direct override for migration/seed |
| ConnectionStrings__Default | Render: production pooled; direct override for deliberate migrations |
| Jwt:SigningKey, Jwt:Issuer, Jwt:Audience | user-secrets / Render environment |
| Seed:AdminEmail, Seed:AdminPassword, Seed:AdminFirstName, Seed:AdminLastName | user-secrets / one-shot seed environment |
| Email:Mode, Email:ApiKey, Email:From | user-secrets / Render environment |
| App:PublicOrigin | user-secrets / App__PublicOrigin on Render; replaces Cors:AllowedOrigin |
| Video:Bunny:Enabled, LibraryId, ApiKey, ReadOnlyApiKey, TokenKey, CdnHost | user-secrets / Video__Bunny__* on Render |
| PORT, RENDER | Render-provided platform environment |

Keep /health database-free, outbox event-driven, keepalive and EF retry strategies off. Console
email is Development-only; real delivery logs metadata, never message credentials. Render proxy
handling trusts only one forwarded hop when RENDER=true, assuming the service is reachable only
through Render ingress. Actual Render headers still need empirical verification.

Creation reserves a unique creator/key before a provider call. An uncertain create response can
leave an orphan provider asset; do not blindly retry remote creation. Reconcile in Bunny first.
Deleting leaves an unpublished Deleting row on provider failure so an admin can retry safely.
See docs/06 for concurrency, ordering, token expiry and idempotency boundaries.

# How to resume

Read this file, CLAUDE.md, docs/04-roadmap.md, ADR-017/018 in docs/05-decisions.md,
docs/10-production.md and docs/11-video-operations.md. Check git status before editing.
Run dotnet test -c Release, then web npm run lint, npm test -- --watch=false and npm run build.
Build Docker only after source changes that affect the shipped image. No git mutations by agents.
All secret values stay outside repo/docs/chat. New tests are authorized, superseding ADR-014's deferral.

---

# History

## Checkpoint — production readiness, checkpoint 1 (2026-09-25)

Documentation only, no code changes.

- Added **ADR-017** superseding ADR-016's choice of providers: Render, Neon, Resend and Cloudflare,
  one origin on `moveyourass.gr`. ADR-016's text is retained as a record and marked superseded.
- Merged `docs/10-production-readiness.md` and `docs/10-free-tier-production.md` into a single
  `docs/10-production.md` and deleted both originals.
- Updated `CLAUDE.md` (stack, current phase, rules 6 and 7, production notes, gotchas, commands),
  `README.md`, `docs/01`, `docs/02`, `docs/04`, `docs/06`, `docs/09`, `infra/README.md` and
  `docs/backlog.md`.
- Rewrote the stale GitHub Pages showcase at `docs/index.html`, which still advertised booking,
  email 2FA, Azure SQL and Blob Storage.
- Fixed `.github/pull_request_template.md`, which linked to `docs/04-phase-1-scope.md`, a file
  that no longer exists, and required tests that ADR-014 defers.
- Restructured this file around a "Current state" section, per the owner's documentation-first
  handover rule.

**Known dangling reference, left on purpose.** `.github/workflows/deploy.yml` still names the
deleted `docs/10-free-tier-production.md` in a comment. That workflow is the Azure deployment job
and is deleted whole in checkpoint 4, so editing it now would only add noise to the review.
Four code comments also still cite ADR-016 (`SpaHostingExtensions`, `OutboxDispatcher`,
`OutboxSignal`, `SqlConnectionRetryInterceptor`); each is updated or deleted when checkpoints 2 to
4 touch that file.

**Carried over from the old readiness notes.** The production Angular build passes and was
re-verified today. The production SPA calls a relative `/api`, which the same-origin hosting
satisfies. No database was modified, no real email was sent, and no cloud resources were
provisioned.

**Dropped from the old readiness notes, deliberately.** The LocalDB connection details, the
record of which SQL Server migrations were applied, and the note that no SMTP settings were
configured are all obsolete: the database provider is changing in checkpoint 2 and the migrations
are being replaced wholesale. The old six-item "next actions" list was superseded by the four
checkpoints in `docs/04`. Nothing was dropped that still described a live constraint.

## Checkpoint — account onboarding and administration (2026-09-24)

Branch at the time: `feat/web-admin`. Committed by the owner on 2026-09-25, which authorised
`feature/prod-readiness`.

**Implemented:** removed unused booking configuration; added the Invited account state, expiring
single-use password setup links and admin resend; added the admin dashboard, pending count,
searchable and paged user list, invitation and edit dialogs, approval and decline, access
management with named confirmations and typed-email deletion; added configurable TLS SMTP delivery
alongside Development console email (since superseded by Resend); added the `AccountInvitations`
migration; fixed malformed Angular control-flow syntax in the password-change template.

**Verified at the time:** solution build clean; 15 tests passing; Angular development build and
lint passing; the new migration applied to an isolated SQL Server LocalDB database; 35 live API
and database checks covering registration, approval, login, admin and client separation, editing,
suspension, reactivation, self-deletion and last-admin protections, invitation creation and
activation, expired and malformed links, resending, rate limiting, and clearing of delivered
outbox payloads; concurrent acceptance of one invitation yielding exactly one success; a browser
pass over admin login, the dashboard, approval, the invitation dialog, status filters and the
invalid-link message.

The production Angular build failed at that checkpoint in a separate working copy, with an LMDB
cache error and then a native memory-allocation error. It did not reproduce in this repository and
now passes.

**Still outstanding from that checkpoint:** the browser and viewport regression matrix in
`docs/07-manual-test-checklist.md`, especially password setup in the browser and the full
mobile and desktop matrix. Real email delivery to an inbox has never been verified.

## Checkpoint 2 local verification (2026-09-25)

Replaced the old SQL Server migration set with a generated PostgreSQL InitialCreate, as approved.
Release build: zero warnings/errors; existing tests: 15 passed. Applied the migration to an isolated
PostgreSQL 17 container. Eighteen live API checks passed: registration, pending refusal, approval,
login, case-insensitive search and literal wildcard escaping, duplicate rejection, client/admin
separation, console outbox wake-up, invitation activation and concurrent single-use acceptance.
No Neon or production database was modified. Neon suspend/resume remains unverified.

## Real admin notification verification (2026-09-25)

The owner supplied their admin email and confirmed the sending domain is verified. Saved only
in user-secrets; seeded an admin in the isolated mya_email_checkpoint PostgreSQL database.
A synthetic client registration returned HTTP 202 and queued AdminNewRegistration. Resend accepted
the message on attempt 1; ProcessedAtUtc was populated and PayloadJson cleared. No Neon or production
data was touched. The test host was stopped after verification; local settings retain Console
as the default unless explicitly overridden. Await owner inbox confirmation and client test alias.

## Neon development API verification

Owner saved the pooled connection and applied migrations using the direct connection. Started
`dotnet run --project src/Mya.Api` successfully; development admin seeding and login passed.
All 18 live account-flow checks passed against Neon: health, registration, pending login refusal,
case-insensitive search and literal wildcard escaping, approval/login, authorization separation,
duplicate rejection, invitation dispatch, concurrent single-use acceptance, and activated profile.
All 15 existing backend tests passed again. API remains running at http://localhost:5077.
Synthetic test users remain in the dev database only. Test emails were console-only. No production
migration or deployment was performed. Do not infer verified Neon scale-to-zero from these checks.
## Extended owner authorization — 2026-09-26

Finish checkpoint 4, then implement phases 5 and 6 and code-level handover work on feature/prod-readiness. Use Bunny Stream as the documented provider default. Add meaningful automated and manual verification. Record unavailable live provider/deployment verification separately; never claim real streaming without credentials and a live test. Branded HTML/MJML email layout remains requested technical debt. Owner retains git operations.

## Checkpoint 4 local verification — 2026-09-26

Docker image mya:checkpoint4 builds successfully including Angular lint and production build. Backend builds without warnings; 15 existing tests pass. Runtime UID 1654, custom PORT=10077 verified. Root and deep SPA links load; unknown API routes return JSON ProblemDetails 404; hashed JavaScript has immutable caching; index revalidates; no production CORS. Health returns 200 with the database deliberately unreachable. Forwarded IP simulation separates rate-limit buckets. No cloud deployment performed. RENDER=true trusts only the last forwarded hop and assumes traffic reaches the service through Render ingress; this must be confirmed on the real service. Live proxy headers are not proven by this local test.

Next: video backend and UI under owner's extended authorization. App:PublicOrigin replaces Cors:AllowedOrigin; local user-secrets must be migrated without printing values. Branded HTML/MJML email improvements recorded in backlog.
