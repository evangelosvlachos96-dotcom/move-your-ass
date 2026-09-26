# Move Your Ass

A private workout video platform for one trainer and approved clients. Booking, calendars,
payments and multiple trainers are outside scope.

## Accounts

- Clients register with a password. The account stays PendingApproval and an email is queued
  to the admin. Approval activates it and queues an email to the client; decline also notifies them.
- Admin can list, search, edit and manage users from the dashboard.
- Admin-created clients start as Invited. An email contains a single-use password setup link,
  valid for 24 hours by default. Setting a password activates the account; normal login follows.
- Admin can resend an invitation. Previous unconsumed links are invalidated.
- Login is password-only. Approval and invitation emails are onboarding, not login 2FA.
- JWT access tokens, rotating refresh cookies and one active session per account are implemented.

## Stack and layout

ASP.NET Core / .NET 10, EF Core and PostgreSQL; Angular standalone components, signals and
Angular Material. Email goes through the Resend HTTPS API. Bunny Stream is the implemented video
provider.

Production is one Render web service (Docker, Frankfurt) serving the API and the Angular build
from the same origin on `https://moveyourass.gr`, with a Neon database and Cloudflare DNS.
See ADR-017 in docs/05-decisions.md and the runbook in docs/10-production.md.

- src/Mya.Api: HTTP host, controllers, authorization and middleware
- src/Mya.Application: use cases, validation, DTOs and external-service interfaces
- src/Mya.Domain: entities and enums
- src/Mya.Infrastructure: Identity, EF Core, migrations and transactional email outbox
- web: Angular app
- tests: existing settings, persistence and architecture checks
- docs: product scope and architecture
- infra: deployment checklist

## Local development

Prerequisites: .NET 10 SDK, Node 22+, a PostgreSQL database and dotnet-ef. The intended local
database is the Neon `dev` branch; any PostgreSQL instance works.
Store database, JWT and seed credentials in user-secrets for src/Mya.Api; never commit them.
See appsettings.json and appsettings.Development.json for configuration keys.

```powershell
dotnet restore
dotnet ef database update --project src/Mya.Infrastructure --startup-project src/Mya.Api
dotnet run --project src/Mya.Api
# Separate terminal
cd web
npm ci
npm start
```

Apply migrations explicitly; startup seeds the first admin in Development but does not migrate.
The local launch profile exposes Swagger at http://localhost:5077/swagger; Angular runs at :4200.

## Email

Email:Mode=Console is the Development default. Rendered messages, including invitation links,
appear in local logs. Treat these logs as sensitive.

Email:Mode=Resend sends real mail through Resend's HTTPS API and logs delivery metadata without
the body or setup token. Configure ApiKey and From in user-secrets or deployment configuration.
Production requires Resend mode and validates it at startup. There is no SMTP mode: Render blocks
outbound SMTP ports on free web services, which is why the provider is reached over HTTPS.
See docs/09-email-setup.md. Code support does not mean a live provider is already configured.

## Validation and milestones

**Start at docs/08-milestone-handover.md §"Current state".** It records the branch, the last
commit, what is verified and what is not, and the exact next step. The owner commits and pushes
before further work; the next checkpoint waits for explicit approval.

```powershell
dotnet test -m:1
cd web
npm run lint
npm run build
```

Run docs/07-manual-test-checklist.md for browser and account-flow verification.
docs/04-roadmap.md is the single milestone sequence. Videos are designed in docs/06-video-catalogue.md
and are not implemented yet. Deployment validation precedes video implementation.

Private and proprietary. All rights reserved.

Video setup and acceptance: [docs/11-video-operations.md](docs/11-video-operations.md).
Trainer guide: [docs/12-trainer-guide.md](docs/12-trainer-guide.md).
Video uploads remain disabled until Bunny settings are supplied. Run backend and frontend tests before deployment.
