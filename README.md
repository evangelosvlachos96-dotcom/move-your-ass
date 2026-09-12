# Training Platform

A private platform for a personal trainer and their clients. Clients register, are approved
manually by the trainer, then log in to watch the trainer's video library and book 1:1 sessions
on a calendar.

Built as a production system for a single trainer with tens to low hundreds of clients. Not a
marketplace, not multi-tenant.

---

## What it does

**For clients**
- Register with email; account stays inactive until the trainer approves it
- Log in with email + password, confirmed by a one-time code sent to their inbox
- Browse and stream the trainer's video library
- Book 1:1 sessions from a calendar showing only free slots — up to 5 active bookings at a time
- Request a change or cancellation of an existing booking, and withdraw that request while it is
  still pending

**For the trainer (admin)**
- Review pending registrations and approve, reject, or later suspend any account
- Upload and publish video content
- Publish weekly availability; move or cancel any booked session directly
- Receive an email on every client change request, and approve or reject it

---

## Stack

| Layer | Technology |
|---|---|
| API | ASP.NET Core Web API, .NET 10 (LTS) |
| Data | EF Core 10, Azure SQL Database |
| Validation | FluentValidation |
| Auth | ASP.NET Core Identity, JWT access tokens, rotating refresh tokens |
| Frontend | Angular (standalone components, signals), Angular Material |
| Storage | Azure Blob Storage, HLS video with scoped SAS |
| Email | SMTP via Brevo / Resend, dispatched through a transactional outbox |
| Hosting | Azure App Service (API), Azure Static Web Apps (SPA) |
| Tests | xUnit, NSubstitute, Testcontainers · Vitest, Angular Testing Library |

---

## Architecture at a glance

```
Angular SPA ──────────────────────────────┐  video bytes, direct (SAS)
  │                                        ▼
  │ JSON + Bearer JWT            Azure Blob Storage
  ▼
ASP.NET Core API ──▶ Azure SQL
  │
  └── outbox ──▶ email provider
```

Four backend projects, layered, with feature folders inside each layer:

```
src/
  Mya.Api/              controllers, middleware, DI composition
  Mya.Application/      use cases, DTOs, validators, abstractions
  Mya.Domain/           entities, enums, domain exceptions
  Mya.Infrastructure/   EF Core, Identity, blob, email
tests/
  Mya.Application.UnitTests/
  Mya.Api.IntegrationTests/
  Mya.ArchitectureTests/
web/                   Angular app
docs/                  design documents — the source of truth
infra/                 Bicep / az CLI deployment
```

This is a **mono-repo** — API, SPA, infra, and docs ship together. An API contract change is one
commit and one PR. CI stays independent via path filters in `.github/workflows`.

`Mya.Application` may not reference EF Core or ASP.NET. This is enforced by a test, not by
convention.

Video bytes never pass through the API — the API only mints short-lived SAS URLs and the browser
fetches from Blob directly.

---

## Two things worth reading before contributing

**Booking conflicts are resolved by the database, not the UI.** Claiming a slot is a single
conditional `UPDATE` whose affected-row count decides the winner. Polling, tab sync, and
idempotency keys sit on top as UX; none of them is the guarantee. See `docs/03`.

**Every user-initiated mutation takes an `Idempotency-Key` header.** The key is generated when
the user forms the intent (dialog opens), not when the button is clicked. Deduped per user for
24 hours, replaying the original response.

---

## Getting started

Prerequisites: .NET 10 SDK, Node 22+, Docker (for the test database), Azure CLI.

```bash
git clone <repo-url> && cd training-platform

# backend
dotnet restore
dotnet user-secrets set "ConnectionStrings:Default" "<local sql connection string>" \
  --project src/Mya.Api
dotnet ef database update --project src/Mya.Infrastructure --startup-project src/Mya.Api
dotnet run --project src/Mya.Api          # https://localhost:7001

# frontend
cd web && npm ci && npm start            # http://localhost:4200
```

The seeder creates the two roles and the first admin from configuration. There is no public path
to becoming an admin.

```bash
dotnet test                  # unit + integration + architecture tests
cd web && npm run test       # component and service tests
cd web && npm run lint       # includes the rule banning HttpClient outside core/http
```

---

## Configuration

Never committed. Local development uses user-secrets; Azure uses App Service configuration
backed by Key Vault.

| Key | Purpose |
|---|---|
| `ConnectionStrings:Default` | Azure SQL |
| `Jwt:SigningKey` | 256-bit HS256 key |
| `Jwt:Issuer`, `Jwt:Audience` | token validation |
| `Email:Host`, `Email:Port`, `Email:User`, `Email:Password`, `Email:From` | SMTP |
| `Storage:ConnectionString`, `Storage:VideoContainer` | Blob |
| `Cors:AllowedOrigin` | exact SPA origin — no wildcards |
| `Seed:AdminEmail`, `Seed:AdminPassword` | first admin, rotate after first login |

---

## Deployment

GitHub Actions on push to `main`: the API runs tests then publishes to App Service; the SPA runs
lint, tests and build then publishes to Static Web Apps. Migrations are applied as a gated
workflow step from an idempotent script, never automatically on startup in production.

**Production baseline** — the Azure free tiers carry no SLA and are documented for
proof-of-concept use. Before real clients:

| Resource | Minimum for production |
|---|---|
| App Service Plan | B1 (no sleeping, no CPU quota) — S1 if you want a staging slot |
| SQL Database | Serverless General Purpose with a real min-vCore floor, PITR enabled |
| Storage | StorageV2 LRS Hot, private containers, soft delete on |
| Monitoring | Application Insights with alerts on failure rate and CPU |
| Domain | Custom domain with a managed certificate |

Set a budget alert in Cost Management on day one. Azure has no hard spend cap.

---

## Documentation

| File | Contents |
|---|---|
| `CLAUDE.md` | working rules and commands, for AI-assisted sessions |
| `docs/01-system-design.md` | containers, Azure topology, video encoding and storage |
| `docs/02-backend-architecture.md` | layering, conventions, error model, testing strategy |
| `docs/03-auth-and-concurrency.md` | auth flows, sessions, idempotency, booking conflicts |
| `docs/04-phase-1-scope.md` | current milestone, API surface, acceptance criteria |
| `docs/05-decisions.md` | ADRs — what was chosen, what was rejected, what it costs |
| `docs/backlog.md` | everything deliberately out of the current phase |
| `infra/README.md` | Azure resource list and pre-production checklist |

When a decision in `docs/05` starts to hurt, amend the ADR rather than quietly working around it.

---

## Status

**Phase 1 — authentication and deployment.** Registration, admin approval, email 2FA, roles,
session control, and a deployed Azure environment with a deliberately plain UI.

Booking, calendar, videos, and change requests are designed but not yet built. See
`docs/04-phase-1-scope.md` for what is in and out of the current milestone.

## License

Private and proprietary. All rights reserved.
