# CLAUDE.md

Context for Claude Code sessions in this repository. Read `docs/` before making architectural
changes. This is a **mono-repo**: backend, frontend, infra, and docs live together.

## What this is

A private video library for one trainer (Admin). Clients register, get manually approved by the
Admin, then log in to browse and watch categorised workout videos. Session booking was part of the
original design and was **dropped in September 2026** — see `docs/04-roadmap.md` and
`docs/06-video-catalogue.md`.

Not a marketplace. Not multi-tenant. One admin, tens-to-low-hundreds of clients. Headed for
production, not a demo.

## Stack

| Layer | Choice | Version |
|---|---|---|
| Backend | ASP.NET Core Web API | .NET 10 (LTS) |
| ORM | EF Core | 10.x |
| Validation | FluentValidation | latest |
| Mapping | AutoMapper | latest (Community licence) |
| DB | PostgreSQL via Npgsql | Neon, EF Core 10 provider |
| Frontend | Angular, standalone + signals | latest (`ng new` default) |
| UI kit | Angular Material | matching Angular major |
| Auth | ASP.NET Core Identity + JWT | — |
| Email | Resend HTTPS API behind `IEmailSender` | — |
| Video | Backblaze B2 (S3-compatible) behind `IVideoStorage` | AWSSDK.S3 4.x |
| Hosting | One Render web service (Docker) serving API + SPA | Frankfurt, free tier |
| DNS | Cloudflare, `moveyourass.gr` | — |
| Tests | xUnit / NSubstitute / Shouldly, Vitest / Testing Library | account, video, provider and architecture coverage |

## Licensing notes

- **AutoMapper and MediatR are fine to use.** Both are free under the Community licence for
  companies and individuals under **$5,000,000 gross annual revenue**. A licence key is required
  for auditing — set it via `AUTOMAPPER_LICENSE_KEY` / `MEDIATR_LICENSE_KEY` environment
  variables, never in `appsettings.json`.
- **FluentAssertions v8+ is not free for commercial use.** Pin v7 (Apache 2.0) or use Shouldly.
  This repo uses Shouldly.
- Register both keys as environment variables in the Render dashboard before the first production
  deploy, or startup logs will fill with licence warnings.

## Non-negotiable rules

1. **Controllers are thin.** Bind → call one handler/service → map the result. No `if`, no EF,
   no business rules in a controller.
2. **Angular features never import `HttpClient`.** Everything goes through `core/http/ApiClient`.
   There is an ESLint rule enforcing this — do not disable it.
3. **All times are UTC** in the database and on the wire. Convert to `Europe/Athens` in the UI only.
4. **`MustChangePassword` is enforced server-side.** While it is set, every authenticated endpoint
   except `/auth/me`, `/auth/change-password` and `/auth/logout` returns 403 `MUST_CHANGE_PASSWORD`
   (`MustChangePasswordMiddleware`). A guard in Angular is a convenience, not the control.
5. **Every mutating endpoint a user can double-submit takes an `Idempotency-Key` header.**
   Video creation reserves a unique creator/key on the Video row. The key is generated when
   the dialog opens, not when the button is clicked.
6. **`Mya.Application` may reference `Microsoft.EntityFrameworkCore` (the abstractions: `DbSet<T>`,
   async LINQ) but must not reference the database provider (`Npgsql.*`) or
   `Microsoft.AspNetCore.*`. `Mya.Domain` references nothing.** Enforced by `Mya.ArchitectureTests`;
   see `docs/02` §1 for the exact statement. Provider-specific query operators such as
   `EF.Functions.ILike` therefore belong in `Mya.Infrastructure`, never in a handler.
7. **No secrets in the repo, in docs, or in chat.** Local: user-secrets for `src/Mya.Api`.
   Production: environment variables in the Render dashboard. Docs and handover notes name a
   setting and say where it lives; they never carry a value. Never ask the owner to paste a
   credential into a conversation.
8. **Admin rules live in handlers, not the UI.** Cannot delete or suspend self, cannot delete or
   demote the last Admin, approve/decline only from `PendingApproval`, duplicate emails decided by
   the unique index (`DbUpdateException`), never by a pre-check query.

## Layout

```
/src
  Mya.Api/              ASP.NET Core host, controllers, filters, middleware
  Mya.Application/      use cases, DTOs, validators, profiles, abstractions
  Mya.Domain/           entities, enums, constants
  Mya.Infrastructure/   EF Core, Identity, JWT, email, outbox dispatcher
/tests
  Mya.Application.UnitTests/
  Mya.Api.IntegrationTests/
  Mya.ArchitectureTests/
/web                    Angular app
/docs                   design documents — source of truth
/infra                  provider dashboard deployment notes
/.github/workflows      path-filtered CI: api.yml, web.yml
```

## Current phase

> **Read `docs/08-milestone-handover.md` §"Current state" first.** It is the single resume-here
> document: branch, last commit, which checkpoint is done or in progress, what is verified and
> how, the exact next step, and every open question. This section gives the shape; that file
> gives the position.

**The app is live at `https://moveyourass.gr`** — Render (Docker, Frankfurt, free) serving the
API and the Angular build, Neon PostgreSQL, Resend, Cloudflare DNS (ADR-017). Phase 4 is done;
the runbook is `docs/10-production.md`.

Current work is `feature/b2-video-and-polish`, in four parts: **A** Backblaze B2 video, **B**
cleanup (branded emails, log noise, dead code), **D** authentication and robustness, **C** logo,
navigation and a responsive pass. `docs/08` §"Current state" says which part is where.

**Documentation-first handover is a standing rule.** Update `docs/08` at the end of every
checkpoint and after every meaningful step inside one. A new agent must be able to continue from
the repo alone, so nothing important may live only in a conversation. Record decisions where they
belong as you make them: ADRs in `docs/05`, runbook steps in `docs/10-production.md`, deferred
ideas in `docs/backlog.md`. The handover points at them rather than repeating them.

Decisions still in force:


- **ADR-013 — email 2FA deferred.** Password-only login. `TwoFactorTicket` stays in the schema,
  unused, so enabling 2FA later is code only.
- **ADR-014 — test deferral superseded for this work.** The owner authorized automated backend and frontend tests on 2026-09-26. Keep the entire test suite green.
- **ADR-019 — video is Backblaze B2 via a provider-neutral S3 adapter; Bunny Stream is removed.**
  Plain object storage: no transcoding, no HLS, no provider player, **no webhook**. The browser
  PUTs parts to presigned URLs; the multipart lifecycle is server-side, so the bucket never has
  to expose `ETag` over CORS. Playback is a presigned GET that **works for anyone holding it
  until it expires** — an accepted trade-off, not a bug. Only MP4 and MOV are accepted.
- **`AppUser.FullName` is gone.** Users have `FirstName` and `LastName` (80 each, required) and a
  `MustChangePassword` bit. The seeded Admin's names come from `Seed:AdminFirstName/AdminLastName`.
- **Booking is dropped.** Nothing in `docs/03` §5 applies. Booking items sit in `docs/backlog.md`
  under "Out of scope".

Admin creation now uses an Invited account and a single-use email password setup link (ADR-015).
Console delivery is Development-only; Email:Mode=Resend enables real email with metadata logs.

New unrelated work goes in `docs/backlog.md`.

## Conventions

- `Result<T>` returned from handlers; controllers translate to `IActionResult` via
  `ResultExtensions.ToActionResult`.
- Errors on the wire are RFC 7807 `ProblemDetails` with a stable machine-readable `code`
  (`ErrorCodes`). The Angular error interceptor switches on `code`, never on `message`.
- Business-rule numbers come from `PlatformSettings`; no magic numbers in handlers.
- EF configurations in `Persistence/Configurations/`, one file per entity. No data annotations.
- AutoMapper profiles live beside their feature in `Mya.Application/Features/<Feature>/`.
- Migrations are committed and excluded from style analysis (`.editorconfig`). Never edit an
  applied migration; add a new one.

## Git policy

- Commit style: `feat(auth): ...`, `fix(users): ...`, `docs: ...`, `chore(infra): ...`.
- Branch per phase from `docs/04-roadmap.md`, using `feature/...`, merged through a PR. Small PRs — the design docs
  are worthless if a 4,000-line PR lands that quietly ignores them.
- **Branches are never deleted after merging, locally or on the remote.** They stay as a record
  of what each phase touched. Do not pass `--delete-branch`, do not tick "delete branch" on the
  PR, do not prune.
- Claude Code never runs `git add`, `git commit` or `git push`; the owner handles git.

## Commands

```bash
# backend
dotnet run --project src/Mya.Api            # Development: seeds roles + admin, Swagger at /swagger
dotnet ef migrations add <Name> --project src/Mya.Infrastructure --startup-project src/Mya.Api
dotnet ef database update --project src/Mya.Infrastructure --startup-project src/Mya.Api
dotnet test

# one-shot production seeding (docs/10 §6) — never part of startup
dotnet run --project src/Mya.Api --no-launch-profile -- --seed-admin

# local secrets (never in appsettings, never in docs, never in chat)
dotnet user-secrets set "ConnectionStrings:Default" "<Neon dev POOLED string>" --project src/Mya.Api
dotnet user-secrets set "Jwt:SigningKey" "<32+ random chars>" --project src/Mya.Api
dotnet user-secrets set "Seed:AdminEmail" "<email>" --project src/Mya.Api
dotnet user-secrets set "Seed:AdminPassword" "<password>" --project src/Mya.Api
# only when testing real delivery locally
dotnet user-secrets set "Email:ApiKey" "<Resend API key>" --project src/Mya.Api

# frontend
cd web && npm start
cd web && npm run test
cd web && npm run lint
```

## Production notes

Launch runs on free tiers (ADR-017), with named triggers for leaving them. Full runbook:
`docs/10-production.md`.

- **One Render free web service** serves the API and the Angular build on `https://moveyourass.gr`.
  Same origin, so the `SameSite=Strict` refresh cookie works and production needs no CORS.
- **Neither Render free nor Neon free has an SLA.** Render spins a free service down after 15
  minutes without traffic, so a cron-job.org job calls `/health` every 10 minutes between 06:00
  and 23:00 Europe/Athens. Nights are allowed to spin down, which saves instance-hours and costs
  a slow first request each morning.
- **Four code properties keep the free tier viable.** `/health` never touches the database, no
  background service polls the database, Npgsql `Keepalive` stays disabled, and EF connection
  resiliency stays off. Break any of them and Neon's compute allowance is gone in under a day.
- **Neon's compute limit is a hard stop, not a slowdown.** There is no threshold alerting, so the
  substitute is a weekly manual check of Neon's usage page.
- **Upgrade trigger that matters:** Render Starter once real clients use the site daily. That
  removes the spin-down and the dependency on an external pinger.

## Things that will bite you

- **Neon has two connection strings per branch.** The pooled one (`-pooler` in the hostname) runs
  PgBouncer in transaction mode and is what the app uses. Migrations, `dotnet ef` and
  `--seed-admin` need the **direct** string, because transaction pooling drops the session state
  DDL depends on.
- **Npgsql refuses to write a non-UTC `DateTime` to `timestamp with time zone`.** `UtcDateTimeConverter`
  is what guarantees `Kind=Utc`, so it is load-bearing, not decorative.
- **PostgreSQL `LIKE` is case-sensitive.** SQL Server's default collation made `.Contains`
  case-insensitive for free; it is not any more. Admin user search uses `EF.Functions.ILike` in
  `Mya.Infrastructure`.
- **Duplicate email is SQLSTATE `23505`** on `Npgsql.PostgresException`, not SQL Server's 2601/2627.
  Still decided by the unique index, never by a pre-check query.
- The `must_change_password` claim is baked into the access token. After a password change the
  client must call `/auth/refresh` once to get a token without it.
- **Render terminates TLS at its load balancer and forwards over plain HTTP.** Without forwarded
  headers the app sees Render's address as the client IP, which collapses the rate limiter's IP
  fallback into one bucket for every caller.
- Render sets `PORT` and the container must bind `0.0.0.0` on it. Do not hardcode a port.
- Production requires `Email:Mode=Resend` with an API key and a valid sender at startup.
  Development can use Console delivery.
- Angular Material's date picker is UTC-naive. Normalise at the API boundary, every time.
- Migrations never run automatically on startup in production. Generate an idempotent script and
  apply it as a gated step.
