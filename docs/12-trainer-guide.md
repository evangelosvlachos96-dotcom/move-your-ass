# 12 — Trainer guide and handover

The interface is Greek. Complete production and video acceptance in docs/10 and docs/11 first.
The development screenshots and synthetic records are not the trainer's real content.

## Clients

The dashboard shows pending registrations. Open Έλεγχος εγγραφών, review the person, then approve
or decline. Approval lets a registered client sign in; their notification is queued by email.

The list of users shows one line per person: their initials, name and email, what they are
(Πελάτης or Διαχειριστής), what state their account is in, and when they registered. Everything
you can do to them is behind the ⋮ button at the end of the line - except approving and declining
a new registration, which stay on the line itself. When registrations are waiting, a bar at the
top says how many; press it to see only those, and press it again to see everyone.

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

1. Open Διαχείριση βίντεο → Νέο βίντεο. This opens a page of its own, in four parts: Βασικά
   στοιχεία, Κατηγοριοποίηση, Εξώφυλλο and Αρχείο βίντεο. If the button is greyed out, ask the
   operator to finish video setup.
2. Enter a descriptive title and instructions. Select audience, body area and whether equipment
   is required. Όλοι means suitable for both audience filters.
3. Pick existing tags or add a reusable one, such as αλτήρες. Avoid near-duplicate labels.
4. Select the video file and press Ανέβασμα βίντεο. It goes up in pieces.
   **Prefer Wi-Fi for anything over a few hundred megabytes.**
5. The progress bar tracks bytes sent. If the connection drops, a piece is retried on its own —
   you do not lose the whole upload. Παύση and Συνέχεια pause and continue it.
6. **Keep the page open while it uploads.** If you do close it, the upload is not lost: find the
   draft in the list, press Νέο ανέβασμα αρχείου, choose the same file, and it continues from
   where it stopped.
7. When the bar reaches 100% the site finishes and checks the upload. The video becomes Έτοιμο on
   its own — there is no processing wait. If it does not, press Έλεγχος.
8. Preview the workout and check sound, orientation, title and tags. **Publish only then.**

**Save and Ακύρωση are always at the bottom of the page**, and they stay there as you scroll. If
you try to leave with something unsaved, the site asks first.

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

The Εξώφυλλο section is in the editor, both when you create a workout and when you change one.
It shows the picture that is currently in use and says which of the three it is.

- Press **Ανέβασμα εικόνας** (or **Αλλαγή**) and pick a photo. A window opens where you **drag and
  pinch to choose exactly what will be shown**, in the same shape as the card, with a preview
  beside it. Press Χρήση εικόνας when it looks right.
- **JPG, PNG or WebP, up to 5 MB.** Anything else is refused with a message.
- Only the part you framed is sent, already shrunk, so a photo straight off your phone is fine.
- Choose a frame where the movement is recognisable and your face or the equipment is visible.
  A dark or blurry first frame is the usual reason a workout looks unappealing in the list.
- You can set the cover **while you are creating the workout**. It is kept and sent the moment
  the video finishes uploading.

Αφαίρεση removes yours and falls back to the captured frame. Replacing a cover deletes
the old picture straight away, and deleting a workout deletes its cover with it.

**Covers use storage too.** They are small next to video - a few hundred kilobytes against tens of
megabytes - but they count towards the same allowance shown on the storage bar.

## Your page: "Ο γυμναστής σου"

Every signed-in client has this page in the menu. You see an **Επεξεργασία** button at the top of
it; that opens a separate editing page, with a **Πίσω** button to come back and Αποθήκευση and
Ακύρωση at the bottom. If you try to leave with something unsaved, the site asks first.

- **Φωτογραφία** - a photo of you, shown as a large circle. When you pick one, a window opens
  where you **drag and pinch to centre your face in the circle** before it is saved. The photo is
  saved as soon as you confirm it, without pressing Αποθήκευση.
- **Όνομα** and **Σύντομη περιγραφή** - your name and a single line under it.
- **Βιογραφικό** - a few paragraphs about you, with a live preview underneath showing exactly what
  clients will see. You can use `**bold**` for emphasis and lines starting with `- ` for a list.
  Everything else is shown as plain text, on purpose.
- **Στοιχεία επικοινωνίας** - your email and phone. The email is where clients' messages go.
- **Ραντεβού** - **Σύνδεσμος κράτησης ραντεβού**. Put your booking page here and an orange
  **Κλείσε ραντεβού** button appears for your clients in four places: at the bottom of the menu, as
  a fourth button in the bar at the bottom of a phone, at the top of Προπονήσεις, and at the end of
  this page. Yours is:

  ```
  https://reply-now.com/book/tasos__ch
  ```

  Paste it exactly, both underscores included. Leave the field empty and the button disappears
  everywhere - that is how you turn it off. You will not see it in your own menu: it is for your
  clients, and you can see what it looks like on this page.
- **Κοινωνικά δίκτυα** - press **Προσθήκη κοινωνικού δικτύου**, choose one from the list, and fill
  in its address. Add only the ones you actually use; a network you have already added disappears
  from the list so you cannot add it twice. Each row has buttons to move it up or down (that is
  the order clients see) and to remove it. Links must start with `https://`, and WhatsApp is just
  digits with the country code in front.

**If you leave the page empty**, clients see an empty page with a working message form. Fill it in
before you invite anyone.

### Messages from clients

The message form is for your clients only - you will not see it on your own page, because it would
just write to you.

Clients can write to you from that page. The message arrives as an email, with the client's own
address as the reply address - **press reply in your mail app and it goes to them.** Nothing is
stored on the site, so there is no inbox to check here; it is ordinary email.

Messages go to the email address you put in Στοιχεία επικοινωνίας. If you leave that blank, they
go to every admin account instead. A client cannot send the same message twice in a row, and
cannot send a burst of them.

## The list of your workouts

Each workout is one card: its picture, its title, two badges (published or private, and whether
the file is ready), and a line saying how long it is, how big, and when you added it.

- **The button on the card is the one thing you most likely want**: Δημοσίευση for a finished
  workout, Απόσυρση for one that is already published, or Νέο ανέβασμα αρχείου if its upload
  never finished.
- **Everything else is behind the ⋮ button**: Επεξεργασία, Προεπισκόπηση, Μετακίνηση πάνω and
  κάτω, Έλεγχος κατάστασης, and Διαγραφή. On a phone it opens as a list you can tap.

You can also fix a workout from the client side: in Προπονήσεις each card has a pencil, and a
workout you are watching has an Επεξεργασία button. Both take you to the same editor and bring
you back where you were. Your clients never see either of them.

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

## Email

If someone says they never received an invitation, check the address first and then resend it from
that user's ⋮ menu. If invitations stop arriving for everyone, tell the operator - that is a
provider problem, not something to retry.

Emails are branded and carry the Move Your Ass header. Many mail apps block images by default, so
the header is written to still read as Move Your Ass with the picture switched off.

Login 2FA remains deferred scope. Forgotten-password recovery is implemented: clients use
"Ξέχασες τον κωδικό;" on the login page and never need to ask you for a password.
