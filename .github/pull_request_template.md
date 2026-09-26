## What

<!-- One or two sentences. Link the phase or checkpoint from docs/04-roadmap.md if applicable. -->

## Why

<!-- What problem this solves. If it contradicts an ADR in docs/05, say so and amend the ADR. -->

## Checklist

- [ ] Stays within the current phase scope (`docs/04-roadmap.md`) — anything else went to `docs/backlog.md`
- [ ] No business logic in controllers
- [ ] No `HttpClient` imported outside `web/src/app/core/http`
- [ ] All datetimes UTC at the boundary
- [ ] `dotnet test` and `npm run lint` green (ADR-014 defers new automated tests)
- [ ] No secrets, keys, or connection strings in the diff
- [ ] Docs updated if a decision changed, and `docs/08-milestone-handover.md` "Current state" is accurate
