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

The additive `VideoObjectStorage` migration adds four nullable columns for S3 storage (ADR-019):
`UploadId` (200) holding the multipart upload while one is in flight, `SizeBytes` counted against
the storage cap, `ContentType`, and `ThumbnailObjectKey` (200). `ExternalId` now holds the
object key rather than a provider asset ID, and its unique index still prevents two rows claiming
the same object. **`ThumbnailUrl` is left unused** — poster URLs are presigned per response and
never stored. It is kept on purpose: dropping a column inverts the deploy order, because the new
code must be live before the migration runs. See `docs/backlog.md`.

Tag has UUID Id, display Name (60), unique NormalizedName (120), UTC creation time. Normalization
trims, removes combining accents and uppercases invariantly, so Greek spelling variants collapse.
VideoTag has a composite VideoId/TagId key, cascade deletion from Video and restricted Tag deletion.
The creator ID is historical attribution rather than a cascading user foreign key.

Indexes cover unique creator/key, unique non-null external ID, publication/status/order and TagId.
Revision is an application-managed UUID concurrency token. The additive VideoCatalogue migration
preserves the applied PostgreSQL InitialCreate. Never regenerate an applied baseline.

Statuses: Uploading, Processing, Ready, Failed, Deleting. Only Ready + IsPublished is visible to
clients. Nothing publishes a workout automatically. `Processing` is unreachable under S3 storage
and is kept only so persisted integer values do not shift.

## 4. Storage and state transitions

**Backblaze B2 through its S3-compatible API, behind a provider-neutral `IVideoStorage`
(ADR-019).** Plain object storage: no transcoding, no HLS, no provider player, no webhooks. The
same adapter serves Cloudflare R2 or MinIO by configuration alone. No video bytes are proxied
through Render.

**Upload.** The admin creates metadata with an `Idempotency-Key`, declaring the file's content
type and size. The API decides everything before issuing a single URL: the type must be
`video/mp4` or `video/quicktime`, the size must be within `Video:S3:MaxFileBytes`, and the total
must stay within `Video:S3:StorageCapBytes`. Only then does it start a multipart upload and
return one presigned PUT per part. The browser uploads parts directly; a failed part is retried
on its own, so a flaky mobile connection costs one part rather than the recording.

**The multipart lifecycle is server-side.** `CreateMultipartUpload`, `ListParts`,
`CompleteMultipartUpload` and `AbortMultipartUpload` are all signed API calls made by the API.
The browser never reads a part's ETag, so **the bucket only has to allow `s3_put` from the
browser and never has to expose `ETag` over CORS**. Nothing the browser reports is trusted.

**Completion replaces the webhook.** After the last part the browser calls
`POST /upload/complete`. The API completes the upload, **HEADs the object**, and compares its
size and content type with what was declared. A match becomes `Ready`; a mismatch deletes the
object and leaves the row `Failed`. `Refresh` re-derives state the same way. `Processing` remains
in the status enum so persisted integers do not shift, but is unreachable for S3.

**Resume is durable, not session-bound.** The multipart `UploadId` is persisted on the row, so
reopening a draft and selecting the same file returns the parts the provider already holds and
re-presigns the rest. Selecting a different file aborts the previous upload first.

**Storage cap counts drafts.** A multipart upload's parts occupy provider storage from the moment
they land, so the cap sums `SizeBytes` over every row. Aborting or deleting a draft aborts its
upload so those parts stop counting.

**Thumbnail.** Optional and best effort. The upload ticket includes one presigned PUT for a
poster frame; the browser captures a frame and its duration from the selected file and stores it
there. Completion records the key only if a HEAD confirms a plausible object. A browser that
cannot decode the recording simply sends none, and the card shows the branded placeholder.

**Playback.** A presigned GET with the configured lifetime, default two hours, played in a native
`<video>` element using HTTP range requests. **A presigned link works for anyone holding it until
it expires** — it survives unpublishing and suspension. Accepted trade-off; see ADR-019. No
provider key ever reaches the browser.

Database reservation prevents a racing creator/key from starting a second upload. Replaying a
matching payload returns the existing ID and a ticket for the upload in flight; a changed payload
returns 409. The guarantee lasts while the row exists; deletion removes its key reservation.
Object keys are derived from the video's ID (`videos/{id}.mp4|.mov`), so an uncertain response
cannot orphan a second copy — a retry addresses the same key.

Delete first marks `Deleting` and unpublishes, then aborts any upload in flight, removes the
object and its poster frame, then the database row. Provider failure leaves a retryable row.
Already-missing objects count as deleted. Tags are retained; only unused tags can be deleted.
Update/publish/reorder require current `Revision` values. Ordering is a stable
SortOrder/creation-time/ID sort; the UI offers adjacent moves on the visible page. It does not
offer a cross-page drag interface.

## 5. API surface

| Method | Path | Purpose |
|---|---|---|
| GET/POST | /api/admin/videos | Paged management list / create `{ video, file }` with Idempotency-Key |
| GET | /api/admin/videos/summary | Counts, provider availability and storage used against the cap |
| GET/PUT/DELETE | /api/admin/videos/{id} | Detail / metadata with revision / retryable deletion |
| POST | /api/admin/videos/{id}/upload | Upload ticket for an Uploading/Failed draft; resumes the same file |
| POST | /api/admin/videos/{id}/upload/complete | Complete, verify by HEAD, mark Ready |
| POST | /api/admin/videos/{id}/upload/abort | Abandon an upload in flight and release its parts |
| POST | /api/admin/videos/{id}/refresh | Re-derive state from the stored object |
| POST | /api/admin/videos/{id}/publish or /unpublish | Publication with revision |
| GET | /api/admin/videos/{id}/playback | Ready draft preview |
| POST | /api/admin/videos/reorder | Array of id, sortOrder, revision |
| GET/POST | /api/admin/tags | Usage counts / normalized creation |
| DELETE | /api/admin/tags/{id} | Delete only if unused |
| GET | /api/videos | Published Ready catalogue; filters and paging |
| GET | /api/videos/{id} | Client-visible detail |
| GET | /api/videos/{id}/playback | Short-lived presigned GET |
| GET | /api/videos/filters | Tags used by published Ready videos |

There is no webhook endpoint: object storage emits no events. An upload ticket is
`{ partSizeBytes, partCount, parts: [{ partNumber, url }], uploadedParts, thumbnailUploadUrl }`
and carries no provider credential. A video's `thumbnailUrl` is a presigned poster URL computed
per response, or null when no frame was captured; presigning is a local signature, so a page of
cards costs no provider round trip. Video error codes: `VIDEO_FILE_TYPE`,
`VIDEO_FILE_TOO_LARGE`, `VIDEO_STORAGE_FULL`, `VIDEO_UPLOAD_MISMATCH`,
`VIDEO_PROVIDER_UNAVAILABLE`, `VIDEO_CONFLICT`, `VIDEO_NOT_FOUND`, `VIDEO_INVALID`.

List parameters: audience, bodyArea, equipment, search, page, pageSize and repeated tags or indexed
tags[0], tags[1]. Defaults: page 1, pageSize 12; max pageSize 100. Equipment omitted means both.
Title/description search is literal and case-insensitive; accent-insensitive free-text search is
not promised. All video handlers recheck current active-account status; admin handlers recheck role.
Signed URL responses are not cached. A previously issued link can work until expiry after withdrawal
or account suspension; this is not DRM or instantaneous revocation of an already playing stream.

## 6. UI and acceptance

Admin dashboard links pending registrations, users, videos and published library with real counts.
The video screen shows **storage used against the cap**, with a warning band from 80%. The upload
form has explicit audience/body/equipment choices, controlled tag checkboxes and inline tag
creation, a file picker limited to MP4 and MOV, byte progress, pause, resume, cancel and a
navigation warning. The size limit shown comes from the API, not a hardcoded number.

Resume works both within the open page and after reopening the draft and selecting the same file:
the server knows which parts already landed. Selecting a different file starts over. Cancel aborts
the multipart upload so its parts stop using the allowance. Metadata remains editable while
storage is unconfigured.

Client dashboard contains the library: URL-backed search/filters, responsive cards, pagination and
a native `<video>` player fed by a short-lived presigned URL. It provides empty/error/loading
states and retry without changing the route. Shared brand tokens keep the existing dark theme.
Native selects are used instead of the original radio/chip proposal; taxonomy and filter semantics
are unchanged.

Provider setup and live acceptance: docs/11-video-operations.md. Trainer guide: docs/12-trainer-guide.md.
Local automated and browser evidence: docs/08-milestone-handover.md. Booking remains out of scope.
