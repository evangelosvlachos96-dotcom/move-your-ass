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

## The picture clients see on a workout

Every workout shows a picture in the library list. It comes from the first of these that exists:

1. **A cover you upload yourself** - always wins.
2. **A frame the site captured while the video was uploading** - automatic, no work from you.
3. **The Move Your Ass placeholder** - if neither of the above is available.

To set your own, open the workout in Διαχείριση βίντεο and use Εξώφυλλο: Αλλαγή εξωφύλλου.

- **JPG, PNG or WebP, up to 5 MB.** Anything else is refused with a message.
- The picture is **shrunk in your browser before it is sent**, so a photo straight off your phone
  is fine - you do not need to resize it first.
- **Landscape works best.** The card is wider than it is tall, so a portrait photo gets cropped at
  the top and bottom.
- Choose a frame where the movement is recognisable and your face or the equipment is visible.
  A dark or blurry first frame is the usual reason a workout looks unappealing in the list.

Αφαίρεση εξωφύλλου removes yours and falls back to the captured frame. Replacing a cover deletes
the old picture straight away, and deleting a workout deletes its cover with it.

**Covers use storage too.** They are small next to video - a few hundred kilobytes against tens of
megabytes - but they count towards the same allowance shown on the storage bar.

## Your page: "Ο γυμναστής σου"

Every signed-in client has this page in the menu, and only you can edit it. Press Επεξεργασία,
change what you want, and press Αποθήκευση.

- **Φωτογραφία** - a photo of you. It is shown as a large circle, so put your face near the middle
  of the picture; the edges are cropped away. Same formats and size limit as a cover.
- **Όνομα** and **Σύντομη περιγραφή** - your name and a single line under it.
- **Βιογραφικό** - a few paragraphs about you. You can use `**bold**` for emphasis and lines
  starting with `- ` for a list. Everything else is shown as plain text, on purpose.
- **Στοιχεία επικοινωνίας** - email, phone, Instagram, YouTube, TikTok, Facebook, WhatsApp and a
  website. **Every one is optional.** Fill in only what you want every client to see - whatever
  you put here is visible to all of them. Links must start with `https://`, or they are refused.

**If you leave the page empty**, clients see an empty page with a working message form. Fill it in
before you invite anyone.

### Messages from clients

Clients can write to you from that page. The message arrives as an email, with the client's own
address as the reply address - **press reply in your mail app and it goes to them.** Nothing is
stored on the site, so there is no inbox to check here; it is ordinary email.

Messages go to the email address you put in Στοιχεία επικοινωνίας. If you leave that blank, they
go to every admin account instead. A client cannot send the same message twice in a row, and
cannot send a burst of them.

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

## Checking that email still works

The dashboard has **Αποστολή δοκιμαστικού email**. It sends one short message to your own address
and nothing else. Use it when you are not sure whether email is working - before inviting a batch
of clients, or if someone says they never received an invitation. If it arrives, the mail
provider is fine and the problem is somewhere else (usually the client's spam folder). If it does
not arrive within a couple of minutes, tell the operator.

It is deliberately limited to a few sends in a row.

Emails are branded and carry the Move Your Ass header. Many mail apps block images by default, so
the header is written to still read as Move Your Ass with the picture switched off.

Login 2FA remains deferred scope. Forgotten-password recovery is implemented: clients use
"Ξέχασες τον κωδικό;" on the login page and never need to ask you for a password.
