# Security policy

`moveyourass.gr` is a private workout-video library for one trainer and their approved clients.
The source is public; the data is not. Nothing in this repository is intended to be reused as a
library, and there is no supported release — the only deployment is the one at
`https://moveyourass.gr`.

## Reporting a vulnerability

Please report privately, not in a public issue.

1. **Preferred:** GitHub → this repository → **Security** → **Report a vulnerability**
   (private vulnerability reporting). It creates a private advisory only the maintainer can see.
2. If that is unavailable, open an issue titled `Security contact request` containing no
   details, and the maintainer will follow up privately.

Please include what you did, what happened, and why you believe it is a problem. A proof of
concept helps; testing against real client accounts does not — use a local instance
(`docs/01-getting-started.md`) rather than the production site, and never access, modify or
retain another person's data.

Expect an acknowledgement within a few days. This is one person's side project, not a funded
programme: there is no bounty, and fixes land when they land. Credit in the release notes is
offered to anyone who wants it.

## What is in scope

Anything reachable from the running application: authentication and session handling, the
approval workflow, authorisation between the Admin and Client roles, the video upload and
playback paths, the presigned-URL scheme, the contact form, and the deployment configuration in
this repository.

## What is not

- Findings from automated scanners with no demonstrated impact.
- Missing hardening headers with no exploitable consequence.
- Denial of service, volumetric testing, or anything that degrades the live site. **Do not run
  load or fuzzing tools against production.** It is a single free-tier instance shared by real
  users.
- Social engineering of the maintainer or the trainer.
- Reports about third-party services (Render, Neon, Backblaze, Resend, Cloudflare). Report those
  to the provider.

## What the repository deliberately contains, and does not

**No secrets, ever** — not in code, not in configuration files, not in documentation, not in
tests. Local development uses .NET user-secrets; production uses environment variables in the
Render dashboard. Documentation names a setting and says where it lives; it never carries a
value. This is enforced three ways:

- **GitHub push protection** rejects a push containing a recognised credential.
- **`gitleaks`** runs on every push and pull request, and sweeps the entire history weekly
  (`.github/workflows/security.yml`).
- Every third-party GitHub Action is **pinned to a commit SHA**, so a compromised tag cannot
  change what runs here.

Workflows are read-only (`permissions: contents: read`) and no workflow uses
`pull_request_target`, so a pull request from a fork cannot reach a secret or write to the
repository. The weekly database backup is encrypted with GPG **on the runner, before the file
exists**, because artifacts on a public repository are downloadable by anyone.

## Known and accepted

- **A playback URL works until it expires.** Video is served by presigned GET URLs with a short
  lifetime (ADR-019). Anyone holding a live URL can fetch that video, whether or not they are
  signed in. This is the trade-off for serving video off plain object storage with no transcoder
  and no DRM, and it is accepted for this product.
- **Two end-to-end test refresh cookies are in the git history.** `web/e2e/.auth/*.json` were
  committed early and removed later; the file is gone from the tree but remains in history, as
  everything on a public repository does. They belong to two local-only test accounts
  (`@localhost.test`) on a development database, never production. The remedy is deleting those
  two accounts, which kills the tokens; see the owner checklist in `docs/08-milestone-handover.md`.
  The directory is now in `.gitignore`.

## Licence

This code is published to be read, not reused: **all rights reserved**. There is no `LICENSE`
file, and the absence is deliberate — see the README.
