# Backlog

Anything that is a good idea but not in the current phase scope. Adding to this file is the
correct response to "while we're here, could we also…".

## Next verification tasks — checkpoint 2026-09-24

- Production Angular build rechecked on 2026-09-25 in the actual repository: passed with an
  initial bundle of 554.96 kB, within the configured warning/error budgets. Previous working-copy
  cache/memory failures did not reproduce; no source change was required.
- Apply AccountInvitations migration to the intended local database; only the isolated verification
  database has been migrated so far.
- Configure real SMTP and verify delivery to an inbox; console delivery and invitation behavior passed.
- Finish the browser/viewport regression matrix described in docs/08-milestone-handover.md.
- Owner green light received on 2026-09-25; readiness work continues on `feature/prod-readiness`.

## Video (phases 5–6, see `docs/04-roadmap.md` and `docs/06-video-catalogue.md`)

- `Video`, `Tag`, `VideoTag` entities and migrations
- Bunny Stream adapter behind `IVideoStorage`, upload credentials endpoint, transcode webhook
- Filtered, paged client query; admin CRUD with confirmation modals
- Player component; publish/unpublish toggle for admin
- `Idempotency-Key` on video creation (the `IdempotencyRecord` table is kept for this)

## Production upgrades (ADR-016 triggers, no code changes)

- App Service B1: Always On, custom domain + managed certificate
- Authenticated sending domain in Brevo (SPF/DKIM/DMARC)
- Key Vault references for secrets once a second environment exists
- Bicep for the free-tier environment in docs/10

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
