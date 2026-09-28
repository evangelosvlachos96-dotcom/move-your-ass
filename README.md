# Move Your Ass

A private workout-video library for **one trainer and their approved clients**, live at
[moveyourass.gr](https://moveyourass.gr). Clients register, the trainer approves them by hand,
and approved clients sign in to browse and watch categorised workout videos. The interface is in
Greek.

Not a marketplace, not multi-tenant, no booking, no calendars, no payments. One admin, tens to
low hundreds of clients.

**The source is public; the data is not.** See [SECURITY.md](SECURITY.md) for how to report a
vulnerability and for what this repository deliberately does and does not contain.

## What it does

**Accounts.** Clients register with a password; the account sits in `PendingApproval` and the
admin is emailed. Approving activates it and emails the client; declining also notifies them.
Admin-created clients start as `Invited` and receive a single-use password-setup link valid for
24 hours. Login is password-only (ADR-013 defers 2FA), with JWT access tokens, rotating refresh
cookies with reuse detection, and one active session per account.

**Videos.** The trainer uploads MP4 or MOV straight from the browser to Backblaze B2 as presigned
multipart parts; the multipart lifecycle is server-side, so the bucket never has to expose `ETag`
over CORS (ADR-019). A cover image can be uploaded per video — otherwise a frame captured at
upload is used, and otherwise a branded placeholder. Playback is a short-lived presigned GET.
Everything stored counts against a configured storage cap, covers included.

**The trainer's page.** `Ο γυμναστής σου` is a page every signed-in user can see and only the
admin can edit in place: photo, name, tagline, a sanitised-markdown biography, and optional
contact links. Clients can send the trainer a message from it; the mail goes out through the
transactional outbox with `Reply-To` set to the client.

## Stack

ASP.NET Core / .NET 10 and EF Core on PostgreSQL (Neon); Angular standalone components with
signals and Angular Material. Email through the Resend HTTPS API behind `IEmailSender`. Video in
Backblaze B2 through its S3-compatible API behind `IVideoStorage`.

Production is **one** Render web service (Docker, Frankfurt, free tier) serving the API and the
Angular build from the same origin, which is what lets the refresh cookie stay `SameSite=Strict`
with no CORS at all. DNS is Cloudflare. See ADR-016 and ADR-017 in
[docs/05-decisions.md](docs/05-decisions.md) and the runbook in
[docs/10-production.md](docs/10-production.md).

```
src/Mya.Api             HTTP host, controllers, authorization, middleware
src/Mya.Application     use cases, validation, DTOs, external-service interfaces
src/Mya.Domain          entities and enums; references nothing
src/Mya.Infrastructure  Identity, EF Core, migrations, S3 adapter, email outbox
web                     Angular app, unit tests and the Playwright suite
docs                    product scope, decisions, runbooks — the source of truth
infra                   provider dashboard notes
```

The layering is enforced by `tests/Mya.ArchitectureTests`, not by convention.

## Local development

Prerequisites: .NET 10 SDK, Node 22+, a PostgreSQL database, `dotnet-ef`. The intended local
database is the Neon `dev` branch; any PostgreSQL instance works. **Never point local development
at the production database.**

Secrets go in .NET user-secrets for `src/Mya.Api` — never in `appsettings.json`, never in the
repository. `appsettings.json` and `appsettings.Development.json` list every key.

```powershell
dotnet restore
dotnet ef database update --project src/Mya.Infrastructure --startup-project src/Mya.Api
dotnet run --project src/Mya.Api        # http://localhost:5077, Swagger at /swagger

# separate terminal
cd web
npm ci
npm start                               # http://localhost:4200
```

The Angular dev server proxies `/api` to the API (`web/proxy.conf.json`), so development is
same-origin exactly like production. Migrations are applied explicitly; startup seeds the first
admin in Development but never migrates.

## Tests

```powershell
dotnet build                            # must stay at zero warnings
dotnet test

cd web
npm run lint
npm run test
npm run build -- --configuration production
npx playwright test                     # needs both servers running; see web/playwright.config.ts
```

The Playwright suite runs Chromium at phone, tablet and desktop sizes and **WebKit at phone and
tablet**, because the trainer's clients are on iPhones, where every browser is WebKit underneath.
Signed-in projects need `E2E_ADMIN_EMAIL` / `E2E_ADMIN_PASSWORD` and `E2E_CLIENT_EMAIL` /
`E2E_CLIENT_PASSWORD`; without them those tests skip, so CI without a database still runs the
public routes.

`docs/07-manual-test-checklist.md` covers what a browser suite cannot.

## Email

`Email:Mode=Console` is the Development default: rendered messages, invitation links included,
appear in the local logs. **Treat those logs as sensitive.** `Email:Mode=Resend` sends real mail
and logs delivery metadata without the body or the token. Production requires Resend mode and
validates it at startup. There is no SMTP mode — Render blocks outbound SMTP on free web
services, which is why the provider is reached over HTTPS. See
[docs/09-email-setup.md](docs/09-email-setup.md).

## Where to start reading

**[docs/08-milestone-handover.md](docs/08-milestone-handover.md) §"Current state"** — the single
resume-here document: branch, last commit, what is verified and how, what is not, and the exact
next step. Everything else hangs off it:

- [docs/05-decisions.md](docs/05-decisions.md) — the ADRs, and why each alternative lost
- [docs/10-production.md](docs/10-production.md) — the production runbook
- [docs/11-video-operations.md](docs/11-video-operations.md) — B2 setup and acceptance
- [docs/12-trainer-guide.md](docs/12-trainer-guide.md) — the guide for the trainer, in Greek
- [CLAUDE.md](CLAUDE.md) — the working rules for this repository

## Licence

**All rights reserved.** There is deliberately **no `LICENSE` file**: without one, default
copyright applies and no permission to use, copy, modify or distribute this code is granted.
It is published so it can be read — as a portfolio piece and so that anyone can check how the
trainer's clients' data is handled — not so it can be reused.

Created by [Evangelos Vlachos](https://github.com/evangelosvlachos96-dotcom).
