# 12 — Trainer guide and handover

The interface is Greek. Complete production and video acceptance in docs/10 and docs/11 first.
The development screenshots and synthetic records are not the trainer's real content.

## Clients

The dashboard shows pending registrations. Open Έλεγχος εγγραφών, review the person, then approve
or decline. Approval lets a registered client sign in; their notification is queued by email.

To invite someone directly, open Όλοι οι χρήστες and invite them with their email and names.
They receive a single-use password setup link and choose their own password. There is no password
to send manually. If the link expires, resend from the invited user's actions; older links stop
working. Verify the address before resending. Use suspend/reactivate for temporary access changes.

## Upload a workout

1. Open Διαχείριση βίντεο → Νέο βίντεο. If disabled, ask the operator to finish video setup.
2. Enter a descriptive title and instructions. Select audience, body area and whether equipment
   is required. Όλοι means suitable for both audience filters.
3. Pick existing tags or add a reusable one, such as αλτήρες. Avoid near-duplicate labels.
4. Select the video file and start upload. Keep this page open. Large files take time; the progress
   bar tracks bytes sent. Pause and continue if necessary. Navigating away interrupts the upload.
5. Wait for processing; press Έλεγχος to refresh its status. Uploaded does not yet mean playable.
6. When ready, preview the workout and check sound, orientation, title and tags. Publish only then.

A new video is private. Clients never see drafts or processing failures. Metadata edits save
without replacing the file. A failed/interrupted draft offers Νέο ανέβασμα αρχείου: select the
source file again. If the browser was closed this starts a new upload attempt, not guaranteed
cross-browser continuation. Ask the operator for help if the draft cannot obtain credentials.

## Manage the library

Use search, edit metadata, move a workout up/down within the visible page, or withdraw publication.
Withdrawal hides it and stops new playback links. A link already issued may work briefly afterward.
Delete is permanent and removes the hosted file; keep your own original recording first. Unused
tags can be removed under Διαχείριση ετικετών; used tags must first be removed from their workouts.

## Client experience

Approved clients sign in and see the published library. They can combine audience, body area,
equipment, tags and search. Tags match any selected label; other dimensions narrow the result.
Open a card to watch. Empty results can mean filters are too narrow; Καθαρισμός resets them.
If a video is unavailable, retry or contact the trainer. No booking or payment flow is included.

## First-day handover

- Trainer can sign in from her phone at the real domain with her own admin account.
- Invite one consenting test client and verify email receipt, setup link and sign-in on another device.
- Upload, preview and publish a short real workout; confirm a client can play it.
- Load the initial content, verify categories/tags, and remove disposable test content deliberately.
- Record who owns Render, Neon, Resend, Cloudflare and Bunny access and who handles recovery.
- Retain source videos and confirm database backup/restore procedures and billing monitoring.
- Keep technical credentials with the operator. Do not send passwords or keys in messages.

Branded HTML/MJML email redesign is recorded in docs/backlog.md as requested; it is not included
in this milestone. Self-service forgotten-password recovery and login 2FA remain deferred scope.
