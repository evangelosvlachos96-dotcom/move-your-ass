# 05 — Architecture Decisions

Short ADRs. Each records what was chosen, what was rejected, and what it costs. Revisit when a
"consequence" starts hurting.

---

### ADR-001 — Layered architecture with feature folders inside each layer

**Status:** accepted

Layers (Api / Application / Domain / Infrastructure) give the horizontal separation and keep EF
out of the use cases. Feature folders *inside* each layer mean one feature lives in three
predictable places instead of scattered across `Services/`, `Models/`, `Validators/`.

Rejected: pure vertical slices (harder to enforce that Application stays persistence-ignorant at
this size), and classic N-tier with type-based folders (fine at 5 features, painful at 20).

**Cost:** more projects than a single-assembly app needs. Enforced by NetArchTest, so the
boundary is real rather than aspirational.

---

### ADR-002 — No mediator library

**Status:** accepted

Handlers are plain classes registered in DI and injected into controllers. MediatR moved to a
paid licence, and at ~20 use cases the indirection costs more than it returns.

Rejected: MediatR (licence + indirection), Wolverine (excellent, but a larger framework
commitment than this app justifies).

**Cost:** no free pipeline for cross-cutting behaviour. Validation runs as an action filter;
anything else uses a decorator. If decorators start multiplying, revisit.

---

### ADR-003 — ASP.NET Core Identity + JWT, not an external IdP

**Status:** accepted

The defining requirement is *manual admin approval before access*. That needs the user directory
in your own database, where approve/suspend/list are plain SQL. Entra External ID, Auth0, and
Clerk all push the directory out and make this custom-policy work, plus per-MAU billing.

**Cost:** you own password storage, lockout, 2FA, and email confirmation. Identity implements all
of it, but it is your surface to secure. See the checklist in `docs/03` §7.

---

### ADR-004 — Access token in memory, refresh token in an HttpOnly cookie

**Status:** accepted

15-minute JWT held in an Angular signal; 14-day rotating opaque refresh token in
`HttpOnly; Secure; SameSite=Strict`. An XSS can read `localStorage`; it cannot read the cookie.

Rejected: token in `localStorage` (XSS-readable), session cookies only (couples SPA and API
origins, complicates a future mobile client).

**Cost:** one silent refresh call on every page load, and the SPA must handle a refresh race
when several requests 401 at once — solved with a single-flight `shareReplay` in the interceptor.

---

### ADR-005 — Lazy session invalidation, not per-request validation

**Status:** accepted

`ActiveSessionId` is checked on refresh only. A suspended or superseded user is out within 15
minutes rather than instantly.

Rejected for now: validating `sid` and `SecurityStamp` on every request. It works, but it makes a
stateless JWT partly stateful and needs cache invalidation across App Service instances.

**Cost:** up to a 15-minute window where a revoked user keeps working. Acceptable for a training
platform. Revisit if a real abuse case appears.

---

### ADR-006 — Slot booking correctness lives in one SQL statement

**Status:** accepted

`UPDATE Slot SET BookedByUserId = @u WHERE Id = @s AND BookedByUserId IS NULL` — rows affected
decides the winner. Idempotency keys, tab sync, polling, and session control are all layered on
top, and none of them is allowed to become the guarantee.

Rejected: `SELECT` then `UPDATE` (races), application-level locks (do not survive multiple
instances), optimistic concurrency via `rowversion` alone (works, but a conditional update is
simpler and needs no retry loop).

**Cost:** booking logic partly expressed as raw SQL rather than LINQ. Worth it — this is the one
place where being clever loses money and trust.

---

### ADR-007 — Idempotency keys on user-initiated mutations

**Status:** accepted

Client generates a UUID per *intent*, server dedupes on `(UserId, Key)` for 24 hours and replays
the original response. Kills double-clicks, network retries, and duplicate submissions from two
tabs.

**Cost:** one extra table, one filter, and a pruning job. The client must generate the key when
the dialog opens, not when the button is pressed — get this wrong and the whole thing is decorative.

---

### ADR-008 — Video as pre-encoded HLS on Blob Storage

**Status:** accepted, revisit if encoding becomes a chore

Encode to a 3-rendition ladder with ffmpeg before upload; serve segments from private Blob with
a directory-scoped SAS; play with hls.js. Adaptive bitrate cuts egress ~5× for mobile viewers,
and egress is the only unpredictable line in the bill.

Rejected: single progressive MP4 (one bitrate, buffers on mobile, full egress cost), Azure Media
Services (retired 2024), Bunny/Cloudflare Stream (genuinely good and cheap — held in reserve).

**Cost:** a manual encoding step per video. `IVideoStorage` keeps the swap to a managed platform
to one class.

---

### ADR-009 — Email only, no SMS

**Status:** accepted

SMS has no free tier anywhere, Azure Communication Services toll-free numbers do not cover
Greece, and alphanumeric sender IDs need registration. Email plus an in-app notification badge
covers admin alerting.

**Cost:** notification latency depends on the admin checking email. If that proves too slow, a
Telegram bot delivers to a phone for free and takes an afternoon.

---

### ADR-010 — Outbox for all outbound side effects

**Status:** accepted

Emails are written as rows in the same transaction as the state change; a `BackgroundService`
dispatches them. A dead SMTP server cannot roll back a user approval.

**Cost:** emails are near-real-time, not real-time. The dispatcher must claim rows atomically so
a recycled App Service instance never double-sends.

---

### ADR-011 — Deploy to Azure at step 5 of 13

**Status:** accepted

Ship a skeleton that only serves `/health` before writing a single feature. Deployment problems
found on day three cost hours; the same problems on day sixty cost a week.

**Cost:** infra work before there is anything to show. This is the point.

---

### ADR-012 — App Service F1 now, B1 when cold starts hurt

**Status:** accepted, with a trigger

F1 is free but sleeps after 20 minutes idle, caps at 60 CPU-minutes/day, and stops outright when
the quota is hit. SQL serverless auto-pause stacks a second cold start on top.

**Trigger to upgrade:** acceptance criterion 14 in `docs/04` — measure cold-start latency against
the deployed environment. If a first login of the day exceeds ~10 seconds, move to B1 (~€12/mo).
Do not agonise over this; it is twelve euros.

---

## Status changes — September 2026 (booking dropped, see `docs/04-roadmap.md`)

- **ADR-006** (slot booking in one SQL statement) — **withdrawn**. No booking, no slot race.
- **ADR-007** (idempotency keys on user-initiated mutations) — **narrowed**. `IdempotencyRecord`
  stays; the only planned use is video creation.
- **ADR-008** (pre-encoded HLS on Blob) — **superseded** by Bunny Stream behind `IVideoStorage`.
  The trainer uploads from a phone and will never run ffmpeg.

---

### ADR-013 — Email 2FA deferred

**Status:** accepted

Password-only login for now. Email 2FA doubles the login surface and couples the first deploy to
a working mail provider. `TwoFactorTicket` stays in the schema, unused, so enabling it later is
code only.

**Cost:** a leaked password is enough to log in until 2FA returns. Mitigated by lockout (5
failures, 15 minutes), per-email rate limiting and single active session.

---

### ADR-014 — Automated tests deferred beyond the architecture tests

**Status:** accepted, revisit once the catalogue has real content

Deliberate, to reach a deployed product faster. `Mya.ArchitectureTests` stays green; the phase 1
seeder, converter and settings tests stay; nothing new is written for phases 2–4.

**Cost:** regressions are found by the trainer in production rather than by CI.


## Current scope overrides — 2026-09-24

The historical ADR text above is retained as a decision record, not current scope.
Booking is excluded (ADR-006 withdrawn); idempotency is planned only for video creation
(ADR-007 narrowed); Bunny Stream replaces manual Blob/HLS (ADR-008 superseded).
The only phase sequence is docs/04-roadmap.md; old deployment step numbers are obsolete.

### ADR-015 — Email invitation for admin-created accounts

Accepted. Create an inactive Invited account and queue a single-use, expiring password setup
link. Do not return or email temporary passwords for this creation path. Activation requires
the client to choose a password. Resending invalidates earlier unused links.

Console delivery remains available in Development; configurable TLS SMTP sends real mail with
metadata-only logging. Approval/invitations are not 2FA; password login remains in force.

---

### ADR-016 — Launch production on free tiers, one origin

**Status:** **superseded by ADR-017** for the choice of providers. The *shape* of the decision —
free tiers, one origin, SPA from `wwwroot`, signal-driven outbox, one-shot seeding — survives
intact and is restated in ADR-017. Only the vendors changed: App Service → Render, Azure SQL →
Neon, Brevo SMTP → the Resend HTTPS API. ADR-012's "upgrade when cold starts hurt" trigger still
applies in spirit; its B1 threshold is replaced by ADR-017's Render Starter trigger.

The original text is kept below as the decision record.

Superseded by ADR-017. Supersedes the "free tiers are not the target" note in CLAUDE.md for launch.

Production starts at €0/month: App Service **F1 (Linux)** for the API, the **Azure SQL Database
free offer** (serverless GP, 100,000 vCore-seconds + 32 GB per month, auto-pause when exhausted)
and **Brevo** free SMTP (300 emails/day). Its runbook, `docs/10-free-tier-production.md`, was
merged into `docs/10-production.md` by ADR-017 and no longer exists.

Consequences in code:

- **The API serves the Angular build from `wwwroot`.** F1 cannot bind a custom domain, and Static
  Web Apps Free cannot link an App Service backend, so SPA and API would sit on two different
  public-suffix hosts (`*.azurestaticapps.net`, `*.azurewebsites.net`). The refresh cookie is
  `SameSite=Strict`, so it would never be sent. One origin fixes that and removes CORS in
  production. Static Web Apps is dropped.
- **The outbox dispatcher no longer polls.** It drains, then sleeps until a commit signals new rows
  (`OutboxSignal`, EF interceptors) or the earliest retry is due. A 15-second poll would keep the
  serverless database awake permanently and use the monthly free compute within days.
- **Connection opens are retried** (`SqlConnectionRetryInterceptor`) to ride out the up-to-a-minute
  resume after auto-pause. Commands are not retried; `EnableRetryOnFailure` is incompatible with
  the handlers' explicit transactions.
- **Production seeding is a one-shot command** (`--seed-admin`), never part of app startup.

**Cost:** no SLA on either free service; a cold first request after idle can take tens of seconds
(app wake + database resume); the URL is `*.azurewebsites.net` until a paid tier; if the free
vCore-seconds run out the database pauses until the 1st of next month.

**Upgrade triggers:** first request of the day regularly over ~15 s, or the F1 CPU quota is hit →
App Service B1. Free vCore-seconds under 10% before the 20th of a month → allow paid overage on
the database. Emails landing in spam → buy a domain and authenticate it in Brevo.

---

### ADR-017 — Render + Neon + Resend, one origin on moveyourass.gr

**Status:** accepted, with upgrade triggers. Supersedes ADR-016's choice of providers.

Production runs as **one Render free web service** built from a Dockerfile in the Frankfurt
region, serving both the API and the Angular build on `https://moveyourass.gr`. The database is
**Neon PostgreSQL** (AWS `aws-eu-central-1`, Frankfurt), branch `production` for production and
branch `dev` for local work. Email goes through the **Resend HTTPS API**. DNS is **Cloudflare**.
Runbook: `docs/10-production.md`.

**Why the providers changed.** Koyeb closed its free tier to new signups, which removed the
obvious Azure alternative. Render's free web service builds from a Dockerfile, supports custom
domains with managed TLS certificates, and offers a Frankfurt region, which is what this app
needs. It does not offer outbound SMTP: Render blocks ports 25, 465 and 587 on free web services,
and port 25 stays blocked even on paid plans. That single fact decides the email design. A
provider reached over HTTPS is the only option, so MailKit and SMTP delivery are removed and
Resend's send endpoint replaces them.

**What carries over from ADR-016, unchanged:**

- **The API serves the Angular build from `wwwroot`.** One origin keeps the `SameSite=Strict`
  refresh cookie working and removes CORS from production entirely. `SpaHostingExtensions` stays.
- **The outbox dispatcher does not poll.** It drains, then sleeps until a commit signals new rows
  or the earliest retry is due. This mattered for Azure SQL's free vCore-seconds; it matters just
  as much for Neon, whose free compute allowance is consumed by query activity.
- **Production seeding is a one-shot command** (`--seed-admin`), never part of app startup.
- **Migrations never run on startup.** They are applied deliberately against the production branch.

**What ADR-016 got wrong, and the correction.** ADR-016 said "no pingers, ever", because on Azure
any request woke both the app and the database and burned the database's free compute. That rule
is now too strong and would break the deployment. Render spins a free service down after 15
minutes without inbound traffic, so the service needs an external monitor to stay up. The rule
becomes:

> **Pingers may only call `/health`, and `/health` must never touch the database.**

This is safe because the two free allowances are consumed differently. Render bills wall-clock
time the instance is running. Neon bills compute time, and its scale-to-zero timer is driven by
*active queries*, not by open connections: a connection sitting idle in the pool does not hold the
compute awake, and Npgsql sends no keep-alive queries of its own. So a monitor that only touches
`/health` keeps Render awake and lets Neon sleep. `SqlConnectionRetryInterceptor` is removed with
the SQL Server provider; Neon resumes in a few hundred milliseconds, well inside Npgsql's default
15-second connect timeout, so nothing replaces it.

Four properties of the code now carry the free tier, and none of them may be broken casually:

1. `/health` performs no database work.
2. No background service polls the database on a timer.
3. Npgsql's `Keepalive` stays at its default of disabled.
4. EF Core connection resiliency stays off, which the handlers' explicit transactions require anyway.

**Cost.** No SLA on Render free or Neon free. Without the keep-alive the first request after 15
idle minutes waits about a minute for the instance to restart. The keep-alive itself is an
unofficial arrangement: Render documents the spin-down and the instance-hour budget factually and
its own uptime guidance recommends external probes, but it does not bless pinging as a way to
avoid spin-down, and nothing stops Render from changing that. Keeping one service awake costs
roughly 730 of the 750 free instance hours in a month, so the free budget supports exactly one
always-on service and no second environment. Exceeding Neon's compute allowance is a hard stop
rather than a slowdown: the project's compute is suspended until the next billing period, existing
connections drop, and new ones cannot open. Neon documents no threshold alerting, so a weekly
manual check of its usage page replaces the Azure budget alert.

**Upgrade triggers:**

- Real clients depending on the site daily → **Render Starter**, which is always on and removes
  both the spin-down and the dependency on an external pinger. This is the trigger that matters;
  the keep-alive is a launch expedient, not the destination.
- Neon compute consistently above half the monthly allowance, or a suspension actually occurring
  → a paid Neon plan.
- Emails landing in spam, or more than 100 a day → a paid Resend plan.
- A second environment (staging) is wanted → paid Render, because the free instance-hour budget
  covers one service.

**Video is not affected.** Cloudflare R2 is Phase 5 at the earliest and is not part of this
decision. R2 is plain object storage with no transcoding and no HLS packaging, which is exactly
why `docs/06` chose Bunny Stream; Cloudflare's transcoding product is Stream, which is paid and
priced per minute. See `docs/06-video-catalogue.md` §4.

## ADR-018 — Video implementation and verification (2026-09-26)

Status: implemented locally, live provider setup pending. The owner authorized finishing video
backend/UI and tests while away, without another branch or intermediate commit. This supersedes
ADR-014's test deferral for this work and the roadmap's deployment-before-video sequencing.

Choose Bunny Stream, following docs/06: browser-to-provider resumable TUS uploads, automatic
transcoding, signed embedded playback and signed webhooks. R2 plain storage does not meet the
phone-upload/transcoding requirement without additional infrastructure. This decision provisions
no account, creates no charge, and does not establish a live service. Configuration defaults off.

Provider boundaries live behind IVideoStorage. Webhook HMAC-SHA256 uses the library read-only API
key over exact request bytes, with explicit signature version and algorithm. Replayed events
fetch authoritative status instead of trusting an old payload. Clients only see Ready + published
rows and must still be active; signed playback expires after 15 minutes. Provider-side access
settings remain required, with live negative tests before release. This is access control, not DRM.

Creation idempotency uses a unique (CreatedByUserId, CreationKey) reservation and payload hash
on Video rather than the legacy IdempotencyRecord table. Replay returns the existing row while
it exists; different payload conflicts. Hard deletion removes that reservation. Provider creation
cannot be atomically committed with PostgreSQL: an uncertain result requires reconciliation, not
unbounded retry. Deletion is retryable via an unpublished Deleting state. Revision UUIDs provide
optimistic concurrency. Tags normalize accents/case in application code with a unique DB index.

UI uses native selects, tag checkboxes and file picker, preserving taxonomy and accessibility
without a new component dependency. Pause/resume works while the page remains open; reopening
requires selecting the source file and restarting upload. No video bytes pass through Render.

Sources: [TUS uploads](https://bunny.net/docs/stream/tus-resumable-uploads),
[token authentication](https://bunny.net/docs/stream/token-authentication),
[signed webhooks](https://bunny.net/docs/stream/webhooks).
