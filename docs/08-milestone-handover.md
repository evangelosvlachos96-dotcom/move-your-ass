# 08 — Milestone handover

**This is the resume-here document.** If you are an agent or a person picking this work up, read
the "Current state" section below before anything else. It is kept accurate at the end of every
checkpoint and after every meaningful step inside one. History follows it, oldest concerns last.

---

# Current state

**Updated:** 2026-09-25, checkpoints 2 and 3 local implementation; cloud verification pending.

| Item | State |
|---|---|
| Branch | feature/prod-readiness |
| Last commit | 847bc10 — documentation checkpoint 1 |
| Working tree | uncommitted checkpoints 2 and 3; owner performs all git operations |
| Phase | 4 — deployment and real delivery verification |

## Checkpoint status

| # | Checkpoint | State |
|---|---|---|
| 1 | Documentation / ADR-017 | reviewed and committed |
| 2 | PostgreSQL | implemented; 18 live API checks passed against configured Neon dev; suspend/resume observation pending |
| 3 | Resend HTTPS sender | implemented; local HTTP/configuration probes pass; real admin registration notification accepted by Resend; owner confirmed notification receipt; client invitation inbox delivery pending |
| 4 | Render container | not started; stop before this checkpoint |

The owner explicitly authorized 2 and 3 back to back with one combined review. Do not wait for
an intermediate commit, but keep this file current. Do not start checkpoint 4 without a new green light.

## Verified so far

- Replaced the three SQL Server migrations and snapshot with a generated PostgreSQL InitialCreate,
  as explicitly approved because no production data exists. This is a fresh database baseline,
  not a SQL Server data-conversion script.
- Release build succeeded with zero warnings/errors; all 15 existing tests passed after provider
  and initial email changes. Re-run after final code edits before the combined handover.
- Applied the migration to an isolated local PostgreSQL 17 container. Eighteen API checks passed:
  registration, pending login refusal, approval/login, client/admin separation, duplicate email
  rejection, case-insensitive search and literal percent/underscore/backslash matching,
  invitation creation, console dispatch, activation and concurrent single-use acceptance.
- Three delivered outbox rows had cleared payloads. Commit signalling works with PostgreSQL.
- EF reports no pending model changes and generates an idempotent PostgreSQL migration script.
- External mock-HTTP probe verified Resend request shape, stable outbox idempotency key and payload,
  sanitized HTTP failures, cancellation, and development/production email configuration validation.
  These probes are temporary workspace tools, not new repo test suites (ADR-014).
- Npgsql idle lifetime defaults to 240 seconds, minimum pool size zero and keepalive disabled.
- Docker engine 29.4.3 runs successfully after the owner's WSL update. The previous Docker blocker
  is resolved. Container used for local smoke checks: mya-phase4-check, localhost port 55432.
- No web files changed, so the prior successful Angular production build was not repeated.

## Not verified / owner setup

- Owner configured Neon dev locally and applied the migration using the direct endpoint. The API
  now runs on the saved pooled connection, seeds the admin, and passes 18 live account-flow checks.
- Owner reports domain/Cloudflare setup. DNS records and deployment are not verified.
- Owner confirmed the Resend domain is verified and received the real admin registration notification.
  Real client-invitation inbox delivery remains unverified; invitation acceptance passes with console delivery.
- Neon suspend/resume, real inbox/spam placement, Render deployment, forwarded headers, phone
  access and UptimeRobot remain unverified. Local PostgreSQL checks cannot prove Neon sleep.

## Exact next steps

1. API is running locally at http://localhost:5077 against the owner-configured Neon dev database.
   Console email is explicitly selected for synthetic account checks; real Resend delivery was
   separately verified for the admin notification and receipt confirmed by the owner.
2. Remaining verification: observe Neon suspend/resume and deliver a real client invitation to
   an owner-controlled second inbox/alias, then verify the emailed browser setup flow.
3. Owner reviews and commits checkpoints 2–3. Do not start checkpoint 4 without a new green light.
   No git add, commit or push has been performed by the agent.
## Gotchas and boundaries

- Production only accepts Email:Mode=Resend with ApiKey and valid From; Console is Development-only.
  MailKit/SMTP were removed. Resend failure logs contain status only, never provider response bodies.
- Each send receives the outbox Id as its Idempotency-Key. Resend retention is 24 hours;
  scheduled retry backoff totals about 2.5 hours, but downtime/manual replay can exceed retention.
- HttpClient timeout must become an ordinary delivery failure so the dispatcher retries; genuine
  host cancellation must propagate. Do not add transport retries alongside outbox retries.
- Keep /health database-free, the outbox event-driven, keepalive off and EF retry strategies off.
- App connection uses Neon pooled endpoint; migration/seed commands use direct endpoint.
- Cors:AllowedOrigin remains the email-link setting until checkpoint 4 renames it App:PublicOrigin.
  Production runbook describes the final checkpoint-4 settings, not today's complete runnable setup.
- The Azure deploy workflow is intentionally untouched until checkpoint 4 removes it.
- Render forwarded headers still require empirical confirmation during checkpoint 4.

## Settings: names only

| Setting | Location |
|---|---|
| ConnectionStrings:Default | local user-secrets, Neon dev pooled connection |
| ConnectionStrings__Default | per-command dev direct override for migration; Render production pooled later |
| Jwt:SigningKey, Seed:AdminEmail, Seed:AdminPassword, Seed:AdminFirstName, Seed:AdminLastName | local user-secrets |
| Email:ApiKey, Email:From, Email:Mode | local user-secrets; corresponding double-underscore keys on Render |
| Email:TestRecipient | optional local-only verification setting; app does not consume it |
| Cors:AllowedOrigin | local current frontend/email-link origin |
| App__PublicOrigin | Render, introduced in checkpoint 4 |
| Jwt__SigningKey, AUTOMAPPER_LICENSE_KEY | Render secrets |
| ASPNETCORE_ENVIRONMENT | Render non-secret setting |

---

# How to resume

**Read in this order:**

1. This section and "Current state" above.
2. `CLAUDE.md` — the non-negotiable rules, the layout, and the commands.
3. `docs/04-roadmap.md` Phase 4 — the four checkpoints and what "done" means.
4. ADR-017 in `docs/05-decisions.md` — why the stack is Render, Neon, Resend and Cloudflare, and
   the four code properties the free tier depends on.
5. `docs/10-production.md` — the runbook for the environment.
6. Only then the code.

**Verify where things actually stand** before trusting anything above:

```powershell
git log --oneline -1
git status --short
dotnet build -c Release          # must be 0 warnings, 0 errors
dotnet test -c Release --no-build
cd web ; npm run build -- --configuration production
```

**Do not redo these:**

- Do not re-research the provider limits, ports, headers and idempotency semantics. They are
  recorded in "Gotchas" above with the date they were checked. Re-check only if a provider is
  behaving differently from what is written there.
- Do not add unit or integration tests. ADR-014 defers them; `Mya.ArchitectureTests` must stay
  green.
- Do not re-verify the Angular production build unless `web/` changed.
- Do not run `git add`, `git commit` or `git push`. The owner does all git operations, and
  branches are never deleted after merging.
- Do not write a secret value anywhere, and never ask the owner to paste one into a conversation.

**If your context is running low,** stop the current work and update "Current state" first. An
accurate handover is worth more than one more half-finished step.

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