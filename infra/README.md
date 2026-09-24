# infra

Deployment scripts for the Azure environment. Nothing here is committed with secrets — all
parameter files ending in `.parameters.local.json` are gitignored.

## Resources

| Resource | Dev | Production |
|---|---|---|
| Resource group | `rg-mya-dev` | `rg-mya-prod` |
| App Service plan | F1 | **B1 minimum** (S1 for a staging slot) |
| App Service | `app-mya-api-dev` | `app-mya-api-prod` |
| SQL server | `sql-mya-dev` | `sql-mya-prod` |
| SQL database | free offer | serverless GP, min-vCore floor set, PITR on |
| Static Web App | Free | Free (sufficient — includes SSL + custom domain) |
| Video provider (phase 5) | Bunny Stream test library | Private Bunny Stream library with protected playback |
| Key Vault | `kv-mya-dev` | `kv-mya-prod` |
| Application Insights | `appi-mya-dev` | `appi-mya-prod` |

## Before the first production deploy

- [ ] Budget alert configured in Cost Management (Azure has no hard spend cap)
- [ ] `AUTOMAPPER_LICENSE_KEY` set in App Service configuration
- [ ] JWT signing key in Key Vault, referenced via managed identity
- [ ] CORS set to the exact SWA origin — no wildcards
- [ ] SQL firewall/network access restricted to required application and operator access
- [ ] SMTP configured and invitation/approval email verified in a real inbox
- [ ] Reviewed migrations applied, admin seeded, frontend origin verified
- [ ] Phase 5: protected video playback and provider webhook validation
- [ ] Alerts on App Service failure rate and SQL DTU/vCore
- [ ] Custom domain + managed certificate

## TODO

Bicep templates. Until then, provision via `az` CLI and record the commands here so the
environment is reproducible.
