# infra

Deployment scripts for the Azure environment. Nothing here is committed with secrets — all
parameter files ending in `.parameters.local.json` are gitignored.

## Resources

Production runs on free tiers (ADR-016); the step-by-step runbook is
`docs/10-free-tier-production.md`.

| Resource | Production (launch) | Upgrade when a trigger in ADR-016 fires |
|---|---|---|
| Resource group | `rg-mya-prod` | — |
| App Service plan | **F1 Linux** (free) | B1 (Always On, custom domain) |
| App Service (API + Angular in wwwroot) | `app-mya-prod` | — |
| SQL server / database | `sql-mya-prod` / `sqldb-mya-prod`, **free offer**, auto-pause at limit | continue with charges |
| Email | Brevo free SMTP (300/day) | authenticated domain / paid plan |
| Static Web App | **not used** — the API serves the SPA (same origin) | — |
| Key Vault | not used at launch; secrets in App Service settings | when a second environment exists |
| Video provider (phase 5) | Private Bunny Stream library with protected playback | — |

## Before the first production deploy

- [ ] Budget alert configured in Cost Management (Azure has no hard spend cap)
- [ ] SQL free offer applied, *auto-pause until next month* selected, free-amount alert set
- [ ] SQL firewall: operator IP + App Service outbound IPs only
- [ ] App settings from docs/10 §2 set; HTTPS Only on; no health check, no pingers
- [ ] Brevo sender verified; invitation/approval email verified in a real inbox
- [ ] Migrations applied (docs/10 §4); Admin seeded with `--seed-admin` (docs/10 §5)
- [ ] `production` environment, publish-profile secret and `AZURE_WEBAPP_NAME` variable in GitHub
- [ ] Trainer completes onboarding and logs in from her phone on the real URL
- [ ] Phase 5: protected video playback and provider webhook validation

## TODO

Bicep templates. Until then, provision via `az` CLI and record the commands here so the
environment is reproducible.
