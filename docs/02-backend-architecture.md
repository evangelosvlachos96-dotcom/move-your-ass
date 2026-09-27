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

## Indexes, and why these and no others

Reviewed against the real queries on PostgreSQL 17, with `EXPLAIN (ANALYZE)` on a seeded
database rather than by reading the code (2026-09-27).

| Query | Index | Verdict |
|---|---|---|
| Refresh: `WHERE TokenHash = @hash`, on every refresh | `IX_RefreshToken_TokenHash` (unique) | **Was missing. Added.** Seq Scan, 7.2 ms, 19,999 rows discarded at 20k rows → Index Scan, 0.17 ms |
| Outbox claim: `ProcessedAtUtc IS NULL … ORDER BY CreatedAtUtc` | `IX_Outbox_Pending`, partial on `ProcessedAtUtc IS NULL` | Already correct — Index Scan, 0.08 ms with 20k rows and only 50 pending |
| Video library: `IsPublished`, `Status`, ordered by `SortOrder` | `(IsPublished, Status, SortOrder)` | Matches filter and order |
| Tag filter: `VideoTags.Any(TagId IN …)` | `IX` on `VideoTag.TagId` | Matches |
| Admin user list: `WHERE Status = @s` | `IX_User_Status` | Matches |
| Creation idempotency, object key, tag name, invitation token | unique indexes on each | Matches, and each is a correctness constraint too |

**Deliberately not added:**

- **Trigram index for the admin user search.** The search is
  `ILIKE '%term%'` across email, first and last name. A leading wildcard cannot use a btree
  index, so it would need `pg_trgm` and a GIN index per column. This product has one trainer and
  tens to low hundreds of clients: a sequential scan over a few hundred rows is microseconds, and
  three GIN indexes would cost more to maintain on every write than they could ever save.
  **Revisit above roughly 50,000 users**, which this will never have.
- **`OutboxMessage.SubjectUserId`.** Only read when an account is hard-deleted, which is rare and
  manual. A scan of the outbox at that moment is not worth an index on every insert.
- **Anything for `ORDER BY CreatedAtUtc DESC` on the user list.** Same reasoning: the table is
  tiny and it is already sorting a filtered handful of rows.

The principle: an index is a write cost paid on every insert and update, forever, to buy read
speed on a query that is actually slow. Two of the queries above were already fast, and one was
not.

## Video

Video, Tag and VideoTag, `IVideoStorage`, presigned multipart upload and presigned playback are
**implemented** on Backblaze B2 (ADR-019). There are no webhooks: object storage emits no events,
so completion is verified by a HEAD instead.

Creation idempotency uses a unique `(CreatedByUserId, CreationKey)` reservation on the `Video`
row. **`IdempotencyRecord` is therefore unused** — it predates that design and no handler touches
it. It stays because dropping a table inverts the deploy order (the new code has to be live
before the migration runs) and an unused table costs nothing; see `docs/backlog.md`.
