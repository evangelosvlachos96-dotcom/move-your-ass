# Email setup

Two delivery modes: `Console` for Development, `Resend` for production. There is no SMTP mode.
Render blocks outbound ports 25, 465 and 587 on free web services, so an HTTPS provider API is
the only option (ADR-017); MailKit and the SMTP sender were removed in phase 4.

## Local log delivery

`Email:Mode=Console` is the default. It is allowed only in Development and logs the rendered email
so the invitation link can be opened locally. Never publish these logs; they contain setup
credentials.

## Real delivery — the Resend API

Set these keys with `dotnet user-secrets` for `src/Mya.Api`, or as environment variables in the
Render dashboard. Names only; values are never written into the repo, into docs, or into chat.

| Key | Value | Secret |
|---|---|---|
| `Email:Mode` | `Resend` | no |
| `Email:ApiKey` | API key created in Resend, with send permission | **yes** |
| `Email:From` | verified sender, e.g. `Move Your Ass <noreply@moveyourass.gr>` | no |
| `App:PublicOrigin` | frontend origin used in email links | no |
| `Platform:InvitationHours` | 24 by default | no |

On Render the same keys use environment-variable spelling: `Email__Mode`, `Email__ApiKey`,
`Email__From`; use `App__PublicOrigin`.

Delivery is a single HTTPS POST to Resend's send endpoint with a bearer token. The sender logs
delivery metadata only: never the body, the API key or a setup token. Production rejects console
mode and refuses to start without an API key and a parseable `From` address.

## Idempotency

Each send carries the outbox message Id in Resend's `Idempotency-Key` header. Outbox delivery is
at-least-once by design: a crash between the provider accepting a message and the row being marked
processed will retry the send. With the key, Resend replays the original response instead of
sending a second copy.

Resend retains a key for 24 hours. The outbox backs off 1m, 5m, 30m then 2h and dead-letters after
five attempts, so the longest possible retry span is about two and a half hours, comfortably
inside that window.

## Sending domain

The domain is added and verified in Resend, which generates per-domain SPF and DKIM records to
publish in Cloudflare. **Leave those records DNS-only (grey cloud)** — Resend's documentation says
a proxied CNAME prevents verification from completing. Setup steps are in
`docs/10-production.md` §3.

Free plan allowance is 100 emails per day and 3,000 per month, far beyond one trainer and tens of
clients. Sending from a verified domain is what keeps messages out of spam; sending as a free
mailbox through a third-party relay fails that domain's DMARC alignment.

## Verification

Create a test client from the admin UI. The success notice means queued, not delivered. Watch
outbox processing — the dispatcher wakes on commit, there is no polling interval — then verify the
email arrives, its link opens the configured frontend, and password setup enables login. Reusing
the link must fail. Resending an invitation must invalidate the earlier link.

A provider failure leaves the account invited and retries through the outbox, then dead-letters
after five attempts with the reason in `LastError`. Resend the invitation after fixing configuration if the
message has expired or dead-lettered.

Resend send endpoint and idempotency: https://resend.com/docs/api-reference/emails/send-email and
https://resend.com/docs/dashboard/emails/idempotency-keys

## Branded templates

Every message is sent as **both** an HTML and a plain-text part. The text part is not optional:
some clients block HTML by default, and a message with no text alternative is likelier to be
filtered as spam.

`EmailContent` describes what a message says — heading, paragraphs, an optional call to action
and an optional note. `EmailLayout` renders both bodies from that one object, so the text version
cannot drift away from the HTML one as copy changes. Templates never build HTML by hand.

The layout is tables with inline CSS, because email clients strip `<style>` blocks and Outlook
ignores most modern layout. Colours mirror `web/src/styles/_brand.scss`. **The logo is a PNG**
(`web/public/email-logo.png`, served from `App:PublicOrigin`) — **Gmail refuses to render SVG**,
so the app's SVG marks cannot be used here. Its background matches the email background so it
does not show as a pale box.

Every user-controlled value — names, decline reasons, email addresses — goes through HTML
encoding. Tests cover this: `EmailTemplateTests` asserts that a name containing `<script>` is
encoded in the HTML body.

**To look at the emails**, set a directory and run the suite; each template is written out as
`.html` and `.txt`:

```powershell
$env:MYA_EMAIL_PREVIEW_DIR = "$env:TEMP\mya-email-preview"
dotnet test --filter "FullyQualifiedName~EmailTemplateTests"
```

Open the `.html` files in a browser. The logo will not load from a local file because it points
at the production origin; that is expected and does not affect delivered mail. Automated
assertions cannot tell you an email is ugly, which is the point of the preview.
