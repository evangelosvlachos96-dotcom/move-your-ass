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
| Keep-alive and alerting | **UptimeRobot** free, 5-minute HTTP check on `/health` | dropped once on Render Starter |
| Video provider (phase 5) | Bunny Stream, private library, protected playback | — |

The owner has created the Neon project in Frankfurt with dev and production branches (screenshot verified), and reports the domain and Cloudflare setup. App connectivity, DNS records, Render deployment and monitoring are not yet verified. See docs/08 for current evidence.

## Settings the owner enters

Names only. Values go into the Render dashboard or local user-secrets, never into this repo.

| Setting | Where | Secret |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Render | no |
| `ConnectionStrings__Default` | Render (Neon production **pooled** string) | **yes** |
| `Jwt__SigningKey` | Render | **yes** |
| `App__PublicOrigin` | Render | no |
| `Email__Mode`, `Email__From` | Render | no |
| `Email__ApiKey` | Render | **yes** |
| `AUTOMAPPER_LICENSE_KEY` | Render | **yes** |
| `ConnectionStrings:Default`, `Jwt:SigningKey`, `Seed:Admin*` | local user-secrets | **yes** |

`Seed:*` is never set on Render. The first Admin is created by the one-shot `--seed-admin` command
from the owner's machine, so that password never lives in a dashboard.

## Before the first production deploy

- [ ] Cloudflare: apex CNAME to Render **DNS-only**, `www` CNAME **proxied**, no `AAAA` records
- [ ] Cloudflare: redirect rule sending `www` to the apex with a 301
- [ ] Neon: project in Frankfurt, branches `production` and `dev`, both connection strings noted
- [ ] Resend: domain verified, records left unproxied, API key created
- [ ] Render: Docker service, Frankfurt, free, health check path `/health`, env vars set
- [ ] Render: custom domain `moveyourass.gr` added and its certificate issued
- [ ] Migrations applied to the `production` branch using the **direct** string (docs/10 §5)
- [ ] Admin seeded with `--seed-admin` against the **direct** string (docs/10 §6)
- [ ] UptimeRobot monitor on `/health` every 5 minutes, alerting to an address the owner reads
- [ ] Invitation and approval email verified in a real inbox, including spam placement
- [ ] Trainer completes onboarding and logs in from her phone on the real URL
- [ ] Phase 5: protected video playback and provider webhook validation

## Recurring checks

Weekly, because neither free tier warns before it stops:

- [ ] Neon usage page: compute hours against the monthly allowance
- [ ] Render metrics: instance hours, and no unexpected spin-downs
