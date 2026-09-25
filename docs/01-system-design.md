# 01 — System design

Move Your Ass serves one trainer (Admin) and approved clients. Its product is a private workout
video library. There is no booking or calendar feature.

## Containers

Angular SPA → ASP.NET Core JSON API → SQL Server / Azure SQL.
The transactional outbox dispatches email to the configured SMTP provider, or local console.
Future video uploads and playback go directly between the browser and Bunny Stream; the API
authorizes access and issues short-lived provider credentials. Video bytes never cross the API.

## Environments

Local: Angular :4200, API launch profile :5077, SQL Server/LocalDB, user-secrets.
Deployment target: one App Service serving the API and the Angular build (same origin), Azure SQL
and Brevo SMTP, all on free tiers at launch (ADR-016, docs/10-free-tier-production.md).
These are targets, not evidence of provisioned resources. See infra/README.md for deployment work.

The production origin is explicit in Cors:AllowedOrigin and is also used to construct email links.
Use HTTPS in production. JWT keys, database credentials and SMTP credentials stay outside source control.

## Video decision

Bunny Stream is planned for upload, managed transcoding and protected playback, behind IVideoStorage.
The trainer can upload from a phone without manual encoding. See docs/06-video-catalogue.md for
the taxonomy, filters, statuses and API design. Verify provider contracts during implementation.

## Operations

Use structured request and outbox delivery logs. SMTP mode must not log passwords, tokens or
email bodies. Development console email deliberately prints the message for local verification.
Outbox delivery is at least once: SMTP acceptance followed by a process crash may resend an email.
Successfully dispatched outbox payloads are cleared to stop retaining delivered invitation links.

Existing GitHub workflows build/test and contain Azure deployment jobs. Production migrations
are reviewed and applied explicitly; startup does not apply them.
