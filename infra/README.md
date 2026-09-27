# infra

Deployment notes for the production environment. Nothing here is committed with secrets. The
step-by-step runbook is `docs/10-production.md`; the decision behind it is ADR-017 in
`docs/05-decisions.md`.

There is no infrastructure-as-code. All four providers are configured through their dashboards,
and Render deploys from its own Git integration rather than from a workflow. That is a deliberate
trade for a one-service environment: a Bicep or Terraform equivalent would be more moving parts
than the thing it describes. The runbook is the reproducibility mechanism, so keep it accurate.

## Resources

| Resource | Production (launch) | Upgrade when an ADR-017 trigger fires |
|---|---|---|
| Web service (API + Angular in wwwroot) | **Render free**, Docker, Frankfurt | Render Starter (always on, no pinger) |
| Database | **Neon PostgreSQL** free, `aws-eu-central-1`, branch `production` | paid Neon plan |
| Local database | Neon branch `dev` | — |
| Email | **Resend** free, sender `noreply@moveyourass.gr` | paid Resend plan |
| DNS | **Cloudflare**, `moveyourass.gr` | — |
| Keep-alive and alerting | **cron-job.org** free, `/health` every 10 min, 06:00–23:00 Europe/Athens | dropped once on Render Starter |
| Video storage | **Backblaze B2** free, private bucket, S3-compatible API (ADR-019) | paid B2, or Cloudflare R2 for zero egress |

**The service is live at `https://moveyourass.gr`**: Render, both domains with certificates,
Cloudflare records, production migrations and the first Admin are all in place. The keep-alive,
Backblaze setup and real-inbox email verification are outstanding. See docs/08 §"Current state"
for exactly what is verified and what is not.

## Settings the owner enters

Names only. Values go into the Render dashboard or local user-secrets, never into this repo.

| Setting | Where | Secret |
|---|---|---|
| `ConnectionStrings__Default` | Render (Neon production **pooled**, Npgsql `key=value`, not a URL) | **yes** |
| `Jwt__SigningKey` | Render | **yes** |
| `App__PublicOrigin` | Render | no |
| `Email__Mode`, `Email__From` | Render | no |
| `Email__ApiKey` | Render | **yes** |
| `AUTOMAPPER_LICENSE_KEY` | Render | **yes** |
| `Video__Provider`, `Video__S3__Enabled`, `Video__S3__ServiceUrl`, `Video__S3__Region`, `Video__S3__BucketName` | Render | no |
| `Video__S3__AccessKeyId`, `Video__S3__SecretAccessKey` | Render | **yes** |
| `ConnectionStrings:Default`, `Jwt:SigningKey`, `Seed:Admin*`, `Video:S3:*` | local user-secrets | **yes** |

**`ASPNETCORE_ENVIRONMENT` is deliberately not in this table.** The Dockerfile sets it; a value
in the Render dashboard overrides it and can stop `appsettings.Production.json` from loading.

`Seed:*` is never set on Render. The first Admin is created by the one-shot `--seed-admin` command
from the owner's machine, so that password never lives in a dashboard.

## Before the first production deploy

- [x] Cloudflare: apex and `www` CNAMEs to Render, both **DNS-only**, no `AAAA` records
- [x] Render performs the `www` redirect, so no Cloudflare redirect rule exists
- [x] Neon: project in Frankfurt, branches `production` and `dev`, both connection strings noted
- [x] Resend: domain verified, records left unproxied, API key created
- [x] Render: Docker service, Frankfurt, free, health check path `/health`, env vars set
- [x] Render: custom domains `moveyourass.gr` and `www.moveyourass.gr`, certificates issued
- [x] Migrations applied to the `production` branch using the **direct** string (docs/10 §5)
- [x] Admin seeded with `--seed-admin` against the **direct** string (docs/10 §6)
- [ ] **Rotate the Resend API key** — one was exposed in a terminal transcript on 2026-09-26
- [ ] cron-job.org job on `/health` every 10 min, 06:00–23:00 Europe/Athens, failure alerts on
- [ ] Backblaze: private bucket, bucket-scoped key, CORS rule (docs/11 §1–3)
- [ ] `Video__S3__*` set on Render and the `VideoObjectStorage` migration applied
- [ ] Invitation and approval email verified in a real inbox, including spam placement
- [ ] Trainer completes onboarding and logs in from her phone on the real URL
- [ ] Live video acceptance: upload, playback, delete on a real device (docs/11 §6)

## Recurring checks

Weekly, because neither free tier warns before it stops:

- [ ] Neon usage page: compute hours against the monthly allowance
- [ ] Render metrics: instance hours, and no unexpected spin-downs inside 06:00–23:00
- [ ] Backblaze reports: stored bytes against the 10 GB free tier, **and downloaded bytes against
      the 3× free egress** — egress is the one that runs out first
