# 08 — Milestone handover

# Current state

Updated 2026-09-27. Branch: **feature/b2-video-and-polish**, cut from `main` at **0100606**.
Nothing on this branch is committed yet; the owner reviews and commits at each PART boundary.

**The app is live at https://moveyourass.gr.** An earlier version of this section said deployment
had not started; that was stale. See "Production, as deployed" below.

| Part | Status |
|---|---|
| A — Backblaze B2 video | **Code and docs complete; awaiting owner review, commit, and live acceptance** |
| B — cleanup (emails, Npgsql, AutoMapper, dead code) | Not started |
| D — auth and robustness | Not started |
| C — logo, navigation, responsive check | Not started |

## Production, as deployed (owner-verified)

- **Render**: Docker web service, Frankfurt, free plan, auto-deploy from `main`, health check `/health`.
- **Domain**: `moveyourass.gr` and `www.moveyourass.gr` both verified on Render, certificate issued.
  Cloudflare has `CNAME @` and `CNAME www`, both to the service's `onrender.com` hostname, both
  **DNS only**. **Render performs the www redirect, so there is no Cloudflare redirect rule.**
  docs/10 §1 used to describe one; it has been corrected to match what is deployed.
- **Database**: production migrations applied to the Neon `production` branch with the direct string.
- **Admin**: `--seed-admin` has been run; the trainer's Admin account exists and logs in on the
  live site. Dashboard, users and videos pages load.
- **Email**: `Email__From` on Render is currently the bare address `noreply@moveyourass.gr`
  with no display name. Changing it to the display-name form is an owner step in FINAL.
- **Keep-alive**: UptimeRobot is **not** used. The owner will create a cron-job.org job:
  every 10 minutes, 06:00–23:00 Europe/Athens, `/health` only.

### Two production gotchas, both already paid for once

- **`ASPNETCORE_ENVIRONMENT` is deliberately NOT set on Render.** A value in the dashboard
  overrode the Dockerfile's and stopped `appsettings.Production.json` from loading. Leave it unset.
- **`ConnectionStrings__Default` must be Npgsql `key=value` format, never a `postgres://` URL.**
  The URL form produced HTTP 500 on login.

### Security state

- The production database password was reset after an exposure.
- **The Resend API key has NOT been rotated and is still pending.** A key was exposed in a
  terminal transcript on 2026-09-26.

### Not verified in production

A real invitation email end to end, public registration and approval on the live site, and any
phone testing. No video has ever been uploaded in production.

## PART A progress

**Decision: Bunny Stream is removed** (ADR-019, written in this part). Video is Backblaze B2
through a provider-neutral S3-compatible adapter; Cloudflare R2 or MinIO would need only
different configuration values. `Video:Provider` remains as an explicit seam and accepts only
`S3`; any other value fails startup.

### Done and verified locally

- `IVideoStorage` replaced with an S3-shaped, provider-neutral contract
  (`src/Mya.Application/Abstractions/Video/`). `VideoStorageException` is the single failure type
  handlers catch, so no AWS type leaks out of Infrastructure.
- `S3VideoStorage` (`src/Mya.Infrastructure/Video/`) on `AWSSDK.S3` 4.0.103.4, with
  `ForcePathStyle`, `AuthenticationRegion`, and `RequestChecksumCalculation` /
  `ResponseChecksumValidation` set to `WHEN_REQUIRED`.
- **The multipart lifecycle is server-side.** The browser only PUTs parts to presigned URLs. The
  API creates the upload, and on completion calls `ListParts` itself to collect the part ETags.
  **The bucket therefore never has to expose `ETag` to script over CORS** — which matters,
  because Backblaze does not document `ETag` as an allowed `exposeHeaders` value.
- Presigned URLs follow the endpoint's scheme (`Protocol = Scheme`). Without this the SDK
  presigns `https://` even for an `http://` endpoint, which breaks every local container test.
- Upload flow: create (with declared content type and size) → presigned part URLs → browser
  PUTs → `complete` → server completes the upload, **HEADs the object**, compares size and
  content type against what was declared, and only then marks it `Ready`. A mismatch deletes the
  object and marks the row `Failed`.
- Resume is real, not session-only: the `UploadId` is persisted, so `POST {id}/upload` with the
  same file returns the parts the provider already holds and re-presigns the rest. A different
  file aborts the old upload first.
- Storage cap enforced **before** any URL is issued, counting drafts, because a draft's parts
  already occupy provider storage. Aborting or deleting a draft aborts its multipart upload.
- Accepts `video/mp4` and `video/quicktime` only. New codes: `VIDEO_FILE_TYPE`,
  `VIDEO_FILE_TOO_LARGE`, `VIDEO_STORAGE_FULL`, `VIDEO_UPLOAD_MISMATCH`.
- Playback is a presigned GET with the configured lifetime, default 2 hours. **A presigned link
  works for anyone holding it until it expires.** Accepted trade-off, recorded in ADR-019.
- Thumbnail: one presigned PUT is handed out with the upload ticket; the browser may store a
  captured frame there, and completion records it only if a HEAD confirms a plausible object
  (larger than 0 and at most 2 MB). No frame means the branded placeholder.
- Webhook removed: `VideoWebhookController`, the route `/api/webhooks/video-ready`, HMAC
  verification and `OwnsLibrary` are gone. `Refresh` now re-derives state from a HEAD.
- **Migration `20260926235417_VideoObjectStorage`** — purely additive, four nullable columns on
  `Video`: `UploadId`, `SizeBytes`, `ContentType`, `ThumbnailObjectKey`. Not applied anywhere yet.

### Angular

- `tus-js-client` removed, and with it the `url-parse` CommonJS allowlist entry in `angular.json`.
- `VideoUploadService` uploads one presigned part at a time with `XMLHttpRequest`: byte progress,
  five attempts per part with backoff, pause, resume and cancel. **A dropped connection costs the
  part in flight, not the recording.** Parts the server reports as already stored are skipped.
- `captureFrame` reads a poster frame and the duration from the selected file with a `<video>`
  and a canvas, capped at 640px and 8 seconds, returning nulls rather than throwing when the
  browser cannot decode the recording.
- `player.component.ts` plays a native `<video controls playsinline>` with `controlsList="nodownload"`.
  The iframe and the `DomSanitizer.bypassSecurityTrustResourceUrl` call are gone.
- Admin video screen: a **storage-used bar** against the cap with a warning band from 80%, the
  file limit read from the API rather than hardcoded, and MP4/MOV-only file selection with Greek
  messages that name the iPhone "Most Compatible" setting.
- New Greek messages for every video error code in the error interceptor, keyed by `code`.
- Cards get a presigned poster URL inline in the list response — presigning is a local signature,
  so a page of twelve cards costs no extra request and no provider round trip.

### Docs

ADR-019 written; ADR-018 marked superseded with its surviving requirements listed; ADR-017 given
a note that plain object storage is now the video choice. Rewritten or corrected: `docs/06` §3–6,
`docs/11` (entirely — B2 account, bucket, scoped key, three ways to apply CORS, settings, free
limits, the acceptance list, recovery), `docs/12` (iPhone "Most Compatible", 720p/1080p guidance
with per-minute sizes, upload and resume behaviour, link-expiry wording), `docs/10` (**the
Cloudflare redirect rule that does not exist**, the cron-job.org keep-alive with its night-gap
trade-off, `Video__S3__*` env vars, the two production gotchas, the migration command as the
ongoing upgrade path), plus `CLAUDE.md`, `README.md`, `docs/01`, `docs/04`, `infra/README.md`
and `docs/backlog.md`.

### Evidence

`dotnet build -c Release`: **0 warnings, 0 errors**. `dotnet test -c Release`: **55 passed**
(16 unit, 3 architecture, 36 integration), up from 29 on `main`. Angular: **lint clean**,
**8 tests passed**, production build succeeds.

Frontend tests cover the uploader directly against a stubbed `XMLHttpRequest`: one PUT per part
with correct slice sizes, skipping parts the provider already holds, **retrying only the part
that failed**, giving up after five attempts with the expired-link message, and stopping on
pause. Timers are faked, so the retry schedule costs no wall-clock time (the suite went from 47s
to under 4s).

The adapter is covered against a **real S3 implementation** via Testcontainers
(`S3VideoStorageMinioTests`, 7 tests): a two-part multipart round trip through presigned PUTs,
`ListParts`, complete, HEAD, presigned GET including a **206 range request** (the native player
needs it to seek), presigned PUT for the poster frame, abort, idempotent delete, and completing
with no parts failing rather than creating an empty object. They run with the same checksum
settings production uses.

**MinIO's own `minio/minio` Docker Hub repository now requires authentication** and cannot be
pulled anonymously, so the test uses `chainguard/minio:latest`, which is public and equivalent.

### Not verified

No Backblaze account, bucket, key or CORS rule has been touched by this agent. Nothing has been
uploaded to B2. MinIO passing is evidence about the adapter, not about B2: B2's own handling of
SDK checksum headers, path-style addressing and presigned part URLs is **unverified**, and that
is exactly what the live acceptance run in docs/11 exists to prove.

Also unverified: **no browser has run this code**. The upload flow, the poster-frame capture and
the native player are covered by unit tests and by the adapter's container tests, not by a real
page. The full browser pass happens in PART C, and the real-device pass is docs/11 §6.

### Exact next step

**Owner:** review and commit PART A. Then, when you want to run live acceptance:

1. Create the Backblaze bucket, the bucket-scoped key and the CORS rule — docs/11 §1–3.
2. Set the `Video:S3:*` user-secrets — the exact commands with placeholders are in docs/11 §4.
3. Start the API against the Neon **dev** branch and upload a real phone recording, following
   the docs/11 §6 list. Report anything B2 does differently from MinIO, especially a 400
   mentioning `x-amz-checksum-…`, which would mean the checksum setting needs revisiting.

**Agent, after that commit:** PART B — branded HTML and plain-text emails with a PNG logo
generated from `web/src/assets/brand/logo-horizontal.svg`, the Npgsql GSSAPI log noise, the
AutoMapper Community licence documentation, and removing the `MEDIATR_LICENSE_KEY` references
(MediatR is not referenced anywhere; ADR-002 rejected it).

## Settings and boundaries

| Setting | Where |
|---|---|
| ConnectionStrings:Default | local user-secrets: Neon dev pooled; direct override for migration/seed |
| ConnectionStrings__Default | Render: production pooled, **Npgsql key=value format, not a URL** |
| Jwt:SigningKey, Jwt:Issuer, Jwt:Audience | user-secrets / Render environment |
| Seed:AdminEmail, Seed:AdminPassword, Seed:AdminFirstName, Seed:AdminLastName | user-secrets / one-shot seed environment |
| Email:Mode, Email:ApiKey, Email:From | user-secrets / Render environment |
| App:PublicOrigin | user-secrets / App__PublicOrigin on Render |
| Video:Provider | `S3`; anything else fails startup |
| Video:S3:Enabled | user-secrets / Video__S3__Enabled |
| Video:S3:ServiceUrl | the bucket's B2 S3 endpoint; absolute HTTPS, no path |
| Video:S3:Region | the region inside that endpoint hostname |
| Video:S3:AccessKeyId, Video:S3:SecretAccessKey | **secret** — user-secrets / Render |
| Video:S3:BucketName | user-secrets / Video__S3__BucketName |
| Video:S3:MaxFileBytes | optional, default 2 GiB |
| Video:S3:StorageCapBytes | optional, default 9 GiB, under B2's 10 GB free tier |
| Video:S3:PartSizeBytes | optional, default 16 MiB |
| Video:S3:PlaybackMinutes | optional, default 120 |
| Video:S3:UploadMinutes | optional, default 360 |
| PORT, RENDER | Render-provided platform environment |
| ASPNETCORE_ENVIRONMENT | **do not set on Render** — see the gotcha above |

No secret value appears in this repository, in these documents, or in any conversation.

Keep `/health` database-free, the outbox event-driven, keepalive and EF retry strategies off.
Console email is Development-only. Creation still reserves a unique creator/key before any
provider call. Deleting leaves an unpublished `Deleting` row on provider failure so an admin can
retry safely. See docs/06 for concurrency, ordering, link expiry and idempotency boundaries.

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
