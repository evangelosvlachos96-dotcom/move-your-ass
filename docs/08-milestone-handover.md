# 08 — Milestone handover

**This is the resume-here document.** If you are an agent or a person picking this work up, read
the "Current state" section below before anything else. It is kept accurate at the end of every
checkpoint and after every meaningful step inside one. History follows it, oldest concerns last.

---

# Current state

**Updated:** 2026-09-25, end of checkpoint 1.

| | |
|---|---|
| Branch | `feature/prod-readiness` |
| Last commit | `6f7a37d` — *infra: free tier production groundwork (ADR-016)* |
| Working tree | **dirty** — checkpoint 1 is complete and awaiting the owner's review and commit |
| Phase | 4, production readiness (`docs/04-roadmap.md`) |

## Checkpoint status

| # | Checkpoint | State |
|---|---|---|
| 1 | Documentation and decision (ADR-017, merged runbook, doc sweep) | **done, uncommitted** |
| 2 | SQL Server to PostgreSQL | not started |
| 3 | Resend API email sender | not started |
| 4 | Container for Render | not started |
| — | Handover section listing everything the owner must enter | not started (after checkpoint 4) |

The owner reviews and commits after each checkpoint. Do not start the next one without an
explicit green light.

## What is verified, and how

Measured in this repository on 2026-09-25, before any checkpoint-1 edits:

- `dotnet build -c Release` — succeeded, **0 warnings, 0 errors** (warnings are errors here).
- `dotnet test -c Release` — **15 passed**: 4 settings, 8 persistence and UTC converter, 3
  architecture.
- `npm run build -- --configuration production` in `web/` — succeeded in about 24 seconds. Output
  lands at `web/dist/mya-web/browser`, which is the path both `deploy.yml` and the future
  Dockerfile expect.
- Tooling present: .NET SDK 10.0.300, `dotnet-ef` 10.0.12, Node 24.15.0, Docker CLI 29.4.3.

Provider facts below were checked against current vendor documentation on 2026-09-25, not
recalled. Package versions were resolved from nuget.org directly.

## What is written but NOT verified

Checkpoint 1 produced documentation only. **No code changed, nothing was deployed, and no
provider account was touched.**

- `docs/10-production.md` describes an environment that **does not exist yet**. No Render service,
  no Neon project, no Resend domain, no Cloudflare records, no UptimeRobot monitor.
- Sections 4 to 6 and 8 of that runbook depend on code from checkpoints 2 to 4 and cannot be run
  today.
- **Docker Desktop is installed but its daemon would not start** (`docker info` returns "Docker
  Desktop is unable to start"). This blocks checkpoint 4 and the owner will need to fix it.
- The claim that Render forwards `X-Forwarded-Proto` is **not** in Render's documentation. Render
  documents only that its load balancer terminates TLS and forwards over HTTP. Confirm the actual
  headers empirically in checkpoint 4 before relying on them.
- Neon's five-minute suspend behaviour with a live Npgsql pool is reasoned from documentation, not
  observed. Checkpoint 2 must measure it.

## The exact next step

**Wait for the owner's review and commit of checkpoint 1.** Then, on their green light, start
checkpoint 2, in this order:

1. In `src/Mya.Infrastructure/Mya.Infrastructure.csproj`, replace
   `Microsoft.EntityFrameworkCore.SqlServer` with `Npgsql.EntityFrameworkCore.PostgreSQL`
   **10.0.3** (verified on nuget.org; it targets EF Core 10 and pulls `Npgsql` 10.0.3).
2. Swap `options.UseSqlServer(...)` for `UseNpgsql(...)` in
   `src/Mya.Infrastructure/DependencyInjection.cs` and delete the registration and the file for
   `SqlConnectionRetryInterceptor`. Keep both `OutboxSignal` interceptors.
3. Work through the provider-specific list in the "Gotchas" section below. It is complete; it was
   produced by grepping for every provider idiom in the solution.
4. Delete the three migrations under `src/Mya.Infrastructure/Persistence/Migrations` plus
   `AppDbContextModelSnapshot.cs`, then `dotnet ef migrations add InitialCreate`. **The owner has
   already approved this deletion** (no production data exists).
5. Update `SqlServerProviderPrefixes` in `tests/Mya.ArchitectureTests/LayeringTests.cs` to the
   Npgsql prefixes, and drop `Testcontainers.MsSql` from the integration test project.
6. Verify: build with zero warnings, `dotnet test`, `dotnet ef migrations add`, then run the app
   against the Neon `dev` branch and exercise register, approve, login and one invitation in
   Console email mode.
7. Measure Neon's compute behaviour while the app idles, and record the result here.

## Open decisions and questions waiting on the owner

- **Checkpoint 1 review and commit.** Nothing else is blocked on anything else right now.
- Later, during checkpoint 3: the owner sets `Email:ApiKey` in user-secrets when asked, so one
  real invitation can be sent to their own address. Never ask for the value in conversation.
- Later, during checkpoint 4: Docker Desktop must be running.

## Deviations from the original plan, and why

- **No Npgsql pool tuning for the reason first proposed.** The first analysis assumed an open idle
  connection would hold Neon's compute awake, which would have made the free tier unviable. Neon's
  documentation says the scale-to-zero timer is driven by *active queries*, and that only truly
  inactive connections are closed. An idle pooled socket does not keep the compute awake.
  `Connection Idle Lifetime` is still set, but for a different and narrower reason: Npgsql's
  default is 300 seconds and Neon's suspend delay is also 300 seconds, so the two timers race and
  a request can be handed a socket Neon has just severed. Setting it below five minutes makes
  Npgsql always close first.
- **`Cors:AllowedOrigin` becomes `App:PublicOrigin`.** Production has no CORS at all, so naming
  the setting after CORS misdescribes its only real job, which is building email links. Approved
  by the owner. The rename lands in checkpoint 4, not checkpoint 1, so the key is still
  `Cors:AllowedOrigin` in code today.
- **ADR-016's "no pingers" rule is narrowed rather than deleted.** It was correct for Azure, where
  any request woke the database too. On this stack the rule becomes "pingers may only call
  `/health`, which must never touch the database". See ADR-017.

## Gotchas discovered

**Provider behaviour, all checked against current vendor docs on 2026-09-25:**

- **Render free blocks outbound SMTP** on ports 25, 465 and 587. Port 25 stays blocked even on
  paid plans. This is why MailKit and the SMTP sender are being removed rather than kept as an
  option.
- **Render terminates TLS at its load balancer** and forwards to the container over plain HTTP,
  redirecting HTTP to HTTPS at the edge first. Consequence for this app: the rate limiter falls
  back to `context.Connection.RemoteIpAddress`, which becomes Render's own address for every
  caller and collapses all unauthenticated requests into a single bucket. Forwarded headers are
  required, not optional.
- **Render sets `PORT`** (default 10000) and requires binding `0.0.0.0`. Do not hardcode a port.
- **Render free cannot attach a persistent disk**, and 750 instance-hours per month is a
  workspace-wide budget. One always-on service is about 730 of it, so there is no room for a
  second free service.
- **Cloudflare and Render disagree about proxying.** Render requires a CNAME at the apex for
  Cloudflare users, and wants it grey-clouded while it validates over HTTP. Cloudflare redirect
  rules only run on proxied records. Resolution: apex is a DNS-only CNAME to Render, `www` is a
  proxied CNAME carrying the redirect rule, so `www` never reaches Render. Also delete any `AAAA`
  records; Render is IPv4 only.
- **Resend requires its own DNS records to stay unproxied.** Its docs say a proxied CNAME prevents
  domain verification from completing.
- **Resend supports `Idempotency-Key`**, up to 256 characters, retained 24 hours. A repeat with
  the same key and the same payload replays the original response without sending again; a repeat
  with a *different* payload returns 409. The outbox's longest retry span is about two and a half
  hours, comfortably inside the 24-hour window, so the outbox message Id is a safe key.
- **Resend free** allows 100 emails/day and 3,000/month, and rate-limits at 10 requests/second.
- **Neon's suspend timer counts active queries, not connections.** An idle pooled connection does
  not hold the compute awake. Npgsql's `Keepalive` defaults to disabled, so the pool sends no
  queries of its own.
- **Neon has two connection strings per branch.** Pooled (`-pooler`, PgBouncer transaction mode)
  for the app; **direct** for migrations, `dotnet ef` and `--seed-admin`. Using the pooled string
  for DDL is the classic mistake.
- **Exceeding Neon's compute allowance is a hard stop**, not throttling: compute is suspended
  until the next billing period, existing connections drop, new ones fail. Neon documents no
  threshold alerting, hence the weekly manual check in the runbook.
- **Npgsql `Max Auto Prepare` defaults to 0**, so PgBouncer transaction pooling needs no extra
  connection-string option.

**Provider-specific code that checkpoint 2 must change.** This list is complete as of today:

| Where | What | Becomes |
|---|---|---|
| `Identity/UserService.cs` | `SqlException` numbers 2601 / 2627 | `Npgsql.PostgresException` with `SqlState == "23505"` |
| `Identity/UserService.cs` `ListAsync` | `.Contains(term)` on email and names | `EF.Functions.ILike` with `%` and `_` escaped — Postgres `LIKE` is case-sensitive, SQL Server's collation was not |
| `Persistence/AppDbContext.cs` | `HaveColumnType("datetime2(3)")` | `timestamp with time zone`; keep `UtcDateTimeConverter`, it is now load-bearing |
| `Notifications/OutboxDispatcher.cs` `ClaimAsync` | `UPDATE TOP (n) ... WITH (ROWLOCK, READPAST, UPDLOCK) ... OUTPUT inserted.*` | CTE with `FOR UPDATE SKIP LOCKED` plus `RETURNING`, same lease, attempt and dead-letter semantics |
| `Configurations/OutboxMessageConfiguration.cs`, `RefreshTokenConfiguration.cs` | `HasFilter("[Col] IS NULL")` | double-quoted identifiers |
| `Configurations/AppUserConfiguration.cs` | `HasCheckConstraint(..., "[Status] IN (...)")` | double-quoted identifier |
| `PasswordInvitation`, `RefreshToken`, `TwoFactorTicket` configs | `HasColumnType("binary(32)")` | `bytea` — Postgres has no fixed-length binary type |
| `OutboxMessage`, `IdempotencyRecord` configs | `HasMaxLength(-1)` (the `nvarchar(max)` idiom) | `text` |
| `tests/Mya.ArchitectureTests/LayeringTests.cs` | `SqlServerProviderPrefixes` | Npgsql prefixes |
| `tests/Mya.Api.IntegrationTests/*.csproj` | `Testcontainers.MsSql` | remove |

`AppDbContext.RestoreStringKeyLengths` stays as it is; it is about Identity's key lengths, not
about the provider. `AcceptInvitationHandler`'s conditional `ExecuteUpdateAsync` is already
provider-neutral.

**Layering note:** `EF.Functions.ILike` lives in the Npgsql provider assembly, which
`Mya.Application` may not reference. The admin search is in `Mya.Infrastructure`, so this is fine
as it stands — but do not move that query into a handler.

## Settings and secrets: names and where they live

Values never appear in this repo, in documentation, or in conversation. This table is names only.

| Name | Where it is set | Secret |
|---|---|---|
| `ConnectionStrings:Default` | local user-secrets for `src/Mya.Api` (Neon `dev`, pooled) | **yes** |
| `Jwt:SigningKey` | local user-secrets | **yes** |
| `Seed:AdminEmail`, `Seed:AdminPassword`, `Seed:AdminFirstName`, `Seed:AdminLastName` | local user-secrets; on the command line for the one-shot production seed | **yes** (password) |
| `Email:ApiKey` | local user-secrets when testing real delivery | **yes** |
| `ConnectionStrings__Default` | Render dashboard (Neon `production`, pooled) | **yes** |
| `Jwt__SigningKey` | Render dashboard | **yes** |
| `Email__ApiKey` | Render dashboard | **yes** |
| `AUTOMAPPER_LICENSE_KEY` | Render dashboard | **yes** |
| `ASPNETCORE_ENVIRONMENT`, `App__PublicOrigin`, `Email__Mode`, `Email__From` | Render dashboard | no |
| Neon direct connection string | used ad hoc for migrations and seeding, not stored | **yes** |
| Resend API key | created in Resend, copied to Render and user-secrets | **yes** |
| DNS records | Cloudflare dashboard | no |

`Seed:*` is deliberately never set on Render. The first Admin is created by a one-shot local
command so that password never lives in a dashboard.

---

# How to resume

**Read in this order:**

1. This section and "Current state" above.
2. `CLAUDE.md` — the non-negotiable rules, the layout, and the commands.
3. `docs/04-roadmap.md` Phase 4 — the four checkpoints and what "done" means.
4. ADR-017 in `docs/05-decisions.md` — why the stack is Render, Neon, Resend and Cloudflare, and
   the four code properties the free tier depends on.
5. `docs/10-production.md` — the runbook for the environment.
6. Only then the code.

**Verify where things actually stand** before trusting anything above:

```powershell
git log --oneline -1
git status --short
dotnet build -c Release          # must be 0 warnings, 0 errors
dotnet test -c Release --no-build
cd web ; npm run build -- --configuration production
```

**Do not redo these:**

- Do not re-research the provider limits, ports, headers and idempotency semantics. They are
  recorded in "Gotchas" above with the date they were checked. Re-check only if a provider is
  behaving differently from what is written there.
- Do not add unit or integration tests. ADR-014 defers them; `Mya.ArchitectureTests` must stay
  green.
- Do not re-verify the Angular production build unless `web/` changed.
- Do not run `git add`, `git commit` or `git push`. The owner does all git operations, and
  branches are never deleted after merging.
- Do not write a secret value anywhere, and never ask the owner to paste one into a conversation.

**If your context is running low,** stop the current work and update "Current state" first. An
accurate handover is worth more than one more half-finished step.

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
