# Production readiness — feature/prod-readiness

Started 2026-09-25 after owner approval. The owner continues to handle git add, commit and push.

## Verified

- Repository: C:\Users\evang\move-your-ass. Branch: feature/prod-readiness.
- Production Angular build: npm run build passed in the actual repository on 2026-09-25.
  Initial JavaScript/CSS total: 554.96 kB; estimated transfer: 128.72 kB. No budget warning/error.
  Previous cache/memory failures in the separate working copy did not reproduce.
- Existing local user-secrets contain database, JWT signing and seeded admin configuration.
  Secret values were not printed or added to source control.
- Configured database: (localdb)\MSSQLLocalDB, database Mya, Windows authentication.
- Read-only migration inspection found InitialCreate and Phase2UserNamesAndPasswordFlag applied.
  AccountInvitations is pending; the PasswordInvitation table is absent.
- No SMTP provider settings are currently configured in local user-secrets.

## Next actions

1. Confirm whether this step targets existing local Mya or an existing Azure SQL database.
2. Review/apply AccountInvitations to the confirmed target and verify migration history.
3. Select or identify the email provider and verified sender address/domain.
4. Set SMTP credentials through local user-secrets or deployment configuration, never chat or git.
5. Verify delivery and invitation completion using an agreed test recipient.
6. Complete remaining browser regression checks, then provision/verify deployment.

The production SPA currently uses /api. Hosting must route it to the API on the same origin,
or the deployment configuration must explicitly supply the actual API origin. Cloud resources,
domain, proxy/cookie behavior and production seeding still need deployment verification.

## Current boundaries

No changes were made to the Mya database during this inspection. No real email was sent.
No SMTP credentials were set and no Azure resources were provisioned. Video implementation
remains a later milestone after deployment verification.
