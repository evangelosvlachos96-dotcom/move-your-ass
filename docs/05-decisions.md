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
