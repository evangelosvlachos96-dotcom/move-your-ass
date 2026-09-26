# 10 — Production runbook

The production environment for ADR-017: **one Render free web service** serving the API and the
Angular build on `https://moveyourass.gr`, **Neon PostgreSQL** in Frankfurt, **Resend** for email,
**Cloudflare** for DNS. Follow the sections in order; each ends with a check.

This file replaces `docs/10-production-readiness.md` and `docs/10-free-tier-production.md`, both
deleted. Record the names you choose in `infra/README.md` so the environment is reproducible.

> **Secrets never appear in this file, in the repo, or in chat.** Every table below names a
> setting and says where it is set. Values are typed by the owner into user-secrets locally or
> into the Render dashboard in production.

## Status of this runbook

Sections 1–3 and 7 are provider setup and can be done now. Sections 4–6 and 8 depend on code that
lands in later checkpoints: PostgreSQL migrations (checkpoint 2), the Resend sender (checkpoint 3)
and the Dockerfile (checkpoint 4). **Nothing here has been executed yet.** No cloud resources are
provisioned, no domain is configured, and no real email has been sent.

## Free limits and what breaks first

| Piece | Service | Free allowance | What happens at the limit |
|---|---|---|---|
| App (API + Angular) | **Render free web service**, Docker, Frankfurt | 512 MB RAM, 0.1 CPU, 750 instance-hours/month per workspace | Exceeding the hours **suspends every free service in the workspace** until the next month |
| Database | **Neon PostgreSQL** free, `aws-eu-central-1` | 0.5 GB storage, 100 CU-hours/month, 10 branches | Compute is **suspended until the next billing period**; connections drop and new ones fail |
| Email | **Resend** free | 100 emails/day, 3,000/month | Sends are rejected; the outbox retries and then dead-letters |
| DNS and redirect | **Cloudflare** free | 10 single redirect rules | — |
| Keep-alive | **UptimeRobot** free | 50 monitors, 5-minute interval | — |

Keeping one service awake costs about 730 of the 750 instance-hours, so the free budget supports
**exactly one always-on service and no staging environment**. Neon's compute clock runs only while
queries are active, so an idle night costs nothing.

**Upgrade triggers** are in ADR-017. The one that matters: move to Render Starter once real clients
use the site daily, which removes the spin-down and the dependency on an external pinger.

## 0. Accounts

Create accounts at render.com, neon.com, resend.com and uptimerobot.com, and add `moveyourass.gr`
to Cloudflare. None of them requires a card for the free tiers used here. Neon and Render have
**no SLA** on free plans; that is accepted for launch.

## 1. DNS — Cloudflare

The apex and `www` are treated differently on purpose, because Render and Cloudflare disagree
about proxying. Render needs the record it validates to reach Render directly. Cloudflare's
redirect rules only run on proxied records. So the apex points at Render unproxied, and `www` is
answered at Cloudflare's edge and never reaches Render at all.

| Record | Type | Name | Content | Proxy |
|---|---|---|---|---|
| Apex | `CNAME` | `@` | the service's `*.onrender.com` hostname (section 4) | **DNS only (grey)** |
| www | `CNAME` | `www` | `moveyourass.gr` | **Proxied (orange)** |

Notes that will save an hour:

- **Use a CNAME at the apex, not an A record.** Render's documentation says Cloudflare users must
  do this. Cloudflare flattens an apex CNAME to an address automatically, on every plan, with no
  setting to enable.
- **Delete any `AAAA` records.** Render is IPv4 only.
- Keep the apex grey-clouded at least until Render has issued its certificate. Render validates
  over HTTP, and an orange cloud answers that challenge at Cloudflare's edge instead of at Render.
  Proxying the apex afterwards is optional and is **not** recommended at launch: Render renews the
  certificate on its own schedule, and a proxy in front of it is one more thing to debug. If you
  do proxy it later, the SSL/TLS mode must be **Full (strict)**, because Render presents a
  publicly trusted certificate.

Then add the **www redirect**: Rules, then Redirect Rules, then a single redirect. When the
hostname equals `www.moveyourass.gr`, redirect to `https://moveyourass.gr` preserving path and
query, status 301. This needs `www` to be proxied, which is why it is orange above.

**Check:** `www.moveyourass.gr` returns a 301 to the apex. The apex does not resolve to anything
useful yet; that comes in section 4.

## 2. Database — Neon

1. Create a project in region **`aws-eu-central-1` (Frankfurt)**, the same city as the Render
   service.
2. Create two branches: **`production`** and **`dev`**. Each branch has its own endpoint and its
   own connection string. The `dev` branch is what your machine talks to; `production` is only ever
   touched by a deliberate migration or seed command.
3. Neon offers a **pooled** and a **direct** connection string per branch. The pooled hostname
   carries a `-pooler` suffix and runs PgBouncer in transaction mode.

   | Use | Which string |
   |---|---|
   | The running app, on Render and locally | **pooled** (`-pooler`) |
   | `dotnet ef database update`, `dotnet ef migrations script`, `--seed-admin` | **direct** (no `-pooler`) |

   Migrations and DDL need session state that transaction pooling does not provide. Using the
   pooled string for a migration is the classic way to lose an afternoon.

4. The connection string needs `SSL Mode=Require`. It also carries `Connection Idle Lifetime` set
   a little below Neon's five-minute suspend window, so Npgsql always closes an idle socket before
   Neon severs it. Without that the two five-minute timers race, and the first request after an
   idle spell can fail with *"terminating connection due to administrator command"*. Npgsql's
   other pool defaults are correct as they are: a minimum pool size of zero, and `Keepalive`
   disabled, which is what lets Neon scale to zero.

5. Set the local `dev` string with user-secrets, never in a file:

   ```powershell
   dotnet user-secrets set "ConnectionStrings:Default" "<Neon dev pooled string>" --project src/Mya.Api
   ```

**Check:** `dotnet run --project src/Mya.Api` starts and the app answers `/health`. Neon's
dashboard shows the compute waking on the first query and suspending about five minutes after the
last one.

## 3. Email — Resend

1. **Domains, then Add domain:** `moveyourass.gr`. Resend generates the DNS records for that
   domain; copy them from its Records tab into Cloudflare. They are the usual SPF and DKIM set as
   `TXT` and `CNAME` records.
2. **Leave every Resend record DNS-only (grey cloud).** Resend's documentation is explicit that a
   proxied CNAME prevents verification from completing.
3. Wait for the domain to verify. Resend says this is usually within 15 minutes and can take up to
   72 hours.
4. **API Keys, then Create API key**, with send permission. This value goes into the Render
   dashboard and into local user-secrets, nowhere else.
5. The sender address is `noreply@moveyourass.gr`. The `Email:From` setting accepts a display name,
   for example `Move Your Ass <noreply@moveyourass.gr>`.

Delivery is one HTTPS POST from the outbox dispatcher. **SMTP is not used and cannot be**: Render
blocks outbound ports 25, 465 and 587 on free web services. Each send carries the outbox message
Id as Resend's `Idempotency-Key`, so an outbox retry after a crash cannot send the same email
twice. Resend keeps a key for 24 hours; scheduled outbox backoff totals about two and a half hours.
Downtime can extend this past 24 hours, so deduplication is not guaranteed for delayed replays.

**Check:** the domain reads *Verified* in Resend. Actual delivery is verified in section 8.

## 4. The application — Render

1. **New, then Web Service.** Connect the GitHub repository, branch `main`.
2. Runtime **Docker**, Dockerfile at the repository root, region **Frankfurt**, instance type
   **Free**.
3. **Health check path:** `/health`. Render uses it to gate zero-downtime deploys and to restart a
   wedged instance. It must stay free of database work.
4. Environment variables. Render provides `PORT` itself; do not set it.

   | Name | Where the value comes from | Secret |
   |---|---|---|
   | `ASPNETCORE_ENVIRONMENT` | `Production` | no |
   | `ConnectionStrings__Default` | Neon **production** branch, **pooled** string | **yes** |
   | `Jwt__SigningKey` | 64 random characters, generated once | **yes** |
   | `App__PublicOrigin` | `https://moveyourass.gr` | no |
   | `Email__Mode` | `Resend` | no |
   | `Email__ApiKey` | Resend API key from section 3 | **yes** |
   | `Email__From` | `Move Your Ass <noreply@moveyourass.gr>` | no |

   The JWT issuer and audience come from `appsettings.Production.json` and are not secrets.
   `Seed__*` is deliberately **not** set here: the first Admin is created by a one-shot local
   command in section 6, so the seed password never lives in the dashboard.

5. **Settings, then Custom Domains:** add `moveyourass.gr`. Render issues and renews the
   certificate. Do not add `www`; Cloudflare redirects it before it reaches Render.
6. Auto-deploy from `main` is on by default. Leave it on. To skip a deploy for a documentation
   commit, put `[skip render]` in the commit message.

**Check:** `https://moveyourass.gr/health` returns `{"status":"ok"}` over a valid certificate, and
the Angular app loads at the apex.

## 5. Schema — apply migrations

Migrations never run on startup. Apply them deliberately, from your machine, against the
**direct** (non-pooled) production string:

```powershell
$env:ConnectionStrings__Default = "<Neon production DIRECT string>"
dotnet ef database update --project src/Mya.Infrastructure --startup-project src/Mya.Api
Remove-Item Env:ConnectionStrings__Default
```

To review the SQL first, run the `api` workflow manually. Its `migrate` job generates an
idempotent PostgreSQL script and uploads it as an artifact, which can also be pasted into Neon's
SQL editor.

**Check:** the `production` branch has the `AspNetUsers`, `OutboxMessage`, `PasswordInvitation`,
`RefreshToken`, `TwoFactorTicket` and `IdempotencyRecord` tables, and `__EFMigrationsHistory`
lists every migration under `src/Mya.Infrastructure/Persistence/Migrations`.

## 6. First Admin — one-shot seed

`--seed-admin` creates both roles and the Admin, then exits without starting the web host.
`--no-launch-profile` stops `launchSettings.json` from forcing Development, which would read your
local user-secrets instead of these values. Use the **direct** production string.

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:ConnectionStrings__Default = "<Neon production DIRECT string>"
$env:Seed__AdminEmail = "<trainer's email>"
$env:Seed__AdminPassword = "<strong temporary password>"
$env:Seed__AdminFirstName = "<first name>"
$env:Seed__AdminLastName = "<last name>"
$env:Jwt__SigningKey = "<the same key set on Render>"
$env:App__PublicOrigin = "https://moveyourass.gr"
$env:Email__Mode = "Resend"
$env:Email__ApiKey = "<Resend API key>"
$env:Email__From = "Move Your Ass <noreply@moveyourass.gr>"
dotnet run --project src/Mya.Api --no-launch-profile -- --seed-admin
Remove-Item Env:Seed__AdminPassword, Env:ConnectionStrings__Default, Env:Email__ApiKey, Env:Jwt__SigningKey
```

The email and JWT variables are present only because startup options validation runs before the
seeder does; the seeder itself sends nothing. The command is idempotent and safe to run twice. The
Admin changes the password from the profile page after the first login.

## 7. Keep-alive — UptimeRobot

A Render free service spins down after 15 minutes without inbound traffic and takes about a minute
to come back. One monitor prevents that.

- Type **HTTP(s)**, URL `https://moveyourass.gr/health`, interval **5 minutes**.
- Optionally make it a keyword monitor expecting `ok`, so that a 200 from a broken build still
  alerts.
- Point the alert at an address the owner reads. This monitor is both the keep-alive and the only
  uptime alerting the environment has.

`/health` performs no database work, so this keeps Render awake without keeping Neon awake. That
separation is the whole reason the free tier works; see ADR-017.

**Check:** the monitor reports up, and Render's metrics show no spin-down over a few hours.

## 8. Verify end to end

1. Open the site on a phone. Log in as the Admin and change the password.
2. Refresh a deep link such as `/admin/users`. The SPA fallback must serve it rather than 404.
3. Leave the tab open past the 15-minute access-token lifetime and confirm the session survives a
   silent refresh. That is the same-origin `SameSite=Strict` cookie doing its job.
4. Invite a test client at an address you can read. The log shows `Outbox: sent PasswordInvitation`
   within seconds, the email arrives, its link opens the site, setting a password activates the
   account, and reusing the link fails.
5. Register a second client from the public form. The Admin receives the notification, approves it,
   and the client receives the approval email.
6. Check spam placement for both messages. A verified sending domain should land in the inbox.
7. Run `docs/07-manual-test-checklist.md` against the real URL.

## Living within the free limits

- **Weekly, check Neon's usage page.** This is the substitute for a budget alert. Neon documents a
  usage dashboard but no threshold alerting, so nothing will warn you before the compute allowance
  runs out, and running out is a hard stop rather than a slowdown. If usage is tracking above half
  the monthly allowance by mid-month, something is querying the database on a timer; find it.
- **Watch Render's instance hours** in the same weekly pass. One always-on service is about 730 of
  750, so there is no headroom for a second free service in the workspace.
- **Emails** are queued in the database and sent within seconds. A failed send retries on backoff
  and dead-letters after five attempts, with the reason in `LastError`.
- **Four code properties keep this working** and must survive future changes: `/health` does no
  database work, no background service polls the database, Npgsql `Keepalive` stays disabled, and
  EF connection resiliency stays off. ADR-017 explains why each one matters.
