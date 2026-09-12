# Backlog

Anything that is a good idea but not in the current phase scope. Adding to this file is the
correct response to "while we're here, could we also…".

## Phase 2 — booking

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

## Phase 3 — video

- `Video` entity, admin upload flow via write SAS (browser → Blob direct)
- ffmpeg HLS ladder encode step (1080p / 720p / 480p)
- Directory-scoped read SAS, 4h expiry
- hls.js player component with `xhrSetup` token injection
- Client library list, publish/unpublish toggle for admin

## Phase 4 — polish

- Password reset flow
- Client profile editing
- Admin dashboard: upcoming sessions, pending requests count
- i18n extraction (currently hardcoded Greek)
- Email templates with real branding
- Custom domain + managed certificate

## Deferred / maybe never

- SMS notifications (no free tier; Telegram bot is the cheap alternative — see ADR-009)
- SignalR live slot updates (polling is sufficient at this scale)
- Instant JWT revocation via per-request security-stamp check (see `docs/03` §6)
- Multiple trainers (would add a `TrainerId` FK to `Slot` — migration, not a rewrite)
- Payments
