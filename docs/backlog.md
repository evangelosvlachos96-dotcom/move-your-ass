# Backlog

Anything that is a good idea but not in the current phase scope. Adding to this file is the
correct response to "while we're here, could we also…".

## Next verification tasks — phase 4

Current position is `docs/08-milestone-handover.md` §"Current state"; this list is only the tail
of work that outlives any one checkpoint.

- Configure Resend and verify delivery to a real inbox, including spam placement. Console delivery
  and the invitation flow already pass locally.
- Finish the browser and viewport regression matrix in `docs/07-manual-test-checklist.md`,
  especially password setup in the browser and the full mobile/desktop matrix.
- Re-run the whole checklist against the deployed URL once the environment exists.
- Measure the first request after an idle period on the real environment, and record it against
  the ADR-017 upgrade trigger.

## Video (phases 5–6, see `docs/04-roadmap.md` and `docs/06-video-catalogue.md`)

- `Video`, `Tag`, `VideoTag` entities and migrations
- Bunny Stream adapter behind `IVideoStorage`, upload credentials endpoint, transcode webhook
- Compare Cloudflare Stream against Bunny Stream on price and signed playback before building the
  adapter. Not R2: R2 is plain storage with no transcoding, see `docs/06` §4
- Filtered, paged client query; admin CRUD with confirmation modals
- Player component; publish/unpublish toggle for admin
- `Idempotency-Key` on video creation (the `IdempotencyRecord` table is kept for this)

## Production upgrades (ADR-017 triggers, no code changes)

- Render Starter: always on, which removes the spin-down and the UptimeRobot keep-alive
- Paid Neon plan if compute hours approach the free allowance, which stops hard rather than throttling
- Paid Resend plan beyond 100 emails/day
- A staging environment, which needs paid Render: the free instance-hour budget covers one service
- Automated usage alerting against Neon's consumption API, replacing the weekly manual check
- Infrastructure-as-code for the environment, if the provider set ever grows past four dashboards

## Polish

- Password reset by email (self-service; admin reset exists)
- Email 2FA (ADR-013 — `TwoFactorTicket` is in the schema, unused)
- Automated tests beyond the architecture tests (ADR-014)
- Admin dashboard: recent uploads (pending registrations count is implemented)
- i18n extraction (currently hardcoded Greek)
- Email templates with real branding

## Deferred / maybe never

- SMS notifications (no free tier; Telegram bot is the cheap alternative — see ADR-009)
- Instant JWT revocation via per-request security-stamp check (see `docs/03-auth-and-concurrency.md`)
- Multiple trainers
- Payments

## Out of scope

Session booking, calendars, slots and booking change requests are not part of this product.
