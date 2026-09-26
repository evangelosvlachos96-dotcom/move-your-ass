# 06 — Video Catalogue

**Scope change, September 2026.** Session booking is dropped. The product is a private video
library: the trainer uploads and categorises workout videos, clients browse and filter them.

Auth, roles, admin approval and email invitations are required. Booking concurrency is outside
scope. Idempotency keys are planned for video creation only.

---

## 1. Taxonomy

Every video carries four pieces of classification.

**Audience** — required, single choice.

| Value | Greek label |
|---|---|
| `Male` | Άντρες |
| `Female` | Γυναίκες |
| `Both` | Και τα δύο |

**BodyArea** — required, single choice.

| Value | Greek label |
|---|---|
| `FullBody` | Όλο το σώμα |
| `UpperBody` | Πάνω μέρος |
| `LowerBody` | Κάτω μέρος |

**Equipment** — required, single choice. Binary on purpose.

| Value | Greek label |
|---|---|
| `false` | Χωρίς εξοπλισμό |
| `true` | Με εξοπλισμό |

No "minimal equipment" middle option. Nobody agrees where that line sits — a mat counts for one
person and not another — and an ambiguous filter is worse than a coarse one. If the trainer wants
to say *which* equipment, that is what tags are for: αλτήρες, λάστιχα, παγκάκι.

**Tags** — optional, zero or more per video. This is the "extra label". Controlled vocabulary,
not free text: the admin picks from existing tags or creates a new one inline, and the new tag
joins the list for next time.

Free text was rejected. "Κοιλιακοί", "κοιλιακοί" and "Κοιλιακοι" would become three separate
filter entries, and the filter list degrades silently as the catalogue grows. A tag table costs
one extra entity and one join.

---

## 2. Filter semantics

This is the part that is easy to get wrong.

**Audience.** `Both` is not a third bucket — it means the video suits either. So filtering by
`Male` returns videos tagged `Male` **or** `Both`.

```csharp
query = filter.Audience switch
{
    Audience.Male   => query.Where(v => v.Audience == Audience.Male   || v.Audience == Audience.Both),
    Audience.Female => query.Where(v => v.Audience == Audience.Female || v.Audience == Audience.Both),
    _               => query   // no filter selected
};
```

**BodyArea.** Plain equality. Selecting nothing means no filter.

**Equipment.** Tri-state in the UI, binary in the data: the client can select "χωρίς", "με", or
leave it unset. Unset means no filter — do not default it to `false`, or the catalogue looks half
empty on first load.

**Tags.** Multi-select. Selecting several tags returns videos carrying **any** of them (OR),
not all of them. With a catalogue this size, AND returns empty results too often to be useful.

**Across dimensions:** AND. "Γυναίκες + Κάτω μέρος + Κοιλιακοί" means all three must hold.

**Search.** A free-text box matching title and description, combined with the filters using AND.

---

## 3. Implemented schema (PostgreSQL)

Video has UUID Id/Revision, required title (200), optional description (2000), integer audience,
body area and status, boolean equipment/publication, integer ordering, optional duration,
provider name, nullable external ID (64) and thumbnail URL (1000), UTC timestamptz creation/update,
creator ID (450), creation key (100) and SHA-256 payload hash (64).

Tag has UUID Id, display Name (60), unique NormalizedName (120), UTC creation time. Normalization
trims, removes combining accents and uppercases invariantly, so Greek spelling variants collapse.
VideoTag has a composite VideoId/TagId key, cascade deletion from Video and restricted Tag deletion.
The creator ID is historical attribution rather than a cascading user foreign key.

Indexes cover unique creator/key, unique non-null external ID, publication/status/order and TagId.
Revision is an application-managed UUID concurrency token. The additive VideoCatalogue migration
preserves the applied PostgreSQL InitialCreate. Never regenerate an applied baseline.

Statuses: Uploading, Processing, Ready, Failed, Deleting. Only Ready + IsPublished is visible to
clients. The provider never publishes a workout automatically.

## 4. Provider and state transitions

Bunny Stream is implemented behind IVideoStorage (ADR-018). No video bytes are proxied through
Render. The admin creates metadata with an Idempotency-Key, receives { id, upload }, and uploads
directly using TUS temporary credentials. A provider webhook or explicit admin Refresh fetches
authoritative provider status, duration and thumbnail. The admin previews and publishes separately.

Uploads authorize six hours; playback links authorize fifteen minutes. Raw provider keys never
reach the browser. The webhook requires version v1, algorithm hmac-sha256 and a valid signature
over exact request bytes using the library read-only API key. Invalid signatures return 401;
malformed signed payloads return 400. Unknown/deleted assets are acknowledged without recreation.
The request body limit is 16 KiB. Replay reads current provider state and cannot publish a draft.

Database reservation prevents duplicate provider creation for a racing creator/key. Replaying a
matching payload returns the existing ID; changed payload returns 409. The guarantee lasts while
the row exists; deletion removes its key reservation. If remote creation succeeds but its response
is lost, reconcile the Bunny library before removing the failed draft and starting anew. The API
does not retry non-idempotent creation blindly. This distributed-transaction limit is documented,
not hidden behind a retry policy.

Delete first marks Deleting and unpublishes, then removes the remote asset, then the database row.
Provider failure leaves a retryable row. Already-missing remote assets count as deleted. Tags are
retained; only unused tags can be deleted. Update/publish/reorder require current Revision values.
Ordering is a stable SortOrder/creation-time/ID sort; the UI offers adjacent moves on the visible
page. It does not offer a cross-page drag interface.

## 5. API surface

| Method | Path | Purpose |
|---|---|---|
| GET/POST | /api/admin/videos | Paged management list / create with Idempotency-Key |
| GET | /api/admin/videos/summary | Actual counts and provider availability |
| GET/PUT/DELETE | /api/admin/videos/{id} | Detail / metadata with revision / retryable deletion |
| POST | /api/admin/videos/{id}/upload | New credentials for Uploading/Failed asset |
| POST | /api/admin/videos/{id}/refresh | Read current provider state |
| POST | /api/admin/videos/{id}/publish or /unpublish | Publication with revision |
| GET | /api/admin/videos/{id}/playback | Ready draft preview |
| POST | /api/admin/videos/reorder | Array of id, sortOrder, revision |
| GET/POST | /api/admin/tags | Usage counts / normalized creation |
| DELETE | /api/admin/tags/{id} | Delete only if unused |
| GET | /api/videos | Published Ready catalogue; filters and paging |
| GET | /api/videos/{id} | Client-visible detail |
| GET | /api/videos/{id}/playback | Short-lived signed embed link |
| GET | /api/videos/filters | Tags used by published Ready videos |
| POST | /api/webhooks/video-ready | Anonymous but signature-verified provider event |

List parameters: audience, bodyArea, equipment, search, page, pageSize and repeated tags or indexed
tags[0], tags[1]. Defaults: page 1, pageSize 12; max pageSize 100. Equipment omitted means both.
Title/description search is literal and case-insensitive; accent-insensitive free-text search is
not promised. All video handlers recheck current active-account status; admin handlers recheck role.
Signed URL responses are not cached. A previously issued link can work until expiry after withdrawal
or account suspension; this is not DRM or instantaneous revocation of an already playing stream.

## 6. UI and acceptance

Admin dashboard links pending registrations, users, videos and published library with real counts.
The upload form has explicit audience/body/equipment choices, controlled tag checkboxes and inline
tag creation, a file picker, progress, pause/resume and navigation warning. Files are limited to
5 GiB by the UI. Resume works in the open page; after navigation select the file again using the
draft's upload action. Metadata remains editable without a configured provider.

Client dashboard contains the library: URL-backed search/filters, responsive cards, pagination and
the signed embedded player. It provides empty/error/loading states and retry without changing the
route. Shared brand tokens keep the existing dark theme. Native selects are used instead of the
original radio/chip proposal; taxonomy and filter semantics are unchanged.

Provider setup and live acceptance: docs/11-video-operations.md. Trainer guide: docs/12-trainer-guide.md.
Local automated and browser evidence: docs/08-milestone-handover.md. Booking remains out of scope.
