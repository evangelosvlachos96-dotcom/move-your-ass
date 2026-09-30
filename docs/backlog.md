# Backlog

Anything that is a good idea but not in the current phase scope. Adding to this file is the
correct response to "while we're here, could we also…".

## Next verification tasks — phase 4

Current position is `docs/08-milestone-handover.md` §"Current state"; this list is only the tail
of work that outlives any one checkpoint.

- Verify a real client invitation inbox/browser flow and spam placement. Admin notification receipt is confirmed; console invitation acceptance passes.
- Finish the browser and viewport regression matrix in `docs/07-manual-test-checklist.md`,
  especially password setup in the browser and the full mobile/desktop matrix.
- Re-run the whole checklist against the deployed URL once the environment exists.
- Measure the first request after an idle period on the real environment, and record it against
  the ADR-017 upgrade trigger.

## Video live acceptance

Backend and UI are implemented on Backblaze B2 (ADR-019). Create the bucket and key, apply the
CORS rule, and run the docs/11 §6 acceptance list; then load trainer content and complete the
docs/12 handover. No Backblaze resource has been created by an agent.

Future improvements, none of them blocking:

- A lifecycle rule or sweep for unfinished large files left by a crash mid-upload.
- Dropping the now-unused `Video.ThumbnailUrl` column, and the entirely unused
  **`IdempotencyRecord` table** (superseded by the `(CreatedByUserId, CreationKey)` reservation
  on `Video`; no handler touches it). **Deliberately deferred by the owner on 2026-09-27, not
  forgotten.** Dropping a column or table inverts the usual deploy order — the new code has to be
  live *before* the migration runs, or the running build selects something that no longer exists
  — and both are harmless where they are. Do this only as its own change, with that ordering
  stated in the plan.
- Server-side global drag ordering across pages, rather than adjacent moves on the visible page.
- Transcoding, if 4K or HEVC uploads become a real problem rather than a documented instruction.
- Cloudflare R2 instead of B2 if egress rather than storage becomes the binding limit; it is a
  configuration change, not a code change.

## Production upgrades (ADR-017 triggers, no code changes)

- Render Starter: always on, which removes the spin-down, the cron-job.org keep-alive and the
  slow first request each morning
- Paid Neon plan if compute hours approach the free allowance, which stops hard rather than throttling
- Paid Resend plan beyond 100 emails/day
- A staging environment, which needs paid Render: the free instance-hour budget covers one service
- Automated usage alerting against Neon's consumption API, replacing the weekly manual check
- Infrastructure-as-code for the environment, if the provider set ever grows past four dashboards

## Polish

- Email 2FA (ADR-013 — `TwoFactorTicket` is in the schema, unused)
- Extend regression coverage as new behavior is introduced; initial video suites now exist
- Admin dashboard: recent uploads (pending registrations count is implemented)
- i18n extraction (currently hardcoded Greek)

## Carried out of the final round (2026-09-28)

Operational, not code. Each one is a thing the owner does in somebody's dashboard.

- **Rotate the Resend API key.** Outstanding since 2026-09-26. Update `Email__ApiKey` on Render
  afterwards.
- **Remove `http://localhost:4200` from the Backblaze B2 CORS rule** once local video testing is
  finished, leaving only `https://moveyourass.gr`. The procedure is docs/11 section 3; the web
  console cannot do it, so it is the `aws s3api put-bucket-cors` route with a rule file.
- **`AUTOMAPPER_LICENSE_KEY` as a Render environment variable**, or every deploy's startup logs
  fill with licence warnings. docs/10 section "AutoMapper licence".
- **`BACKUP_DATABASE_URL` and `BACKUP_PASSPHRASE` as GitHub secrets, then a real restore test.**
  The workflow is written, pinned and encrypts before anything is written to disk, but it has
  never run. An untested backup is a guess.
- **Delete `playwright-admin@localhost.test` and `playwright-client@localhost.test`** from the
  Neon dev branch. Their refresh-token cookies are in the public git history (`cc9cabe`);
  deleting the accounts is what makes those tokens worthless. See docs/08.

## Known, accepted, and worth revisiting

- **Presigned playback URLs work until they expire** (up to two hours), whether or not the holder
  is still signed in. Accepted in ADR-019 - the alternative is proxying every byte through the
  single free Render instance. Revisit if the library is ever shared beyond the trainer's own
  clients.
- **Development rate limits are raised in `appsettings.Development.json`** (60 sign-ins, 40
  per-user writes) because five browser projects times two roles cannot sign in inside a limit of
  five. Production keeps the defaults of five and five, and the limiter itself is covered by
  backend tests. Worth re-checking if the suite ever runs against a deployed instance.
- **Development rate limits are far above production's** (`appsettings.Development.json`: 300
  sign-ins per (email, IP), 500 per email per hour, 100 per-user writes). Production keeps 5, 50
  and 5. The suite signs in roughly forty times per run across five browser projects, and the
  limits exist to stop guessing, not to stop a test — the limiter itself is covered by backend
  tests. Worth re-checking if the suite is ever pointed at a deployed instance.
- **`ngx-image-cropper` is a new runtime dependency** (MIT, 9.x). It is the only third-party UI
  library in the app besides Angular Material. If it ever goes unmaintained, the crop dialog is
  one component and the shapes it exports are two constants.
- **No LICENSE file, deliberately** - all rights reserved. If the repository is ever meant to be
  reusable, that is the decision to revisit first.

## Deferred / maybe never

- SMS notifications (no free tier; Telegram bot is the cheap alternative — see ADR-009)
- Instant JWT revocation via per-request security-stamp check (see `docs/03-auth-and-concurrency.md`)
- Multiple trainers
- Payments

## Out of scope

Session booking, calendars, slots and booking change requests are not part of this product.
