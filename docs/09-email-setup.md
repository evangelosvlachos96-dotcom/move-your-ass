# Email setup

## Local log delivery

Email:Mode=Console is the default. It is allowed only in Development and logs the rendered email
so the invitation link can be opened locally. Never publish these logs; they contain setup credentials.

## Real SMTP delivery

Set these keys using dotnet user-secrets for src/Mya.Api, or deployment configuration:

| Key | Value |
|---|---|
| Email:Mode | Smtp |
| Email:Host | Provider SMTP host |
| Email:Port | 587 for STARTTLS, or provider's TLS port |
| Email:Security | StartTls or SslOnConnect |
| Email:User | Provider username |
| Email:Password | SMTP password/API credential |
| Email:From | Verified sender address |
| Cors:AllowedOrigin | Exact frontend origin used in email links |
| Platform:InvitationHours | 24 by default |

No credentials belong in appsettings files or git. SMTP mode logs delivery metadata, retaining
application logs while avoiding disclosure of the email body. It does not send a second console copy.
The provider must allow the From identity. Production rejects console mode and incomplete SMTP settings.

## Verification

Create a test client from the admin UI. The success notice means queued, not delivered. Watch
outbox processing, then verify the email arrives, its link opens the configured frontend, and
password setup enables login. Reusing the link must fail. Resend must invalidate the earlier link.
An SMTP failure leaves the account invited and retries through the outbox, then dead-letters after
five attempts. Resend after fixing configuration if the message has expired or dead-lettered.

MailKit TLS/async SMTP API: https://mimekit.net/docs/html/T_MailKit_Net_Smtp_SmtpClient.htm
Dependency: MailKit 4.18.0. No SMTP provider account or credentials are provisioned by this change.
