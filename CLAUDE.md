# CLAUDE.md

Context for Claude Code sessions in this repository. Read `docs/` before making architectural
changes. This is a **mono-repo**: backend, frontend, infra, and docs live together.

## What this is

A private training platform. One trainer (Admin) publishes video content and 1:1 session slots.
Clients register, get manually approved by the Admin, then log in to watch videos and book
sessions on a calendar.

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
| Hosting | Azure App Service (API) + Static Web Apps (SPA) | — |
| Tests | xUnit / NSubstitute / Shouldly, Vitest / Testing Library | — |

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
4. **Slot booking is one atomic SQL UPDATE.** Never `SELECT` then `UPDATE`. See `docs/03` §5.1.
   Nothing in the UI, session model, or token design is allowed to become the guarantee.
5. **Every mutating endpoint a user can double-submit takes an `Idempotency-Key` header.**
   The key is generated when the dialog opens, not when the button is clicked.
6. **`Mya.Application` must not reference EF Core or ASP.NET.** Enforced by `Mya.ArchitectureTests`.
7. **No secrets in the repo.** Local: user-secrets. Azure: App Service config backed by Key Vault.

## Layout

```
/src
  Mya.Api/              ASP.NET Core host, controllers, middleware
  Mya.Application/      use cases, DTOs, validators, profiles, abstractions
  Mya.Domain/           entities, enums, domain exceptions
  Mya.Infrastructure/   EF Core, Identity, blob, email
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

**Phase 1 — auth skeleton + deployment.** See `docs/04-phase-1-scope.md`.

Do not build booking, videos, or change requests yet. The goal of phase 1 is a deployed, working
login on Azure with two roles and a plain UI. Nothing more. If a task is not in `docs/04` §1, it
goes in `docs/backlog.md`.

## Conventions

- `Result<T>` returned from handlers; controllers translate to `IActionResult`.
- Errors on the wire are RFC 7807 `ProblemDetails` with a stable machine-readable `code`.
  The Angular error interceptor switches on `code`, never on `message`.
- EF configurations in `Persistence/Configurations/`, one file per entity. No data annotations.
- AutoMapper profiles live beside their feature in `Mya.Application/Features/<Feature>/`.
  Add `AssertConfigurationIsValid()` to a unit test so a broken profile fails the build.
- Migrations are committed. Never edit an applied migration; add a new one.
- Commit style: `feat(auth): ...`, `fix(booking): ...`, `docs: ...`, `chore(infra): ...`.
- Branch per step from `docs/04` §7. Small PRs — the design docs are worthless if a 4,000-line
  PR lands that quietly ignores them.

## Commands

```bash
# backend
dotnet run --project src/Mya.Api
dotnet ef migrations add <Name> --project src/Mya.Infrastructure --startup-project src/Mya.Api
dotnet ef database update --project src/Mya.Infrastructure --startup-project src/Mya.Api
dotnet test

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

- Serverless SQL auto-pause means a cold first request. Expected, not a bug — but measure it
  (acceptance criterion 14 in `docs/04`) and set the min-vCore floor accordingly.
- SQLite cannot host the concurrency tests (no `rowversion`, no filtered indexes). Integration
  tests use SQL Server via Testcontainers.
- Angular Material's date picker is UTC-naive. Normalise at the API boundary, every time.
- Migrations never run automatically on startup in production. Generate an idempotent script and
  apply it as a gated workflow step.
