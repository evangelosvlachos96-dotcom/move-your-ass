# 01 — System design

Move Your Ass serves one trainer (Admin) and approved clients. Its product is a private workout
video library. There is no booking or calendar feature.

## Containers

Angular SPA → ASP.NET Core JSON API → PostgreSQL (Neon).
The transactional outbox dispatches email through the Resend HTTPS API, or to the local console.
Future video uploads and playback go directly between the browser and Bunny Stream; the API
authorizes access and issues short-lived provider credentials. Video bytes never cross the API.

## Environments

Local: Angular :4200, API launch profile :5077, the Neon `dev` branch, user-secrets.
Deployment target: one Render web service (Docker, Frankfurt) serving the API and the Angular
build from wwwroot on https://moveyourass.gr, the Neon `production` branch, and the Resend API,
all on free tiers at launch (ADR-017, docs/10-production.md). Cloudflare holds DNS.
These are targets, not evidence of provisioned resources. See infra/README.md for deployment work.

Same origin is a design constraint, not a convenience: the refresh cookie is SameSite=Strict, so
the SPA and the API must share a host. Production therefore needs no CORS at all.

The production origin is explicit in App:PublicOrigin and is also used to construct email links.
Use HTTPS in production. JWT keys, database credentials and the Resend API key stay outside
source control, in user-secrets locally and in the Render dashboard in production.

## Video decision

Bunny Stream is planned for upload, managed transcoding and protected playback, behind IVideoStorage.
The trainer can upload from a phone without manual encoding. See docs/06-video-catalogue.md for
the taxonomy, filters, statuses and API design. Verify provider contracts during implementation.

## Operations

Use structured request and outbox delivery logs. Resend mode must not log the API key, setup
tokens or email bodies. Development console email deliberately prints the message for local
verification. Outbox delivery is at least once: provider acceptance followed by a process crash
may retry a send, which is why each send carries the outbox message Id as Resend's idempotency
key. Successfully dispatched outbox payloads are cleared to stop retaining delivered invitation
links.

A keep-alive monitor calls /health every five minutes so the free Render instance does not spin
down. /health must never touch the database: that is what lets Neon's compute scale to zero while
the web service stays awake (ADR-017).

GitHub workflows build and test. Render deploys through its own Git integration rather than a
workflow. Production migrations are reviewed and applied explicitly; startup does not apply them.
