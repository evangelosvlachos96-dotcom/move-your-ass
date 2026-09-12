## What

<!-- One or two sentences. Link the step from docs/04-phase-1-scope.md §7 if applicable. -->

## Why

<!-- What problem this solves. If it contradicts an ADR in docs/05, say so and amend the ADR. -->

## Checklist

- [ ] Stays within the current phase scope (`docs/04` §1) — anything else went to `docs/backlog.md`
- [ ] No business logic in controllers
- [ ] No `HttpClient` imported outside `web/src/app/core/http`
- [ ] All datetimes UTC at the boundary
- [ ] Tests added or updated; `dotnet test` and `npm run test` green
- [ ] No secrets, keys, or connection strings in the diff
- [ ] Docs updated if a decision changed
