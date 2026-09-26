# 02 — Backend architecture

## Layers

- Domain contains entities/enums and references only the BCL.
- Application references Domain and EF Core abstractions, never the database provider or ASP.NET.
- Infrastructure implements Identity, PostgreSQL persistence and email delivery.
- API composes dependencies and maps HTTP requests to handlers.

Architecture tests enforce these dependencies. Handlers are injected directly; there is no mediator.
Controllers bind input, call one handler, and translate Result into a response.

## Conventions

- Feature folders group commands, validators and handlers.
- FluentValidation checks input shape; handlers enforce state transitions and authorization rules.
- Result/Result<T> carry stable error codes; API failures use ProblemDetails.
- Angular features call ApiClient through API services, never HttpClient directly.
- Users are accessed through IUserService. IAppDbContext exposes other sets and transactions.
- Identity and the application share one scoped DbContext, so account changes and email outbox
  entries commit in one transaction.
- SQL unique constraints settle duplicate identities. Never rely on a pre-check as the guarantee.
- EF configurations live in Infrastructure. UTC datetimes use the UTC converter and map to
  `timestamp with time zone`. The converter is load-bearing: Npgsql refuses to write a DateTime
  whose Kind is not Utc to that column type.
- Preserve historical migrations; schema changes require a new migration.

## Accounts and email

See docs/03-auth-and-concurrency.md for account states, sessions and single-use invitations.
EmailOutbox stores typed payloads. EmailTemplates renders them. The dispatcher leases rows and
retries delivery before dead-lettering; it provides at-least-once delivery, not exactly-once
sending. Each send carries the outbox message Id as the provider's idempotency key, so a retry
after a crash cannot produce a second copy. The Resend sender logs metadata only, never the body,
the API key or a setup token. Console delivery is limited to Development.

## Testing policy

Existing architecture, settings, seeder and UTC converter tests remain. ADR-014 defers additional
automated suites; browser/API acceptance checks are maintained in docs/07-manual-test-checklist.md.
Build, lint and the existing tests must pass before handover. Do not claim a checklist was run
unless it was actually exercised.

## Future video work

Video, Tag and VideoTag, IVideoStorage, provider upload/playback, signed webhooks and video
idempotency are planned, not implemented. IdempotencyRecord is retained for that milestone.
