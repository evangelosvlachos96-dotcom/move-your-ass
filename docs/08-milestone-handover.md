# 08 — Milestone handover

# Current state

Updated 2026-09-28. Branch: **feature/final-polish**, cut from `main` at **8fa9b53** (the
B2/auth/UI merge). Nothing on this branch is committed yet.

The previous round (B2 video, branded emails, auth hardening, shell and navigation) is merged
and described further down; this section now tracks the final round.

**The app is live at https://moveyourass.gr.** An earlier version of this section said deployment
had not started; that was stale. See "Production, as deployed" below.

| Part | Status |
|---|---|
| A — Backblaze B2 video | **Done, committed, and live-acceptance PASSED against the real bucket** |
| B — cleanup (emails, Npgsql, AutoMapper, dead code) | **Done** — 69 backend tests green |
| D — auth and robustness | **Done** |
| C — logo, navigation, confirm dialog, responsive check | **Done** |

**Regression as of 2026-09-28, final round:** `dotnet build -c Release` **0 warnings / 0
errors** · `dotnet test` **145 passed** (16 unit, 3 architecture, 126 integration) ·
`npm run lint` clean · `npm test` **33 passed** · production `npm run build` succeeds ·
Playwright **160 passed, 5 skipped, 0 failed** across five projects (Chromium at phone, tablet
and desktop; WebKit at phone and tablet) and both roles. The skips are conditional: tests that
need a video in the library, on a database that has none.

## FINAL ROUND scope (owner, 2026-09-28) — branch `feature/final-polish`

Recorded so another agent can resume from the repo alone.

1. **Custom video cover** — admin uploads JPG/PNG/WebP up to 5 MB, resized client-side to 1280px,
   presigned PUT to B2, HEAD-verified. Priority **cover > auto-captured frame > placeholder**.
   Old objects deleted on replace and on video delete. Thumbnails count towards the storage cap.
2. **About / contact page "Ο γυμναστής σου"** — nav item for every signed-in user; admin-editable
   in place. Trainer photo, name, tagline, restricted-markdown bio, and optional contact methods
   (email, phone, Instagram, YouTube, TikTok, Facebook, WhatsApp, website) each with an SVG icon.
   Client contact form emailing the trainer via the outbox with Reply-To set to the client.
   Single-row settings table with optimistic concurrency.
3. **Footer** on every page including auth: "Created by Evangelos Vlachos" linking to the GitHub
   profile in a new tab with `rel="noopener noreferrer"`, plus the year. Above the phone bottom
   bar, never behind it, safe-area aware.
4. **UI fixes** — equal internal spacing on every dashboard card via shared tokens; upload
   availability for an admin created after startup; a WebKit Playwright project at phone and
   tablet; a full responsive re-review.
   - **4A Chrome autofill styling.** Picking an email from Chrome's autofill gives the input a
     light background and a different-looking font. Override `:-webkit-autofill` / `:autofill`
     app-wide with an inset box-shadow in the field's own background, matching
     `-webkit-text-fill-color`, `caret-color`, font family/size/weight, and a long
     `background-color` transition to suppress the flash. Verify in Chromium **and** WebKit.
   - **4B Token links while another session is active.** Reported: signed in as admin, opened an
     invitation link in the same browser, set the client password, pressed login — and landed in
     the **admin** dashboard. Opening `/set-password` or `/reset-password` with any active session
     must first sign that session out server-side (revoking the refresh cookie), clear client
     state, tell other tabs over the existing BroadcastChannel, and show a short Greek notice.
     After success it must never resume the earlier session. Tested end to end.
   - **Profile button icon** must be exactly centred in its circular button at every viewport, and
     **always the generic account icon** — never the trainer photo. The trainer photo appears only
     on the About page, as a large circle.
5. **Email live verification** — an admin-only "Αποστολή δοκιμαστικού email" action, rate-limited;
   then a live send of every template from the owner's machine with `Email:Mode=Resend`.
6. **Public-repo hardening** — gitleaks over full history plus a CI job, workflow audit, actions
   pinned to SHAs, SECURITY.md, no LICENSE (all rights reserved, stated in the README).
7. **Final handover** — README, trainer guide additions, ordered owner steps, backlog, one commit
   message.

## FINAL ROUND progress (2026-09-28)

Items 1, 2, 3, 4 and 6 are **done**. Item 5 is built but needs the owner for the live half.
Item 7 is this section plus the README, SECURITY.md and the trainer guide.

### 1 - Custom video cover - done

`Video` gained `CoverObjectKey` / `CoverSizeBytes` and `ThumbnailSizeBytes` (migration
`VideoCoverAndSiteContent`). The admin uploads JPG, PNG or WebP up to 5 MB; the browser resizes
to 1280px on the long edge before the presigned PUT, and the server HEAD-verifies the object
before recording it. The object key carries a random suffix, so replacing a cover can never be
served from a cached URL. Resolution order is **cover -> frame captured at upload -> branded
placeholder**, decided in one place (`VideoContracts.Map`). Replacing or removing a cover, and
deleting a video, delete the old objects. Every byte - video, frame and cover - counts against
the storage cap. 19 tests in `tests/Mya.Api.IntegrationTests/Videos/VideoCoverTests.cs`, which
include the two rules that are easy to get wrong: a confirm naming another video's object key is
refused, and an object whose verified content type is wrong is deleted rather than adopted.

### 2 - "Ο γυμναστής σου" - done

A single-row `SiteContent` table with a fixed id and a `Revision` concurrency token, so two admin
tabs cannot silently overwrite each other. Every signed-in user can read it; only an admin can
write it. Reading does **not** create the row.

The biography is markdown, rendered by `web/src/app/shared/text/safe-markdown.ts`, which escapes
first and then applies a whitelist (paragraphs, `**bold**`, `- ` lists). No markdown library is
used: they all support raw HTML, and this field is rendered to every client. Contact links are
validated https-only, server-side, and drawn with inline SVG rather than emoji so they stay crisp
and keep an accessible label.

The contact form queues one outbox message per recipient - the configured contact address, or
every admin if none is set - with `Reply-To` set to the client, so the trainer just presses reply.
It is rate-limited per user and rejects a duplicate of the same subject and body within ten
minutes (a SHA-256 fingerprint stored on the outbox row). 22 tests in
`tests/Mya.Api.IntegrationTests/Site/SiteContentTests.cs`.

### 3 - Footer - done

`app-site-footer` renders after `<main>` on every page, auth pages included. On phones the shell
publishes the bottom bar's height as `--app-bottom-inset` and the footer takes it as a *margin*,
not padding, so the footer's own box ends above the bar rather than merely its text. The
safe-area inset is added on top. Asserted at phone size in both engines, after scrolling to the
bottom - measuring it unscrolled proves nothing, since the footer is simply below the fold.

### 4 - UI fixes - done

- **Card spacing** now comes from `web/src/styles/_spacing.scss` tokens, with every dashboard card
  restructured as title / body / actions. The original bug was a `margin: 0` on one card's text.
- **`videos.scss` split.** One stylesheet was shared by the library, the player and the admin
  screen, and the cover editor pushed it past Angular's component-style budget - charging all
  three components for CSS only one of them renders. The admin-only rules moved to
  `admin-videos.scss`; the shared file is back under budget and the production build is warning
  free again.
- **Upload availability** - an admin created after startup can upload immediately, and an admin
  who must change their password is told why in Greek rather than shown a dead button. Both are
  tested (`VideoWorkflowTests`).
- **Autofill (4A)** - `:-webkit-autofill` / `:autofill` overridden app-wide in `styles.scss` with
  an inset box-shadow in the field's own background colour, matching `-webkit-text-fill-color`
  and `caret-color`, inherited font metrics, and a `background-color` transition long enough to
  suppress the flash. `web/e2e/autofill.spec.ts` checks the computed style in both engines and
  asserts the rule is actually present in the stylesheet.
- **Token links (4B)** - `signOutForTokenGuard` runs before `/set-password` and `/reset-password`,
  calls `logoutQuietly()` (which revokes the refresh cookie server-side), clears client state,
  announces the sign-out over the BroadcastChannel so other tabs return to login, and raises a
  short Greek notice on the page. `web/e2e/token-links.spec.ts` asserts the old session is gone
  by checking `/api/auth/refresh` returns **401**, not merely that the page looks logged out.
- **Profile button** - pinned to a 44px circle (48px on phones) with the icon as its own flex
  centre and every Material metric fixed to one number. Measured at 4x zoom: gaps are 7px on all
  four sides at phone size and 8px on all four at tablet and desktop. It always shows the generic
  account icon; the trainer photo appears only on the About page.

### 4 - WebKit, and the bug it found

Adding WebKit projects paid for itself on the first run. Seven tests failed with the app sitting
on `/login` while the server still considered the session valid.

**The cause was `RefreshCookie`: `Secure = true` unconditionally.** Chromium exempts
`http://localhost` from the Secure-cookie rule and sends the cookie anyway; **WebKit does not.**
It stored the cookie and then never sent it back, so every refresh returned 401 and the app
signed itself out on the next page load - in Safari, and only in Safari. `Secure` now mirrors the
request scheme **in Development only**, and is unconditionally true everywhere else, so a
forwarded-headers mistake behind Render's load balancer still cannot put the session token on the
wire in the clear. `tests/Mya.Api.IntegrationTests/Auth/RefreshCookieTests.cs` pins all of it,
including `HttpOnly`, `SameSite=Strict` and the `/api/auth` path.

**This was a development-only defect.** Production is https, so no real client was ever affected.
Two false starts on the way, recorded because they cost time: it is *not* a cross-origin or
`SameSite` problem (development now proxies `/api` through the Angular dev server, which is right
for other reasons but changed nothing here), and it is *not* `navigator.locks` or
`BroadcastChannel` behaving differently in WebKit.

### 6 - Public-repository hardening - done

- **gitleaks over the full history of every branch: 18 commits, ~1.38 MB, no leaks.** Re-run at
  the end of this round.
- **A working-tree scan finds two hits, both in `.vs/`** - Visual Studio's local IIS Express
  `sessionKey`s. `.vs/` is gitignored (`.gitignore:71`) and has never been tracked.
- **`.github/workflows/security.yml`** runs gitleaks on every push, every pull request (forks
  included), weekly, and on demand, with `fetch-depth: 0` so it walks history rather than a single
  commit.
- **Every third-party action is pinned to a commit SHA** with the tag in a trailing comment,
  across all five workflows.
- **Every workflow now declares `permissions: contents: read`.** `api.yml` and `web.yml` had no
  block at all and were inheriting the repository default.
- **No workflow uses `pull_request_target`**, so no fork pull request can reach a secret or write
  to the repository. The only secret referenced under a `pull_request` trigger is
  `AUTOMAPPER_LICENSE_KEY`, which GitHub does not supply to fork pull requests; the build simply
  runs without it.
- **`backup.yml` was already correct** - it pipes `pg_dump` straight into `gpg`, so no plaintext
  dump ever exists on the runner, and it refuses to upload an implausibly small file.
- **`SECURITY.md` added**: how to report privately, what is in and out of scope, what this
  repository deliberately does not contain, and the two accepted risks (presigned playback URLs
  work until they expire; two end-to-end refresh cookies remain in history).
- **No `LICENSE` file, deliberately** - all rights reserved, stated in the README.

### 5 - Email - half done

Built and tested: an admin-only `POST /api/admin/site/test-email`, rate-limited per user, wired to
a dashboard button ("Αποστολή δοκιμαστικού email"). The contact-message and test-email templates
are covered by `EmailTemplateTests`, including `Reply-To` and the escaping of a subject and body
the client wrote. The email header logo points at `https://moveyourass.gr` and carries alt text,
so a client who blocks images still sees the brand name rather than a broken box.

**Not done, and only the owner can do it:** an actual send through Resend, and looking at the
result in a real inbox. See the owner steps below.

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

### Live acceptance on the real bucket — PASSED, owner-verified 2026-09-27

Run by the owner against the **real Backblaze B2 bucket** `mya-videos-gallery`
(`s3.eu-central-003.backblazeb2.com`, region `eu-central-003`), with the app running locally
against the Neon **dev** branch. This is the evidence the MinIO tests could not provide.

| Checked | Result |
|---|---|
| Real phone recording, `vid2.mp4`, 44 MB | Uploaded as **3 presigned multipart parts, all HTTP 200** |
| Poster frame | Captured client-side and uploaded |
| `POST /upload/complete` | **204** — server completed, HEAD-verified, status **Έτοιμο** |
| Storage bar | Showed **44 MB** used, then **0 KB** after deletion |
| Admin preview playback | Works, **including seeking** (the HTTP range path) |
| Publish | Works |
| Delete | Removed the object; storage returned to 0 |

**What this settles, that was previously marked unverified:**

- **B2 accepts the AWS SDK v4 requests as configured.** No `x-amz-checksum-…` rejection occurred
  with `RequestChecksumCalculation`/`ResponseChecksumValidation` at `WHEN_REQUIRED`.
- **Path-style addressing works against B2** (`ForcePathStyle = true`).
- **Presigned multipart part URLs work from a browser**, and the CORS rule with `PUT`/`GET`/`HEAD`
  from `http://localhost:4200` is sufficient.
- **B2 accepts `ExposeHeaders: ["ETag"]`** via `PutBucketCors`, returning it lowercased. The
  design does not depend on it — the API reads part ETags with `ListParts` — but the CORS
  obstacle that shaped the design turns out not to exist. ADR-019 records this.
- **Range requests serve seeking from a presigned GET**, so the native player scrubs correctly.

The Backblaze web console **cannot** create this CORS rule: it offers only presets, at most one
origin, and no choice of operations. It also does not display API-created rules, so the
`get-bucket-cors` read-back is the only confirmation. Applied with the AWS CLI; docs/11 §3 has
the exact commands.

### Still not verified

- **Nothing has been uploaded in production.** This run was local, against the Neon dev branch.
  The production `Video__S3__*` variables are not set and the `VideoObjectStorage` migration has
  not been applied to the Neon `production` branch.
- **No phone or tablet has loaded the site**, for video or anything else. Playback was checked in
  a desktop browser. Mobile Safari is the case that matters most and is untested.
- **An HEVC / "High Efficiency" recording has not been tried.** `vid2.mp4` was H.264. The docs
  tell the trainer to use "Most Compatible"; what actually happens with an HEVC `.mov` — it will
  upload, since the type is `video/quicktime`, but may not decode in some browsers — is unproven.
- Egress behaviour and the B2 usage dashboard figures have not been observed over time.

### Exact next step

**Owner:** review and commit PARTS B, D and C, then work through "Owner steps for production"
above. Nothing on this branch is committed.

## PART B — done

- **Branded emails.** `EmailMessage` now carries `Text` *and* `Html`; Resend receives both.
  `EmailContent` (heading, paragraphs, optional CTA, optional note) is rendered into **both**
  bodies by `EmailLayout`, so the plain-text alternative cannot drift from the HTML — the usual
  failure of hand-maintaining two copies. Tables with inline CSS; brand colours from
  `_brand.scss`; Greek copy; a lime CTA button with dark text on it, repeated as a bare URL for
  clients that strip links; preheader text; footer.
- **Logo is a PNG** at `web/public/email-logo.png`, rendered from the Option A design with
  Playwright at 3× (245×70 CSS px). **Gmail refuses SVG**, so the app's SVG marks cannot be used.
  Its background matches the email background, or it shows as a pale box.
- **Escaping is tested**: a name containing `<script>` is HTML-encoded in the HTML part and left
  literal in the text part, which is correct for each.
- **Email preview**: set `MYA_EMAIL_PREVIEW_DIR` and run the suite to dump every template as
  `.html` and `.txt`. Automated assertions cannot tell you an email is ugly. Documented in docs/09.
- **Npgsql GSSAPI noise — root cause found, not suppressed.** Npgsql 10 changed its default
  `GssEncryptionMode` to `Prefer`, and Microsoft's .NET runtime images have not shipped `libkrb5`
  since .NET 8. Every connection therefore attempted Kerberos and **threw**, which under load has
  been reported to produce exception storms — not merely a log line. `AddInfrastructure` now sets
  `GSS Encryption Mode=Disable` on the connection string it builds, unless one is already
  specified. Chosen over installing `libgssapi-krb5-2`: Neon uses TLS with password auth and
  there is no Kerberos realm anywhere here, so the library would be shipped and patched forever
  for a mechanism that can never be used. Set in code so a credential rotation cannot lose it.
- **AutoMapper Community licence** documented in docs/10 — where to get the free key and the
  `AUTOMAPPER_LICENSE_KEY` variable on Render. **No key added to the repo.**
- **`MEDIATR_LICENSE_KEY` references removed** from CLAUDE.md. MediatR is referenced nowhere;
  ADR-002 rejected it.
- **Stale docs corrected**: docs/02 said video was "planned, not implemented" and that
  `IdempotencyRecord` was retained for it; docs/07's video section still described signed
  webhooks and protected embeds.

## PART D — backend done

**89 backend tests green** (16 unit, 3 architecture, 70 integration), 0 warnings.

- **Two tabs no longer log each other out.** Rotation was already atomic (one conditional
  `UPDATE`), so the missing piece was a **30-second grace window**: presenting a token that was
  just rotated returns a fresh *access* token for the same session and rotates nothing, leaving
  the cookie the winning tab already set. Outside the window it is still reuse and still revokes
  the family. A grace replay cannot resurrect a session that logout, suspension or a newer login
  ended — tested for each.
- **Forgotten password.** `POST /auth/forgot-password` always answers 204, whether or not the
  address exists; `POST /auth/reset-password` consumes a single-use link. `PasswordInvitation`
  gained a `Purpose` discriminator so **an invitation token cannot be spent as a reset** and vice
  versa. Reset links last 1 hour against an invitation's 24. A successful reset ends **every**
  session — someone recovering an account may be locking an intruder out.
- **Lockout replaced, not tuned.** Identity account lockout is **off**. Counting failures per
  account and locking it hands the internet a denial of service: anyone who knows the trainer's
  email could lock her out repeatedly without ever guessing a password. Instead the credential
  endpoints are throttled **per (email, IP)** — 5 per 15 minutes — so one attacker spends only
  their own budget, with a **per-email backstop of 50/hour across all IPs** as the global limiter
  for distributed attempts. The password policy is what makes guessing impractical.
- **Role changes.** An admin can never change their own role (`CANNOT_MODIFY_SELF`); the
  last-admin rule still holds; a role change **ends the affected user's session and revokes their
  refresh tokens**. For the gap before their next refresh, the `AdminOnly` policy now carries
  `CurrentAdminRequirement`, which **re-reads role, status and session from the database on every
  admin request** — so a demoted admin loses admin access on the very next request rather than
  when their 15-minute token expires. Scoped to admin endpoints deliberately: doing it for every
  authenticated request is the broad "instant JWT revocation" item the backlog defers, and admin
  traffic is one person.
- **Races, tested against real PostgreSQL** via Testcontainers (`AdminRaceTests`), because SQLite
  serialises writes and would have proved nothing. Two DbContexts, two transactions, READ
  COMMITTED. Concurrent duplicate registration, one invitation link submitted twice, two admins
  approving the same user, approve versus decline, approve versus delete.
- **That last one found a real bug.** Approving while deleting left "your account was approved"
  queued to an account that no longer existed. `OutboxMessage` gained a nullable `SubjectUserId`
  and a hard delete now withdraws that user's unsent messages in the same transaction. The
  admin's new-registration notice deliberately carries no subject, so deleting the registrant
  does not withdraw the admin's own notification.
- **Indexes reviewed with `EXPLAIN (ANALYZE)` on seeded PostgreSQL, not by reading code.** One
  genuine gap: `RefreshToken.TokenHash` had **no index at all** despite being looked up on every
  refresh — Seq Scan, 7.2 ms, 19,999 rows discarded at 20k rows, against 0.17 ms with a unique
  index. Added. The outbox partial index and the video/library indexes already covered their
  queries. A trigram index for the admin user search was considered and **rejected** with the
  reasoning written into docs/02.
- **Encrypted backups.** `.github/workflows/backup.yml` runs weekly and on demand: `pg_dump` from
  a `postgres:17-alpine` container piped **straight into `gpg` AES-256** so no plaintext file is
  ever written, uploaded as a 30-day artifact. **The repository is public**, so this is the whole
  point. A dump under 2 KB fails the job. Secrets and a **tested** restore procedure are in
  docs/10.

Three additive migrations: `PasswordCredentialPurpose`, `OutboxSubjectUser`, `RefreshTokenHashIndex`.

### PART D — frontend, done

- **Cross-tab single-flight refresh.** `SessionSyncService` serialises refreshes on a Web Lock and
  shares the resulting access token over `BroadcastChannel`; a second tab reuses a token less than
  ten seconds old instead of presenting the same cookie again. Degrades to the previous per-tab
  behaviour where `navigator.locks` is missing or refuses the request — which is what the server's
  grace window is for. Six tests, including a lock stand-in that really serialises.
- **Forgot/reset password screens**, with "Ξέχασες τον κωδικό;" on the login page. The
  confirmation says a link was sent *if the address belongs to an account*, and says it whatever
  happened. The token travels in the URL fragment, so it stays out of server logs and referrers.
- **Role-change confirmation** naming the user and the old → new role, destructive styling on
  demotion, and the role control disabled on the admin's own row.

## PART D scope, as agreed (owner, 2026-09-27)

Recorded here so it is not lost with the conversation. PART D runs after the B2 live acceptance
and after PART B.

1. **One active session per user stays the rule.** A new login ends every other device's session.
   Prove it with tests, and on the logged-out device show "Συνδέθηκες από άλλη συσκευή" on its
   next action rather than a generic error. (`SESSION_SUPERSEDED` already exists and the error
   interceptor already maps it — verify, then test.)
2. **Two tabs on the same device must not log each other out.** Today two simultaneous refreshes
   with the same cookie look like token reuse and revoke the family. Server: a ~30 s grace window
   in which the just-rotated token still returns a valid session, and atomic rotation (row lock or
   concurrency token) so two concurrent refreshes cannot both rotate. Real reuse outside the
   window still revokes. Client: single-flight refresh **across tabs** via the Web Locks API with
   `BroadcastChannel` to share the result, degrading gracefully where unsupported.
3. **Forgot password.** "Ξέχασες τον κωδικό;" on the login page; a single-use expiring emailed
   link, reusing the invitation-token mechanism if it fits. **The same response whether or not the
   address exists** — no user enumeration. Rate-limited. A successful reset ends all sessions.
   Branded email per PART B.
4. **Lockout hardening.** Today anyone who knows the trainer's email can lock her out for 15
   minutes with wrong passwords. Propose and implement something safer — per-email-plus-IP
   throttling with progressive delay instead of a hard account lock, and/or an admin-clearable
   lockout — and write down the trade-off.
5. **Role changes (added by the owner, 2026-09-27).**
   - **An admin can never change their own role.** Enforced server-side with a clear Greek error;
     the UI disables or hides the role control on the admin's own row. The existing last-admin
     protection stays.
   - **Every role change needs a confirmation dialog** naming the user and the old → new role,
     e.g. "Αλλαγή ρόλου του Γιώργου από Πελάτης σε Διαχειριστής;". Audit the other sensitive admin
     actions — delete, suspend/reactivate, decline, password reset, video delete — and confirm
     each has a dialog too.
   - **A role change takes effect on the next request, not when the access token expires.** End
     the affected user's session on role change so their refresh fails and they must sign in
     again, **and** make admin authorization robust against a stale token (re-check role or
     session on admin endpoints). Pick an approach, justify it, and test that a demoted admin
     loses admin access on the very next request.
6. **Double-submit and race coverage**, integration-tested against PostgreSQL where the behaviour
   depends on the database: concurrent duplicate registrations; one invitation link used twice
   concurrently; two admins approving/declining the same user; approve versus delete; a
   disabled/deleted user's existing access and refresh tokens; expired and reused invitation and
   reset links; clients never reaching admin endpoints or unpublished videos; rate limits on
   login, register and forgot-password. Every submit button disables while its request is in
   flight.
7. **Backups.** A weekly GitHub Actions workflow, plus manual trigger, that `pg_dump`s the Neon
   **production** branch using a direct string from a GitHub secret and **encrypts the dump with
   a passphrase secret before uploading it** as a 30-day artifact. **The repository is public, so
   an unencrypted dump must never be uploaded.** Document the secrets to create and a *tested*
   restore into the Neon dev branch or a fresh branch, in docs/10.
8. **Indexes.** Check the existing indexes actually cover the real queries — the users list
   filters and search, the video library filters, the outbox claim, refresh-token lookups. Add
   only what an `EXPLAIN` on the dev branch shows is missing, and write the reasoning into
   docs/02.

## PART C — done

- **One logo, one component.** `BrandLogoComponent` draws Option A as inline SVG plus text, so the
  wordmark inherits the page font and the accent is the brand token. Used in the sidebar, the
  phone top bar and the auth pages. **Every other variant is gone**: the stacked lockup (whose SVG
  text started at `x=8`, which is why it always looked shoved left) and the spaced-capitals
  "MOVE YOUR ASS / ΔΙΑΧΕΙΡΙΣΗ" eyebrows on the admin and library pages. A Playwright test asserts
  exactly one `app-brand-logo` per screen.
- **The shell is one frame.** Sidebar and top bar share `--brand-surface` and a single 1px
  `--brand-border`; the sidebar's logo area is exactly the top bar's height, so their bottom edges
  meet across the corner with no step. The rounded sidebar corner is gone, and so is the **green
  crescent** — the old active-item pill was clipped by the sidebar edge and rendered as a floating
  arc. Active is now lime text and icon on a subtle fill.
- **Navigation redesigned.** Phone: a bottom bar, four destinations, icon plus label, safe-area
  aware, 62px tall, lime when active, **no hamburger**. Tablet and desktop: a sidebar (208px /
  244px). A slim icons-only tablet rail was built and **dropped**: view encapsulation meant the
  shell could not hide the wordmark inside the logo component, so it clipped mid-word, and the
  narrow column wrapped "Προπονήσεις" across two lines.
- **The top bar carries the page title** on tablet and desktop, and the centred wordmark on phones,
  with the profile menu on the right.
- **One confirmation dialog.** `ConfirmDialogService.confirm()` returns `Promise<boolean>`.
  Branded dark surface, Greek, title/message/detail/labels, a destructive variant with a red
  confirm, focus trapped by the CDK overlay, Escape and backdrop cancel, **Cancel focused for
  destructive actions**, and a guard so a double tap cannot answer twice. Centred card on
  desktop, full-width sheet at the bottom on phones with 48px buttons.
- **Every native dialog is gone** — five `window.confirm` calls replaced, plus new confirmations
  for logout and role changes. An ESLint `no-restricted-globals` rule now fails the build if
  `confirm`, `alert` or `prompt` is reintroduced; **verified by reintroducing one and watching
  lint fail**. A Playwright test fails if a native dialog ever appears.
- **Playwright smoke suite** in `web/e2e/`, three viewports × both roles, **66 tests**: horizontal
  overflow (naming the widest offending element), console errors, one logo per screen, bottom bar
  versus sidebar per breakpoint, every nav target ≥44px, and the branded dialog appearing for a
  destructive action. Screenshots are **not** committed; `test-results/`, `playwright-report/` and
  `.pw-tmp/` are gitignored.

### Three things the suite taught us about the app

1. **`networkidle` never settles** against the Angular dev server, which holds connections open
   for live reload. Waiting for it turned a 6-second suite into 10 minutes. The suite waits for a
   rendered element instead.
2. **Signing in per test throttles itself.** Five attempts per (email, IP) per fifteen minutes is
   the rule from PART D, and the suite met it. It now signs in **once per project per role** —
   three logins per account per full run — and reports a 429 with an explanation rather than a
   bare navigation timeout. Two full runs back to back will still meet the limit; restart the API
   to clear it, since the limiter is in memory.
3. **A saved `storageState` is single-use.** Refresh tokens rotate, so replaying one saved cookie
   across three projects is reuse, and the family gets revoked — correctly. Each project now signs
   in and keeps **one browser context** for that role, so the cookie rotates in place as it does
   in a real browser. `browser.newContext()` also does not inherit the project viewport; without
   passing it explicitly every viewport assertion silently ran at 1280×720.

## PART C scope, as agreed (owner, 2026-09-27)

1. **Logo, "Option A".** `MoveYourAss` on one line, weight 600, `Ass` in the lime accent,
   **always horizontally centred**. Phone (<600px): wordmark only. Tablet and desktop (≥600px):
   lime chevron mark then wordmark, centred as one group. **One reusable component, used
   everywhere.** The old stacked logo leans left because its SVG text starts at `x=8`.
2. **One logo everywhere — delete every other variant.** The brand currently appears twice in two
   styles: the small mark-plus-wordmark in the shell, and a spaced-capitals "MOVE YOUR ASS"
   eyebrow just below it. The spaced-capitals form, the stacked form and any other variant go,
   wherever they appear — shell, sidebar, page headers, auth pages, emails. **If a page header
   shows the brand name as decoration, remove it.** The brand appears once per screen.
3. **Shell frame, as a whole — not just the logo.** Reported problems: sidebar and top bar are two
   different dark shades; their edges do not align, leaving a notch where the sidebar top meets
   the top bar; and a visible seam or extra strip runs between sidebar and content.
   - Define shell surfaces **once as tokens** (app background, frame surface for sidebar and top
     bar, card surface, border) and use only those. No one-off shades.
   - Sidebar and top bar share **one surface**, forming a continuous L-shaped frame. No steps, no
     notches, pixel-aligned edges. **At most one subtle 1px border** between frame and content,
     used consistently. No double borders, no stray strips, no box-shadow seams.
   - **Top-bar height equals the sidebar's logo-area height** so their bottom edges line up.
   - Content area on the app background, cards on the card surface, consistent outer padding per
     breakpoint.
   - Verify at 390 / 820 / 1440, **while scrolling** (a sticky top bar must not open a gap), and
     collapsed/expanded if that state exists.
4. **Logo placement and sizing.** Desktop/tablet: logo at the **top of the sidebar**, aligned with
   the nav items' left edge. Mark ~32px, wordmark ~20px, vertically centred, **16–24px minimum
   from edges** — no cramped corners. The top bar then holds only the page title on the left and
   the profile menu on the right. Phone: top bar shows the wordmark only, centred, profile icon
   right; navigation is the bottom bar.
5. **Navigation redesign.** No hamburger, no rounded green pill marker on the left of nav items.
   - Phone: **bottom navigation bar**, icon plus short label — Πίνακας, Προπονήσεις, and for
     admins Βίντεο and Χρήστες with the pending badge. Safe-area aware, ≥44px targets, active
     item shown by lime icon and label.
   - Tablet/desktop: slim left sidebar. Active item by lime text and icon plus a subtle highlight
     or thin underline.
   - Profile/logout menu stays top right, large icon on phones.
6. **One shared confirmation dialog replaces every native browser dialog.** A component plus
   `ConfirmDialogService.confirm({...})` returning a promise/observable of boolean.
   - Branded: dark surface, brand colours and typography, rounded corners, Greek text.
   - Options: title, message (may name the affected user or video), confirm label, cancel label,
     and a **destructive** variant with a red confirm button for delete, disable, decline and
     demotion.
   - Accessible: focus moves into the dialog and is **trapped**; Esc and backdrop click cancel;
     **default focus on Cancel for destructive actions**; proper aria labelling.
   - Responsive: centred on desktop/tablet; on phones full-width near the bottom with ≥44px
     buttons.
   - The confirm button shows a loading state and **prevents double clicks while the action runs**.
   - Used for: role changes, user delete/disable/decline, password reset, video delete, video
     publish/unpublish, cancelling an in-progress upload, logout, and anything else currently
     using a browser dialog. **Grep for `confirm(`, `alert(` and `prompt(` so none remain, and
     add a lint rule or test that fails if one is reintroduced.**
   - Tests for the component and the service, plus a Playwright check that a destructive action
     shows the dialog at all three viewports.
7. **Responsive pass with Playwright.** Every route, both roles, at 390×844, 820×1180 and
   1440×900. Review for overflow, clipped or badly wrapped Greek, horizontal scrolling, tap
   targets under 44px and contrast; fix what turns up. Keep a committed smoke suite (key routes ×
   three viewports) that fails on horizontal overflow or console errors. **Screenshots stay out of
   the repo**; the final report names the folder.
8. **Before/after screenshots** in the final report: the top-left area where sidebar meets top
   bar, and the full shell, at all three sizes.

## Owner steps for production, in order

1. **Rotate the Resend API key** (still outstanding from 2026-09-26) and update `Email__ApiKey`
   on Render.
2. **Change `Email__From`** on Render from the bare address to the display-name form:
   `Move Your Ass <noreply@moveyourass.gr>`.
3. **Backblaze CORS** — already applied for `https://moveyourass.gr` and `http://localhost:4200`
   (docs/11 §3). Nothing to do unless the origins change. Remove the localhost origin when local
   testing is finished.
4. **Render environment variables** — add the seven `Video__*` values (docs/11 §4) and
   `AUTOMAPPER_LICENSE_KEY` (docs/10 §"AutoMapper licence"). **Do not add
   `ASPNETCORE_ENVIRONMENT`.**
5. **Apply the migrations to the Neon production branch**, using the **direct** string, *before*
   the new code is deployed. **Five** additive migrations are pending, in order:
   `VideoObjectStorage`, `PasswordCredentialPurpose`, `OutboxSubjectUser`,
   `RefreshTokenHashIndex`, `VideoCoverAndSiteContent`. The last one is new in this round: it
   adds `CoverObjectKey`, `CoverSizeBytes` and `ThumbnailSizeBytes` to `Video` and creates the
   `SiteContent` table. All five are additive - nothing is dropped or renamed.
   ```powershell
   $env:ConnectionStrings__Default = "<Neon production DIRECT string>"
   dotnet ef database update --project src/Mya.Infrastructure --startup-project src/Mya.Api
   Remove-Item Env:ConnectionStrings__Default
   ```
6. **GitHub secrets for backups** (docs/10 §"Backups"): `BACKUP_DATABASE_URL` (Neon production
   **direct**, `postgres://` URL form) and `BACKUP_PASSPHRASE`. Then run the workflow manually
   once and **do the restore test** — an untested backup is a guess.
7. **Merge and redeploy.** Render auto-deploys from `main`.
8. **Production upload and playback test**: docs/11 §6, on a real phone, over Wi-Fi and mobile
   data. Nothing has been uploaded in production yet.
9. **Email test on a real phone**: an invitation and a forgotten-password reset, end to end,
   checking spam placement.
10. **cron-job.org keep-alive**: `https://moveyourass.gr/health`, every 10 minutes, 06:00–23:00
    Europe/Athens, failure alerts on (docs/10 §7).
11. **Fill in "Ο γυμναστής σου"** once deployed: photo, name, tagline, biography and whichever
    contact methods should be public. Until it is filled in, clients see an empty page with a
    working contact form. Instructions are in docs/12.

### Email live verification - the half of item 5 that needs you

Run this **locally**, not in production, so nothing depends on a deploy.

1. Confirm the Resend API key in local user-secrets is current. If it was rotated (owner step 1),
   set the new one:
   ```powershell
   dotnet user-secrets set "Email:ApiKey" "<Resend API key>" --project src/Mya.Api
   dotnet user-secrets set "Email:Mode" "Resend" --project src/Mya.Api
   dotnet user-secrets set "Email:From" "Move Your Ass <noreply@moveyourass.gr>" --project src/Mya.Api
   ```
2. Start the API and the Angular app, sign in as the admin, and trigger one of each on the dev
   database, all addressed to **your own** inbox:
   - **Test email** - dashboard, "Αποστολή δοκιμαστικού email".
   - **Invitation** - create a client with your address, then delete it afterwards.
   - **Approval / decline** - register with your address, then approve it (and repeat, declining).
   - **Admin notification** - the same registration produces it.
   - **Forgotten password** - the login page's "Ξέχασες τον κωδικό;".
   - **Contact message** - sign in as the client and send one from "Ο γυμναστής σου".
3. Then check, and tell the next session: did each arrive; **inbox or spam**; does the header
   render with the logo, and does it still read as Move Your Ass with images blocked; are the
   Greek characters correct; does replying to the contact message go to the client's address.
4. Put `Email:Mode` back to `Console` locally afterwards, so development stops sending real mail.

### GitHub settings to switch on (repository -> Settings)

The workflows are in the repository; these are dashboard clicks only you can make.

| Where | Setting | Why |
|---|---|---|
| Settings -> Code security | **Secret scanning** | GitHub's own scan of the history |
| Settings -> Code security | **Push protection** | refuses a push containing a credential, before it is public |
| Settings -> Code security | **Dependabot alerts** and **security updates** | notice when a dependency has a known hole |
| Settings -> Code security | **Private vulnerability reporting** | this is what SECURITY.md tells people to use |
| Settings -> Actions -> General | **Workflow permissions: read repository contents** | defence in depth; the workflows already pin themselves to read |
| Settings -> Actions -> General | **Require approval for all outside collaborators** | a fork pull request cannot run a workflow unreviewed |
| Settings -> Rules -> Rulesets | **Protect `main`**: require a pull request, require the `api`, `web`, `container` and `security` checks, block force pushes, no deletions | `main` is what Render deploys |

### Delete the two local test accounts

`playwright-admin@localhost.test` and `playwright-client@localhost.test` exist on the **Neon dev
branch** only. Delete both when you are finished running the suite locally.

This is not only tidiness. `web/e2e/.auth/admin.json` and `client.json` were committed in
`cc9cabe` and untracked again in `6484c5b` - which means they are in the public history
permanently, and each holds a refresh-token cookie for one of these two accounts. gitleaks does
not flag them (they match no credential pattern), so a clean scan is **not** evidence that those
two sessions are safe. Deleting the accounts is what makes the tokens worthless. The directory is
now in `.gitignore`, so it cannot happen again.

### Local test accounts created during this work

Two accounts exist in the **Neon dev branch only**, created for the Playwright suite:
`playwright-admin@localhost.test` (Admin, seeded) and `playwright-client@localhost.test` (Client,
registered and approved through the app's own API). Their generated passwords are in the session
scratchpad, outside the repository, and appear nowhere in git or in these documents. Delete both
when the suite is no longer being run locally. **Nothing was created in production.**

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
| BACKUP_DATABASE_URL | **GitHub Actions secret** — Neon production direct, `postgres://` URL form |
| BACKUP_PASSPHRASE | **GitHub Actions secret** — lose it and every backup is unreadable |
| AUTOMAPPER_LICENSE_KEY | Render environment; free Community key, docs/10 |
| E2E_ADMIN_EMAIL/PASSWORD, E2E_CLIENT_EMAIL/PASSWORD | local only, for `npm run e2e`; never committed |
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
