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
- Dropping the now-unused `Video.ThumbnailUrl` column in a deliberate, owner-approved migration.
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

- Password reset by email (self-service; admin reset exists)
- Email 2FA (ADR-013 — `TwoFactorTicket` is in the schema, unused)
- Extend regression coverage as new behavior is introduced; initial video suites now exist
- Admin dashboard: recent uploads (pending registrations count is implemented)
- i18n extraction (currently hardcoded Greek)
- Branded, structured HTML email templates (owner request 2026-09-26): evaluate MJML or hand-authored email-safe HTML; use Move Your Ass logo/brand imagery, clear headings and CTA buttons, responsive layout, accessible alt text and a plain-text fallback. Verify Outlook/Hotmail and mobile rendering; keep setup credentials out of logs.

## Deferred / maybe never

- SMS notifications (no free tier; Telegram bot is the cheap alternative — see ADR-009)
- Instant JWT revocation via per-request security-stamp check (see `docs/03-auth-and-concurrency.md`)
- Multiple trainers
- Payments

## Out of scope

Session booking, calendars, slots and booking change requests are not part of this product.
