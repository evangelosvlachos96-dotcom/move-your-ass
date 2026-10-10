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

> **Overtaken by ADR-019.** Plain object storage is what video now uses, on Backblaze B2, and
> the loss of transcoding and HLS is accepted rather than avoided. Nothing else in ADR-017
> changes: video bytes still never pass through Render, and object storage is not on the
> `/health` path, so the free-tier properties above are untouched.

## ADR-018 — Video implementation and verification (2026-09-26)

**Status: superseded by ADR-019 for the choice of provider.** Bunny Stream was never configured
or paid for, and the adapter, its TUS upload client and its signed webhook are removed. The
*requirements* below survive and are restated in ADR-019: direct browser-to-provider uploads, no
bytes through Render, Ready-and-published-only client access, short-lived playback links,
creation idempotency on `(CreatedByUserId, CreationKey)`, retryable deletion through an
unpublished `Deleting` state, revision UUIDs for optimistic concurrency, and accent-normalised
tags. The original text is kept below as the record. Its supersession of ADR-014's test deferral
also still stands.

The owner authorized finishing video backend/UI and tests while away, without another branch or
intermediate commit.

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

---

### ADR-019 — Backblaze B2 behind a provider-neutral S3 adapter; Bunny Stream removed (2026-09-27)

**Status:** accepted. **Supersedes ADR-018's choice of provider.** The video *requirements* in
ADR-018 — direct browser-to-provider uploads, no bytes through Render, published-and-ready-only
client access, short-lived playback links, creation idempotency, retryable deletion — all stand.
What changes is the provider and, because of that, what the product can promise.

Video is stored in **Backblaze B2** through its **S3-compatible API**, reached with a generic
S3 adapter. `Video:Provider` exists as an explicit seam and accepts only `S3`; **Cloudflare R2 or
MinIO would work by changing `ServiceUrl`, `Region`, keys and bucket, with no code change.**

**Why B2 and not Bunny.** Bunny Stream costs about $1/month minimum and needs a card. B2's free
tier is 10 GB of storage with no card, which is what the owner chose. That decision is the whole
of it; the rest of this ADR is the consequences.

**What B2 does not give us, and what we do instead.**

| Bunny Stream gave | B2 gives | What we do |
|---|---|---|
| Transcoding to several renditions | Nothing; the object is the file uploaded | The browser plays the trainer's original. Recording settings now matter, so docs/12 tells the trainer to use "Most Compatible" on iPhone and 720p/1080p |
| HLS packaging and adaptive bitrate | A single file over HTTP range requests | A native `<video>` element. A client on a weak connection buffers instead of dropping to a lower rendition |
| A hosted player with signed embeds | No player | Native controls, `controlsList="nodownload"`, `playsinline` |
| A webhook when processing finishes | No events at all | There is nothing to process. The API completes the multipart upload, HEADs the object and marks it Ready in one request |
| Signed embed tokens tied to a session | Presigned URLs | See "the playback trade-off" below |

**Bunny is removed, not kept selectable.** Keeping both was considered and rejected. The old
`IVideoStorage` was Bunny-shaped — `UploadCredentials(…, LibraryId, Signature, …)`,
`VerifyWebhook`, `OwnsLibrary`, `RemoteVideo(int Status, …)` decoding Bunny's numeric states —
so supporting both meant a union interface where half the members throw per provider, a webhook
endpoint one provider never calls, two upload clients in Angular (`tus-js-client` and the
multipart client), and two player modes. That is roughly double the video surface for a provider
the owner has decided not to pay for. Re-adding a provider later is a new adapter behind the
seam, not a rewrite.

**The multipart lifecycle is server-side, and that is the load-bearing design choice.** The
browser only ever PUTs a part to a presigned URL. The API calls `CreateMultipartUpload`, issues
one presigned PUT per part, and on completion calls **`ListParts` itself** to read the part
ETags before `CompleteMultipartUpload`.

The alternative — the browser collecting ETags from its own PUT responses — requires the bucket
to expose `ETag` to script via CORS, and Backblaze's CORS documentation does not list `ETag` among
supported `exposeHeaders` values. Reading the ETags server-side removes that dependency entirely:
the bucket only ever has to allow `s3_put` from the browser. It also means nothing the browser
reports about the upload is trusted, which is worth having on its own.

**Measured afterwards (2026-09-27): B2 does in fact accept `ExposeHeaders: ["ETag"]`** through
`PutBucketCors`, returning it lowercased. So the CORS obstacle turned out not to exist. The design
stays, because not trusting the browser's account of its own upload is the better property, and
because it keeps the bucket's CORS surface to the minimum either way.

**Verification replaces the webhook.** After completion the API HEADs the object and compares
size and content type against what was declared at creation. A mismatch deletes the object and
leaves the row `Failed`; only a match becomes `Ready`. `Refresh` re-derives state the same way.
The `Processing` status survives in the enum — persisted integers must not shift — but is
unreachable for S3.

**Accepted: only MP4 and QuickTime.** Anything else is refused before a URL is issued, because
without transcoding an unusual container is a file no client can play.

**Accepted, the playback trade-off: a presigned link is shareable until it expires.** Playback
is a presigned GET with a default two-hour lifetime. Anyone given that URL can fetch the file
without signing in, until it expires. There is no per-session binding and no revocation: an
already-issued link survives unpublishing and account suspension. This is weaker than Bunny's
signed embeds and it is accepted, for the same reason ADR-018 accepted its own limit — this is
access control for a private library, not DRM. Two hours is the balance between "long enough to
watch a workout without the link dying mid-playback" and "short enough that a leaked URL rots".

**Storage cap, enforced before presigning.** `Video:S3:StorageCapBytes` defaults to 9 GiB,
under B2's 10 GB free tier. The check sums `SizeBytes` over **every** row including drafts,
because a multipart upload's parts occupy provider storage from the moment they land. Aborting
or deleting a draft aborts its multipart upload so those parts stop counting. The admin screen
shows usage against the cap and warns from 80%.

**Egress is the limit that is easier to hit than storage.** B2's free downloads are 3× average
monthly stored data. At 9 GB stored that is about 27 GB a month, which a 200 MB workout watched
135 times exhausts. Watch it in the Backblaze dashboard; see docs/11.

**Checksums.** AWS SDK v4 attaches CRC checksums by default and several S3-compatible providers,
B2 among them, have rejected those headers with HTTP 400. The adapter sets
`RequestChecksumCalculation` and `ResponseChecksumValidation` to `WHEN_REQUIRED`. Backblaze is
reported to have added support for these headers in July 2025, so this may now be unnecessary;
it is kept because it costs nothing and keeps the adapter portable. **Verified working against
the real bucket on 2026-09-27** — no checksum rejection occurred.

**Path-style addressing** (`ForcePathStyle`) is used because B2 and MinIO both serve buckets
under the endpoint path. B2's documentation does not state this either way; **verified working
against the real bucket on 2026-09-27.**

**Live acceptance passed on 2026-09-27**: a 44 MB H.264 phone recording uploaded as three
presigned parts, completed and HEAD-verified to Ready, played back with seeking, published, and
deleted. See docs/08. Production upload, any mobile browser, and HEVC recordings remain untested.

**Presigned URLs follow the endpoint's scheme.** The SDK presigns `https` regardless of the
configured endpoint, which is wrong for a plain-HTTP container. Production is HTTPS-only and
startup validation enforces it.

**Cost.** No transcoding means the trainer's recording settings decide compatibility and size,
and the guide has to say so. No adaptive bitrate means a weak connection buffers. A presigned
link is shareable until expiry. Free storage is 10 GB and free egress is 3× stored, so the
product has a real ceiling that the cap and the dashboard make visible rather than surprising.

**Upgrade triggers:** storage consistently near the cap, or egress approaching 3× stored →
a paid B2 plan, or Cloudflare R2, which has no egress charge and needs only new configuration
values. Adaptive playback actually being needed → a transcoding provider, which is a new adapter.

Sources: [B2 S3-compatible API](https://www.backblaze.com/docs/cloud-storage-s3-compatible-api),
[S3-compatible API operations](https://www.backblaze.com/apidocs/introduction-to-the-s3-compatible-api),
[B2 CORS rules](https://www.backblaze.com/docs/cloud-storage-cross-origin-resource-sharing-rules),
[B2 pricing and free tier](https://www.backblaze.com/cloud-storage/pricing),
[AWS SDK data-integrity settings](https://docs.aws.amazon.com/sdkref/latest/guide/feature-dataintegrity.html).

### ADR-020 — Command receipts and stable field validation codes (2026-10-08)

The owner requested explicit JSON responses for successful operations. Commands previously
returning 204 now return HTTP 200 with `{ "success": true, "traceId": "..." }`. The shared
`ApiSuccess` factory uses the current activity ID or request trace identifier. Existing resource
responses and HTTP 201 creation responses retain their schemas and status codes. Errors remain
ProblemDetails. This changes the status/body contract for former 204 endpoints; clients must
accept 200, and API and SPA should ship together.

FluentValidation errors now also include `fieldCodes`, keyed by camel-case field names. This
lets the UI translate `PASSWORD_UNCHANGED` without matching English message text or displaying
arbitrary server details. The existing `errors` dictionary and `VALIDATION_FAILED` code remain.
Forgot-password responses keep the same receipt for known and unknown accounts.
