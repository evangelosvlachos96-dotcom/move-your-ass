# 03 — Authentication, authorization and invitations

## Account states

| State | Entry | Allowed next states |
|---|---|---|
| PendingApproval | Self-registration | Active by approval, Declined by rejection |
| Invited | Admin creates user | Active only after password setup |
| Active | Approval or completed invitation | Suspended |
| Suspended | Admin suspends active user | Active by reactivation |
| Declined | Admin declines registration | PendingApproval by re-registration, Active by admin reactivation |

Admin may edit names and roles or delete an account. Self-deletion, self-suspension and removing
the last admin are prohibited by handlers. Suspension is limited to active accounts so an invitation
cannot be activated through suspend/reactivate. Approve/decline apply only to PendingApproval.

## Registration and invitations

Self-registration accepts names, email and password, queues an admin email and returns 202.
Approval queues a client email. Pending users cannot log in.

Admin creation generates an undisclosed random password, creates Invited status and queues a
password invitation in one transaction. No password or setup token is returned to the admin.
The email links to /set-password#token=...; the fragment avoids sending the token in page URLs
to the server or in referrers. The form submits the credential and new password to the API.

Tokens have 256 random bits, a SHA-256 hash in PasswordInvitation and a configurable expiry
(Platform:InvitationHours, default 24). A conditional database update consumes a valid token once;
password change and activation commit in the same transaction. An expired, used, deleted or
otherwise invalid invitation cannot activate an account. Resending invalidates earlier unused links.
Opening the page alone does not consume the link; email scanners cannot activate the account.

The outbox needs the delivery credential until email is dispatched; restrict access to that table
and development logs. Delivered outbox payloads are cleared. The client logs in after setup;
invitation acceptance does not issue a session automatically.

## Login and sessions

Login is password-only; email approval/invitations are not two-factor authentication.
TwoFactorTicket is unused legacy schema retained to avoid a destructive cleanup migration.
Identity validates passwords and lockout. Access tokens last 15 minutes by default and stay in
memory in Angular. Refresh tokens rotate, are hashed in the database and travel in an HttpOnly,
Secure cookie. A new login replaces the active session; earlier refresh attempts fail.

Revocation is lazy: existing access tokens can survive until expiry. Strict per-request account
checks remain backlog work. The UI's role guard is only navigation; API policies enforce admin access.

## Existing forced password change

The older admin reset-password endpoint remains available for active accounts and returns a
temporary password. It revokes refresh tokens and requires a change on next login. It is separate
from admin-created invitations and is not exposed in the new user list.

MustChangePasswordMiddleware blocks authenticated endpoints except me, change-password and logout
while the claim is present. A forced change can omit the current password only after the handler
checks the database flag. Refresh after changing the password to replace the old claim.

## Future concurrency

IdempotencyRecord is retained for video creation. Booking concurrency is outside product scope.
