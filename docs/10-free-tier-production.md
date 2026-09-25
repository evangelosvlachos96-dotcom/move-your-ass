# 10 — Free-tier production runbook

The production environment at €0/month (ADR-016). Follow the steps in order; each ends with a
check. Record the names you choose in `infra/README.md` so the environment is reproducible.

| Piece | Service | Free allowance |
|---|---|---|
| App (API + Angular) | Azure App Service **F1, Linux, .NET 10** | 60 CPU-min/day, 1 GB RAM, sleeps after ~20 min idle, `*.azurewebsites.net` only |
| Database | **Azure SQL Database free offer** (serverless GP) | 100,000 vCore-seconds + 32 GB data per month, per database |
| Email | **Brevo** SMTP relay | 300 emails/day |

Pick **one region** for the App Service and the database (e.g. West Europe) — cross-region traffic
is billed and adds latency.

## 0. Accounts and the spend guard

1. Azure: a Pay-As-You-Go subscription (a card is required; the free offers below cost nothing
   while you stay inside them). **Cost Management → Budgets:** create a €5/month budget with an
   email alert at 50% and 100%. Azure has no hard cap; this alert is the guard.
2. Brevo: sign up at brevo.com. New accounts are reviewed before they may send; wait for approval.

## 1. Database — Azure SQL free offer

1. Portal → Azure SQL → **Create SQL database**, and accept the **Apply offer** / "Start free" banner.
   The cost summary must read **$0**.
2. New server `sql-mya-prod`, authentication **SQL authentication** (keep the admin login and
   password in your password manager). Database `sqldb-mya-prod`.
3. **Behavior when free limit reached: Auto-pause the database until next month.** Never choose
   "continue with charges" for launch; it cannot be switched back.
4. Auto-pause delay: the minimum the portal allows. Every minute awake costs free vCore-seconds.
5. Networking: public endpoint; add **your current client IP** (needed for steps 4–5). The App
   Service's IPs are added in step 2.
6. Connection string (ADO.NET), with a longer timeout for resume-from-pause:

   ```
   Server=tcp:sql-mya-prod.database.windows.net,1433;Initial Catalog=sqldb-mya-prod;User ID=<login>;Password=<password>;Encrypt=True;TrustServerCertificate=False;Connect Timeout=60;
   ```

**Check:** the database overview shows a *Free amount remaining* tile. In **Monitoring → Alerts**
add an alert on the *Free amount remaining* metric below 10,000 seconds.

## 2. App — App Service F1

1. Portal → **Create Web App**: name `app-mya-prod` (becomes `https://app-mya-prod.azurewebsites.net`),
   publish **Code**, runtime **.NET 10**, OS **Linux**, same region, pricing plan **Free F1**.
2. **Settings → Configuration → General settings:** *HTTPS Only* **On**; *SCM Basic Auth Publishing
   Credentials* **On** (needed to download the publish profile for GitHub Actions).
3. **Settings → Environment variables → App settings** (Linux uses `__` for `:`):

   | Name | Value |
   |---|---|
   | `ASPNETCORE_ENVIRONMENT` | `Production` |
   | `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `true` |
   | `ConnectionStrings__Default` | the connection string from step 1 |
   | `Jwt__SigningKey` | 64 random characters (e.g. `openssl rand -base64 48`) |
   | `Cors__AllowedOrigin` | `https://app-mya-prod.azurewebsites.net` (used for email links) |
   | `Email__User` | Brevo SMTP login (step 3) |
   | `Email__Password` | Brevo SMTP key (step 3) |
   | `Email__From` | `Move Your Ass <your-verified-sender@example.com>` |

   Host, port and TLS for Brevo, and the JWT issuer/audience, come from `appsettings.Production.json`.
   App settings are encrypted at rest; Key Vault is optional and not free-of-charge, so it is
   skipped at launch.
4. **Do not** enable App Service *Health check* and do not add uptime pingers. Anything that wakes
   the app every few minutes keeps the database awake too and burns the free vCore-seconds.
5. **Properties → Outbound IP addresses → Additional (possible) outbound IPs:** add each of them
   to the SQL server's firewall (step 1.5). The simpler alternative, *Allow Azure services and
   resources to access this server*, admits every Azure customer's IP range to the login prompt;
   prefer the explicit list.

**Check:** `https://app-mya-prod.azurewebsites.net/health` returns `{"status":"ok"}` after the first
deploy (step 6).

## 3. Email — Brevo

1. **Senders, domains & dedicated IPs → Senders → Add a sender**: the address clients will see;
   confirm the verification email.
2. **SMTP & API → SMTP**: copy the SMTP **login** (looks like `xxxx@smtp-brevo.com`, not your
   account email) and **generate an SMTP key**. These are `Email__User` / `Email__Password`.
3. Deliverability: sending *as* a free mailbox (gmail.com, outlook.com…) through a third-party
   relay fails those domains' DMARC alignment, so messages may land in spam. For launch that is
   acceptable if you tell the first clients to check spam. The fix is a domain (~€10/year): add it
   under *Domains*, publish the SPF/DKIM/DMARC records Brevo shows, and send from it.

**Fallback if Brevo messages do not arrive:** Gmail SMTP with an app password works with no domain
(needs 2-Step Verification on the Google account): `Email__Host=smtp.gmail.com`, `Email__Port=587`,
`Email__Security=StartTls`, `Email__User` and `Email__From` = the Gmail address,
`Email__Password` = the 16-character app password.

## 4. Schema — apply migrations from your machine

Migrations never run on startup. From the repo root, with your IP allowed in the SQL firewall:

```powershell
dotnet ef database update `
  --project src/Mya.Infrastructure --startup-project src/Mya.Api `
  --connection "<connection string from step 1>"
```

To review first, run the `api` workflow manually: its `migrate` job uploads an idempotent
`migrate.sql`, which can also be executed in the portal's *Query editor*.

**Check:** the database has the `AspNetUsers`, `OutboxMessage` and `PasswordInvitation` tables and
`__EFMigrationsHistory` lists every migration in `src/Mya.Infrastructure/Persistence/Migrations`.

## 5. First Admin — one-shot seed

`--seed-admin` creates both roles and the Admin, then exits without starting the web host.
`--no-launch-profile` stops launchSettings.json from forcing Development (which would read your
local user-secrets instead):

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:ConnectionStrings__Default = "<connection string from step 1>"
$env:Seed__AdminEmail = "<trainer's email>"
$env:Seed__AdminPassword = "<strong temporary password>"
$env:Seed__AdminFirstName = "<first name>"
$env:Seed__AdminLastName = "<last name>"
dotnet run --project src/Mya.Api --no-launch-profile -- --seed-admin
Remove-Item Env:Seed__AdminPassword, Env:ConnectionStrings__Default
```

Safe to run twice (idempotent). The Admin changes the password from the profile page after the
first login.

## 6. Deploy — GitHub Actions

1. App Service → **Overview → Download publish profile**.
2. GitHub → *Settings → Environments* → create `production` (optionally require your approval).
   Add secret `AZURE_WEBAPP_PUBLISH_PROFILE` (the file's contents) and variable
   `AZURE_WEBAPP_NAME` = `app-mya-prod`. The old `AZURE_STATIC_WEB_APPS_API_TOKEN` is unused.
3. Merge to `main` (or run the `deploy` workflow manually). It lints and builds Angular, tests and
   publishes the API, copies the SPA into `wwwroot` and deploys one package.

If the app does not start, open **Log stream**. On Linux, if the runtime cannot find the entry
point, set **Configuration → Startup command** to `dotnet Mya.Api.dll`.

## 7. Verify end to end

1. Open the site on a phone, log in as the Admin, change the password.
2. Invite a test client with an address you can read. Within a few seconds Log stream shows
   `Outbox: sent PasswordInvitation`; the email arrives (check spam); the link opens the site;
   setting the password activates the account; reusing the link fails.
3. Register a second client from the public form; the Admin receives the notification; approve it;
   the client receives the approval email.
4. Leave everything idle for an hour, then open the site: expect a slow first request (app wake +
   database resume), then normal speed. Measure it — this is ADR-012's trigger.
5. Run `docs/07-manual-test-checklist.md` against the real URL.

## Living within the free limits

- **Database compute** is billed while the database is awake (minimum 0.5 vCore), not per query.
  100,000 vCore-seconds is about 55 awake hours a month at the minimum size. Normal use by one
  trainer and tens of clients fits; a pinger, an open SSMS session or a polling job does not.
  Close query tools when finished.
- **App CPU**: 60 minutes/day. When exceeded the app is stopped until the next day. Watch
  *CPU Time* in the App Service metrics after launch.
- **Emails** are queued in the database and sent within seconds while the app is awake. A failed
  send is retried on backoff only while the app is running; anything left over is sent by the
  startup sweep the next time someone opens the site.
- **Upgrade path** (no code changes): App Service plan → B1 enables Always On and custom domains;
  the database's free-limit behaviour → continue with charges; Brevo → a paid plan or an
  authenticated domain. See ADR-016 for the triggers.
