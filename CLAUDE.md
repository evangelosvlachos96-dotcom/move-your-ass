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
| DB | Azure SQL Database | serverless General Purpose |
| Frontend | Angular, standalone + signals | latest (`ng new` default) |
| UI kit | Angular Material | matching Angular major |
| Auth | ASP.NET Core Identity + JWT | — |
| Video | Bunny Stream behind `IVideoStorage` | — |
| Hosting | Azure App Service (API) + Static Web Apps (SPA) | — |
| Tests | xUnit / NSubstitute / Shouldly, Vitest / Testing Library | deferred beyond architecture tests (ADR-014) |

## Licensing notes

- **AutoMapper and MediatR are fine to use.** Both are free under the Community licence for
  companies and individuals under **$5,000,000 gross annual revenue**. A licence key is required
  for auditing — set it via `AUTOMAPPER_LICENSE_KEY` / `MEDIATR_LICENSE_KEY` environment
  variables, never in `appsettings.json`.
- **FluentAssertions v8+ is not free for commercial use.** Pin v7 (Apache 2.0) or use Shouldly.
  This repo uses Shouldly.
- Register both keys in App Service configuration before the first production deploy, or startup
  logs will fill with licence warnings.

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
   Video creation when it lands; `IdempotencyRecord` is kept for it. The key is generated when
   the dialog opens, not when the button is clicked.
6. **`Mya.Application` may reference `Microsoft.EntityFrameworkCore` (the abstractions: `DbSet<T>`,
   async LINQ) but must not reference `Microsoft.EntityFrameworkCore.SqlServer` or
   `Microsoft.AspNetCore.*`. `Mya.Domain` references nothing.** Enforced by `Mya.ArchitectureTests`;
   see `docs/02` §1 for the exact statement.
7. **No secrets in the repo.** Local: user-secrets. Azure: App Service config backed by Key Vault.
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
/infra                  Bicep / az CLI deployment
/.github/workflows      path-filtered CI: api.yml, web.yml
```

## Current phase

**Phase 2 — backend auth and user management.** See `docs/04-roadmap.md` §"Phases" and
§"API surface after phase 2". Phase 1 (foundation) is done.

Decisions in force for this phase:

- **ADR-013 — email 2FA deferred.** Password-only login. `TwoFactorTicket` stays in the schema,
  unused, so enabling 2FA later is code only.
- **ADR-014 — automated tests deferred beyond the architecture tests.** Do not add unit or
  integration tests until the catalogue has real content; `Mya.ArchitectureTests` must stay green.
- **`AppUser.FullName` is gone.** Users have `FirstName` and `LastName` (80 each, required) and a
  `MustChangePassword` bit. The seeded Admin's names come from `Seed:AdminFirstName/AdminLastName`.
- **Booking is dropped.** Nothing in `docs/03` §5 applies. Booking items sit in `docs/backlog.md`
  under "Dropped — may return".

If a task is not in the current phase of `docs/04-roadmap.md`, it goes in `docs/backlog.md`.

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
- Commit style: `feat(auth): ...`, `fix(users): ...`, `docs: ...`, `chore(infra): ...`.
- Branch per phase from `docs/04-roadmap.md`. Small PRs — the design docs are worthless if a
  4,000-line PR lands that quietly ignores them.

## Commands

```bash
# backend
dotnet run --project src/Mya.Api            # Development: seeds roles + admin, Swagger at /swagger
dotnet ef migrations add <Name> --project src/Mya.Infrastructure --startup-project src/Mya.Api
dotnet ef database update --project src/Mya.Infrastructure --startup-project src/Mya.Api
dotnet test

# local secrets (never in appsettings)
dotnet user-secrets set "ConnectionStrings:Default" "<LocalDB connection string>" --project src/Mya.Api
dotnet user-secrets set "Jwt:SigningKey" "<32+ random chars>" --project src/Mya.Api
dotnet user-secrets set "Seed:AdminEmail" "<email>" --project src/Mya.Api
dotnet user-secrets set "Seed:AdminPassword" "<password>" --project src/Mya.Api

# frontend
cd web && npm start
cd web && npm run test
cd web && npm run lint
```

## Production notes

This ships to real clients, so the Azure free tiers are not the target:

- **App Service F1 has no SLA**, sleeps after 20 minutes idle, and stops outright at 60 CPU-min/day.
  Use it for the dev environment only. Production is **B1 minimum**, S1 if a staging slot is wanted.
- **The SQL free offer has no SLA** and is documented by Microsoft for proof-of-concept use.
  Production uses serverless General Purpose with a real min-vCore floor and PITR enabled.
- Budget roughly €25–35/month for the production environment. Set a budget alert on day one —
  Azure has no hard spend cap.

## Things that will bite you

- Serverless SQL auto-pause means a cold first request. Expected, not a bug — measure it during
  phase 4 and set the min-vCore floor accordingly.
- SQLite cannot host SQL Server semantics (no `rowversion`, different concurrency). The seeder
  test runs on SQLite in-memory because it needs neither; anything touching concurrency uses
  SQL Server via Testcontainers.
- The `must_change_password` claim is baked into the access token. After a password change the
  client must call `/auth/refresh` once to get a token without it.
- The refresh cookie is `Secure`. Over plain `http://localhost` browsers still accept it, but
  behind a TLS-terminating proxy the API must see `X-Forwarded-Proto` (phase 4).
- Outside Development the email sender is `UnconfiguredEmailSender`: outbox rows fail and
  dead-letter with a clear `LastError` until SMTP lands in phase 4.
- Angular Material's date picker is UTC-naive. Normalise at the API boundary, every time.
- Migrations never run automatically on startup in production. Generate an idempotent script and
  apply it as a gated workflow step.
