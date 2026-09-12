# Backlog

Anything that is a good idea but not in the current phase scope. Adding to this file is the
correct response to "while we're here, could we also…".

## Video (phases 5–6, see `docs/04-roadmap.md` and `docs/06-video-catalogue.md`)

- `Video`, `Tag`, `VideoTag` entities and migrations
- Bunny Stream adapter behind `IVideoStorage`, upload credentials endpoint, transcode webhook
- Filtered, paged client query; admin CRUD with confirmation modals
- Player component; publish/unpublish toggle for admin
- `Idempotency-Key` on video creation (the `IdempotencyRecord` table is kept for this)

## Polish

- Password reset by email (self-service; admin reset exists)
- Email 2FA (ADR-013 — `TwoFactorTicket` is in the schema, unused)
- Automated tests beyond the architecture tests (ADR-014)
- Admin dashboard: pending registrations count, recent uploads
- i18n extraction (currently hardcoded Greek)
- Email templates with real branding
- Custom domain + managed certificate

## Deferred / maybe never

- SMS notifications (no free tier; Telegram bot is the cheap alternative — see ADR-009)
- Instant JWT revocation via per-request security-stamp check (see `docs/03` §6)
- Multiple trainers
- Payments

## Dropped — may return

Session booking was removed from the product in September 2026 (`docs/04-roadmap.md`). ADR-006
and ADR-007 are withdrawn. Kept here in case it returns; nothing below is scheduled.

- `Slot`, `Booking`, `ChangeRequest` entities and migrations
- Admin weekly availability template + materialiser job (8 weeks ahead)
- `GET /api/availability` with free/taken state
- `POST /api/bookings` — atomic claim, 409 on conflict, `Idempotency-Key`
- 5-active-bookings cap (serializable transaction)
- Client calendar: CSS-grid week view, taken slots blurred and disabled
- `BroadcastChannel` tab sync + 20s polling while calendar is visible
- Change requests: create, cancel while pending, admin approve/reject
- Admin direct move/cancel of a booked session
- Integration test: two parallel bookings → one 201, one 409
- SignalR live slot updates
- `PlatformSettings` booking values (`MaxActiveBookings`, `SessionDurationMinutes`,
  `MinBookingNoticeHours`, `CancellationWindowHours`, `SlotHorizonWeeks`) — still bound and
  validated, unused until booking returns
