# Commit checkpoint — account onboarding and administration

Date: 2026-09-24. Branch: feat/web-admin.

This is a reviewable implementation checkpoint. Stop here for the owner to run git add,
git commit and git push. Do not begin the next milestone until the owner gives the green light.

## Implemented

- Removed unused booking configuration; aligned product scope and phase documentation around videos.
- Preserved existing registration, approval and forced-password-change work.
- Added Invited account state, expiring single-use password setup links and admin resend.
- Added admin dashboard, pending count, searchable/paged user list, invitation/edit dialogs,
  approval/decline and access management with named confirmations and typed-email deletion.
- Added configurable TLS SMTP delivery alongside Development console email.
- Added AccountInvitations migration; existing migration history is unchanged.
- Fixed malformed Angular control-flow syntax in the existing password-change template.

## Verification performed in the working copy

- .NET solution build: passed, zero warnings/errors.
- Existing tests: 15 passed (4 settings, 8 persistence, 3 architecture).
- Angular development build: passed.
- Angular lint: passed.
- Production build: not verified. The first attempt failed in Angular's LMDB cache with
  "Not enough space" despite approximately 312 GB of free disk space. A cache-disabled retry
  failed with a native memory-allocation error. No production-build success is claimed.
- Applied the new migration successfully to a uniquely named, isolated SQL Server LocalDB database.
- 35 live API/database checks passed using synthetic accounts and console email. These covered
  registration/approval/login, admin/client separation, editing/suspension/reactivation,
  self-deletion and last-admin protections, invitation creation and activation, expired/unknown/
  malformed links, resending, rate limiting and clearing delivered outbox payloads.
- Concurrent acceptance of one invitation: exactly one 204 response and one rejected request.
- Browser: admin login; dashboard pending count; pending-client list; approval confirmation and
  successful approval; pending row and badge removed after approval; invitation dialog and submission;
  status filter navigation; invalid-link message. The user list was also inspected visually.

## Remaining after this commit

- Retry the production build in a fresh environment/CI and resolve any reported issues before deployment.

- Apply AccountInvitations to the owner's intended local database before running this version there.
  Only the isolated verification database was migrated; existing application data was not changed.
- Configure SMTP using docs/09-email-setup.md and verify a real message reaches an inbox.
  No live SMTP provider was configured and no real email was sent.
- Complete the remaining browser regression matrix in docs/07-manual-test-checklist.md, especially
  password setup submission in the browser, the full mobile/desktop viewport matrix, and all dialogs.
  API verification does not replace every browser interaction.
- Provision and verify deployment before video implementation. Video upload/library/player remain
  designed but unimplemented. Booking is outside scope.

## Suggested commit

feat(onboarding): add admin user management and email invitations

- add pending approval dashboard, user administration and invitation setup flow
- support expiring single-use setup links, resend and TLS SMTP with development logs
- remove obsolete booking configuration and align roadmap and handover documentation

No git add, commit or push has been performed by the agent. Wait for the owner's green light.
