# 03 — Authentication, Authorization, Sessions, Concurrency

## 1. Why ASP.NET Core Identity + JWT (and not something else)

Considered and rejected:

| Option | Why not |
|---|---|
| **Microsoft Entra External ID** (ex-Azure AD B2C) | Your core requirement is *manual admin approval before access*. External ID pushes the user directory outside your database, which makes an approval queue, a suspend switch, and an admin user list all awkward custom-policy work. Also adds MAU billing. |
| **Auth0 / Clerk** | Same directory-ownership problem, plus a monthly bill and a hard dependency for a system with ~100 users. |
| **Cookie auth only** | Simplest, but couples the SPA and API to the same site and complicates a future mobile client. |
| **Identity + JWT + refresh cookie** ✅ | You own the user table, so approval/suspension/roles are plain SQL. Stateless request path suits a free-tier API that sleeps. |

**Decision: ASP.NET Core Identity for the user store, JWT for the request path, rotating refresh
token in an HttpOnly cookie.**

## 2. Token strategy

| Token | Lifetime | Storage | Contains |
|---|---|---|---|
| Access (JWT) | **15 min** | memory only (Angular signal) — never `localStorage` | `sub`, `email`, `role`, `sid`, `stamp`, `exp` |
| Refresh | **14 days**, rotating | `HttpOnly; Secure; SameSite=Strict; Path=/api/auth` cookie | opaque 256-bit random, stored **hashed** (SHA-256) in DB |

Signing: HS256 with a 256-bit key in Key Vault for phase 1. Move to RS256 only if a second
service ever needs to validate tokens independently.

Why access tokens live in memory and not `localStorage`: an XSS in the SPA can read
`localStorage` but cannot read the HttpOnly refresh cookie, and a 15-minute in-memory token
dies on tab close. This costs you one silent refresh call on page load. Worth it.

## 3. Roles and policies

Two roles only. Do not add more without a real reason.

```csharp
public static class Roles   { public const string Admin = "Admin"; public const string Client = "Client"; }
public static class Policies{ public const string AdminOnly = "AdminOnly";
                              public const string ActiveClient = "ActiveClient"; }

options.AddPolicy(Policies.AdminOnly,
    p => p.RequireRole(Roles.Admin));

options.AddPolicy(Policies.ActiveClient,
    p => p.RequireRole(Roles.Client).RequireClaim("status", nameof(UserStatus.Active)));
```

`UserStatus`: `PendingApproval → Active ⇄ Suspended`. A user is never deleted; they are
suspended. Deleting would orphan bookings and audit history.

The first Admin is created by the seeder from configuration, not by registration. There is no
public path to becoming an Admin.

## 4. Flows

### 4.1 Registration → approval

```
Client                     API                        DB                Email
  │                         │                          │                  │
  ├─ POST /auth/register ──▶│                          │                  │
  │                         ├─ create AppUser ────────▶│                  │
  │                         │  Status=PendingApproval  │                  │
  │                         │  EmailConfirmed=false    │                  │
  │                         ├─ outbox: ConfirmEmail ──▶│                  │
  │◀── 202 {status:         │                          │                  │
  │     "PendingApproval"}  │                          │  ──── dispatch ─▶│
  │                         │                          │                  │
  │  UI: "Αναμένεται επικοινωνία από τον διαχειριστή"  │                  │
```

```
Admin                      API                        DB                Email
  ├─ GET /admin/users ─────▶│  ?status=PendingApproval │                  │
  ├─ POST /admin/users      │                          │                  │
  │    /{id}/approve ──────▶│                          │                  │
  │                         ├─ Status=Active ─────────▶│                  │
  │                         ├─ AddToRole(Client) ─────▶│                  │
  │                         ├─ outbox: Approved ──────▶│  ──── dispatch ─▶│
  │◀── 204                  │                          │                  │
```

Registration returns **202, not 200**, and returns no token. There is nothing to log into yet.

### 4.2 Login with email 2FA

```
POST /api/auth/login          { email, password }
  ├─ invalid credentials        → 401 INVALID_CREDENTIALS   (same response for unknown email —
  │                                                          do not leak account existence)
  ├─ EmailConfirmed == false    → 403 EMAIL_NOT_CONFIRMED
  ├─ Status == PendingApproval  → 403 ACCOUNT_PENDING
  ├─ Status == Suspended        → 403 ACCOUNT_SUSPENDED
  └─ ok → generate 6-digit code, outbox email
         → 200 { requiresTwoFactor: true, ticket: "<opaque, 5 min>" }

POST /api/auth/2fa/verify     { ticket, code }
  ├─ wrong code  → 401 TWO_FACTOR_INVALID   (attempts++)
  ├─ 5 attempts  → ticket destroyed → 401 TWO_FACTOR_EXPIRED
  └─ ok → rotate ActiveSessionId
         → Set-Cookie: rt=<opaque>
         → 200 { accessToken, expiresIn: 900, user: { id, fullName, role } }
```

Use Identity's built-in `EmailTokenProvider` for the code. Store the ticket server-side
(`TwoFactorTicket` table or `IMemoryCache` — table is safer given F1 recycles).

Rate limits: 5 login attempts per email per 15 min, 10 per IP per 15 min, via
`AddRateLimiter` with a fixed window. Lock the account for 15 min after 5 failures
(`SignInManager` does this natively — turn it on).

### 4.3 Refresh

```
POST /api/auth/refresh        (cookie only, no body)
  ├─ token not found / hash mismatch     → 401
  ├─ token already used  → REUSE DETECTED → revoke entire token family, 401
  ├─ user.Status != Active               → 401 ACCOUNT_SUSPENDED
  ├─ user.SecurityStamp != token.Stamp   → 401 SESSION_SUPERSEDED
  ├─ token.SessionId != user.ActiveSessionId → 401 SESSION_SUPERSEDED
  └─ ok → issue new access token + NEW refresh token, revoke old
```

Rotation with reuse detection is the part people skip. If a refresh token is presented twice,
one of the two presenters is an attacker — kill the whole family.

### 4.4 Admin cutting access

```
POST /api/admin/users/{id}/suspend
  → Status = Suspended
  → SecurityStamp regenerated
  → all RefreshTokens for user revoked
  → ActiveSessionId = null
```

Effect: the user's current access token still works for **up to 15 minutes**, then their next
refresh fails and they are logged out. If you need instant revocation, see §6.

## 5. "Prevent the double browser" — four separate problems

You raised this as one question. It is actually four, with four different fixes. Only the
first one is load-bearing for correctness.

### 5.1 Two different users book the same slot → atomic UPDATE

The database decides. One statement, no `SELECT` first:

```csharp
var rows = await db.Database.ExecuteSqlInterpolatedAsync($"""
    UPDATE Slot
       SET BookedByUserId = {userId}, BookedAtUtc = SYSUTCDATETIME()
     WHERE Id = {slotId}
       AND BookedByUserId IS NULL
       AND IsPublished = 1
       AND StartsAtUtc > SYSUTCDATETIME()
    """, ct);

if (rows == 0) return Result.Conflict(ErrorCodes.SlotTaken);
```

No lock, no race, no retry loop. The loser gets 409 and the calendar refreshes. **Nothing in the
UI, the session model, or the token design changes this guarantee, and nothing should be
allowed to weaken it.**

### 5.2 One user double-submits → Idempotency-Key

This is the "unique key" idea you were reaching for, and it is the right pattern. The client
generates a UUID **once per user intent** (when the confirm dialog opens, not when the button is
clicked) and sends it as a header:

```
POST /api/bookings
Idempotency-Key: 9c1f0e2a-...
{ "slotId": "..." }
```

Server side, an action filter:

```csharp
public sealed class IdempotencyFilter(IAppDbContext db) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
    {
        if (!ctx.HttpContext.Request.Headers.TryGetValue("Idempotency-Key", out var key))
        { await next(); return; }

        var userId = ctx.HttpContext.User.GetUserId();

        var existing = await db.IdempotencyRecords
            .FirstOrDefaultAsync(r => r.UserId == userId && r.Key == key.ToString());

        if (existing is not null)
        {
            // replay the original outcome — do NOT execute again
            ctx.Result = new ContentResult {
                StatusCode  = existing.StatusCode,
                Content     = existing.ResponseJson,
                ContentType = "application/json"
            };
            return;
        }

        var executed = await next();
        // persist (userId, key, statusCode, responseJson, expiresAt = +24h)
    }
}
```

`IdempotencyRecord` has a **unique index on `(UserId, Key)`**. A background job prunes rows
older than 24 hours. Now a double-click, a flaky-network retry, or two tabs submitting the same
intent all produce exactly one booking and two identical responses.

Apply to: `POST /bookings`, `POST /change-requests`, `POST /auth/register`.

### 5.3 Two tabs showing stale state → BroadcastChannel + polling

Pure UX, zero backend involvement:

```ts
// core/sync/tab-sync.service.ts
const channel = new BroadcastChannel('pt-sync');

// after any successful booking mutation
channel.postMessage({ type: 'CALENDAR_INVALIDATED' });

// in CalendarComponent
channel.onmessage = e => { if (e.data.type === 'CALENDAR_INVALIDATED') this.reload(); };
```

Combine with a 20-second poll of `GET /api/availability` while the calendar is visible (pause
on `document.visibilitychange` to save CPU quota). Slots taken by someone else render blurred
and disabled with "Κλεισμένο από άλλον χρήστη".

Also broadcast `LOGGED_OUT` so logging out in one tab logs out all of them.

### 5.4 One account used on two devices → single active session

If you want to stop account sharing — one person paying, three people watching — enforce one
session per user.

```
AspNetUsers
  + ActiveSessionId  uniqueidentifier NULL
  + ActiveSessionStartedAtUtc datetime2 NULL
  + ActiveSessionUserAgent nvarchar(256) NULL
```

On successful 2FA verify:

```csharp
user.ActiveSessionId = Guid.NewGuid();          // invalidates any previous session
await db.RefreshTokens
    .Where(t => t.UserId == user.Id && t.RevokedAtUtc == null)
    .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, clock.UtcNow), ct);
```

The new access token carries `sid = ActiveSessionId`. The refresh endpoint compares them (§4.3).

Two enforcement modes — pick one and write it down:

| Mode | Check | Cost | Kick-out delay |
|---|---|---|---|
| **Lazy** (recommended) | `sid` validated only on refresh | zero per-request | ≤ 15 min |
| **Strict** | `sid` validated on every request against `IMemoryCache` (5 min TTL, invalidated on login/suspend) | one cache hit | immediate |

Start lazy. It is one line in the refresh handler and costs nothing on a free-tier API. Move to
strict only if account sharing turns out to be a real problem — and note that strict mode makes
your stateless JWT partly stateful, which is a real tradeoff, not a free upgrade.

On login, if `ActiveSessionId` was already set, return it in the response so the SPA can show
"Έγινε αποσύνδεση από την άλλη συσκευή." Silent kick-outs generate support tickets.

## 6. Optional: instant revocation

If "admin cuts access and it must take effect *now*" is a hard requirement rather than a
nice-to-have, add a `SecurityStampValidator` on `JwtBearerEvents.OnTokenValidated` that compares
the token's `stamp` claim against a cached user stamp. That is one `IMemoryCache` lookup per
request and immediate revocation on suspend.

**Do not build this in phase 1.** A 15-minute window is acceptable for a training platform, and
the cache invalidation across App Service instances is a real complication you do not need yet.

## 7. Security checklist for phase 1

- [ ] HTTPS enforced, HSTS on, HTTP redirected
- [ ] CORS: exact SPA origin, `AllowCredentials`, no wildcard
- [ ] Refresh cookie: `HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/api/auth`
- [ ] Access token never written to `localStorage` or a log
- [ ] Rate limiting on `/auth/*` (login, register, 2fa/verify, refresh)
- [ ] Account lockout: 5 failures → 15 min
- [ ] Password: min 10 chars, upper + lower + digit, checked against a breach list if cheap
- [ ] Identical response for unknown email and wrong password
- [ ] Email confirmation link: single-use, 24h expiry
- [ ] 2FA code: 6 digits, 5 min, 5 attempts, single-use
- [ ] JWT signing key in Key Vault, not `appsettings.json`
- [ ] `ProblemDetails` in production carries no stack trace
- [ ] Security headers: `X-Content-Type-Options`, `Referrer-Policy`, CSP on the SWA
- [ ] Serilog redacts `password`, `code`, `token`, `Authorization`
