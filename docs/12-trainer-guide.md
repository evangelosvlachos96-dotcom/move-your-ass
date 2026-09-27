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

## Before you record: two phone settings that matter

The site stores and plays **the exact file you upload**. Nothing converts it, so the recording
settings decide whether every client can watch it and how much space it uses.

**On iPhone — set the format to "Most Compatible".** Settings → Camera → Formats → **Most
Compatible**. The default, "High Efficiency", produces HEVC video that some browsers cannot play
and that the site will refuse. This setting only affects new recordings, so change it before
filming, not after.

**Record at 1080p/30, or 720p/30 if space is tight.** Settings → Camera → Record Video.

| Setting | Roughly per minute | Use it when |
|---|---|---|
| 720p HD at 30 fps | ~40 MB | Most workouts. Plenty sharp on a phone, and half the storage |
| 1080p HD at 30 fps | ~80 MB | Detail matters — small movements, close-up form |
| 4K, or 60 fps | 200-400 MB | **Avoid.** It fills the storage allowance and slows every client's playback for no visible benefit |

**On Android** the equivalent is the camera app's video resolution: choose FHD (1080p) or
HD (720p), not 4K or 60 fps. Android records MP4 already, which the site accepts.

**Accepted files: MP4 and MOV only, up to the limit shown on the upload form.** Anything else is
refused before the upload starts, with a message saying so.

**Keep your own copy of every recording.** The site holds one copy. Deleting a workout deletes
the file for good, and a database backup does not contain video.

## Upload a workout

1. Open Διαχείριση βίντεο → Νέο βίντεο. If it is disabled, ask the operator to finish video setup.
2. Enter a descriptive title and instructions. Select audience, body area and whether equipment
   is required. Όλοι means suitable for both audience filters.
3. Pick existing tags or add a reusable one, such as αλτήρες. Avoid near-duplicate labels.
4. Select the video file and start the upload. The file goes straight from your phone or computer
   to storage, in pieces. **Prefer Wi-Fi for anything over a few hundred megabytes.**
5. The progress bar tracks bytes sent. If the connection drops, a piece is retried on its own —
   you do not lose the whole upload. Παύση and Συνέχεια pause and continue it.
6. **Keep the page open while it uploads.** If you do close it, the upload is not lost: reopen the
   draft with Νέο ανέβασμα αρχείου, choose the same file, and it continues from where it stopped.
7. When the bar reaches 100% the site finishes and checks the upload. The video becomes Έτοιμο on
   its own — there is no processing wait. If it does not, press Έλεγχος.
8. Preview the workout and check sound, orientation, title and tags. **Publish only then.**

**Watch the storage bar** at the top of the video screen. It shows how much of the allowance is
used, and warns before it fills. When it is full, delete old workouts — nothing else frees space.

A new video is private. Clients never see drafts or failed uploads. Metadata edits save without
replacing the file. A failed or interrupted draft offers Νέο ανέβασμα αρχείου: select the source
file again. Ask the operator for help if a draft cannot start an upload at all.

## Manage the library

Use search, edit metadata, move a workout up/down within the visible page, or withdraw publication.
Withdrawal hides it and stops new playback links being issued. **A link already issued keeps
working until it expires**, which is up to two hours — withdrawal is not instant revocation.
Delete is permanent and removes the stored file; keep your own original recording first. Unused
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
- Record who owns Render, Neon, Resend, Cloudflare and Backblaze access, and who handles recovery.
- Retain source videos and confirm database backup/restore procedures and billing monitoring.
- Keep technical credentials with the operator. Do not send passwords or keys in messages.

Branded HTML/MJML email redesign is recorded in docs/backlog.md as requested; it is not included
in this milestone. Self-service forgotten-password recovery and login 2FA remain deferred scope.
