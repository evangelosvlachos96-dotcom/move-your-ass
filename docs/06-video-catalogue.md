# 06 — Video Catalogue

**Scope change, September 2026.** Session booking is dropped. The product is a private video
library: the trainer uploads and categorises workout videos, clients browse and filter them.

Auth, roles, and admin approval are unchanged and still required. Everything in `docs/03` §5
about booking concurrency — the atomic `UPDATE`, the 5-booking cap, tab sync — no longer applies
and moves to `docs/backlog.md`. Idempotency keys survive, but only on video creation.

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

## 3. Schema

```
Video
  Id                uniqueidentifier  PK
  Title             nvarchar(200)     NOT NULL
  Description       nvarchar(2000)    NULL
  Audience          int               NOT NULL   -- 0 Male, 1 Female, 2 Both
  BodyArea          int               NOT NULL   -- 0 FullBody, 1 UpperBody, 2 LowerBody
  RequiresEquipment bit               NOT NULL
  DurationSeconds   int               NULL       -- filled by the provider after processing
  ThumbnailUrl      nvarchar(500)     NULL
  StorageProvider   int               NOT NULL   -- 0 BunnyStream, 1 AzureBlob
  ExternalId        nvarchar(200)     NOT NULL   -- provider's video id / blob prefix
  Status            int               NOT NULL   -- 0 Uploading, 1 Processing, 2 Ready, 3 Failed
  IsPublished       bit               NOT NULL   DEFAULT 0
  SortOrder         int               NOT NULL   DEFAULT 0
  CreatedAtUtc      datetime2(3)      NOT NULL
  UpdatedAtUtc      datetime2(3)      NULL
  CreatedByUserId   nvarchar(450)     NOT NULL
  RowVersion        rowversion

Tag
  Id            uniqueidentifier PK
  Name          nvarchar(60)     NOT NULL    -- as typed, for display
  NormalizedName nvarchar(60)    NOT NULL    -- upper-invariant, trimmed, for matching
  CreatedAtUtc  datetime2(3)     NOT NULL

VideoTag
  VideoId  uniqueidentifier  FK → Video   ON DELETE CASCADE
  TagId    uniqueidentifier  FK → Tag
  PRIMARY KEY (VideoId, TagId)
```

```sql
CREATE UNIQUE INDEX UX_Tag_Normalized ON Tag(NormalizedName);
CREATE INDEX IX_Video_Browse ON Video(Audience, BodyArea, RequiresEquipment, SortOrder)
  WHERE IsPublished = 1 AND Status = 2;
CREATE INDEX IX_VideoTag_Tag ON VideoTag(TagId);
```

`Status` exists because transcoding is asynchronous. A video is not playable the moment the
upload finishes, and the admin UI must show that rather than serving a broken player.

`StorageProvider` and `ExternalId` keep the entity host-agnostic — a Bunny video id and an Azure
blob prefix both fit, so switching providers is a data migration, not a schema change.

Deleting a video removes the row and the remote asset. Tags are never auto-deleted; an orphaned
tag simply stops appearing in filters (the filter list is built from tags that have at least one
published video).

---

## 4. Hosting decision

**Bunny Stream.** Recommended, and a change from ADR-008.

The original plan — encode an HLS ladder with ffmpeg, upload segments to Azure Blob, serve with
a directory-scoped SAS — assumed a technical person preparing each video. With the trainer
uploading from a phone, that assumption is dead. An unencoded 1080p phone recording served as a
single MP4 will buffer on mobile data, and nobody is going to run a shell command before each
upload.

Bunny Stream transcodes on upload, produces the adaptive ladder automatically, hosts a player,
and supports token-authenticated playback URLs so videos are not publicly guessable. At roughly
€0.01/GB stored and €0.005/GB delivered, a hundred videos and fifteen clients costs well under
a euro a month.

What this costs you: a second vendor outside Azure, and playback URLs signed with a Bunny token
rather than an Azure SAS. The `IVideoStorage` abstraction from `docs/02` absorbs the difference.

**If you insist on Azure-only:** Blob with plain MP4, no ladder, accepting that mobile playback
will be rough. Do not build a server-side transcoder on App Service — an ffmpeg process will
exhaust a B1 instance and block the request thread.

### Upload flow

```
Admin fills the form (title, audience, body area, tags)
  → POST /api/admin/videos          creates the row, Status = Uploading,
                                     returns { videoId, uploadUrl, uploadSignature }
  → browser uploads the file DIRECTLY to the provider using that URL
                                     (never through the API — a 1 GB POST kills a B1 instance)
  → provider webhook → POST /api/webhooks/video-ready
                                     sets Status = Ready, DurationSeconds, ThumbnailUrl
  → admin toggles IsPublished when happy
```

The webhook endpoint is anonymous but must verify the provider's signature header. Treat an
unsigned or badly-signed call as hostile and return 401.

---

## 5. API surface

```
# Admin
POST   /api/admin/videos                 create + get upload credentials  (Idempotency-Key)
PUT    /api/admin/videos/{id}            edit title, description, audience, body area, tags
POST   /api/admin/videos/{id}/publish    IsPublished = true
POST   /api/admin/videos/{id}/unpublish  IsPublished = false
DELETE /api/admin/videos/{id}            delete row + remote asset
GET    /api/admin/videos                 all videos, any status, paged
POST   /api/admin/videos/reorder         [{ id, sortOrder }]
GET    /api/admin/tags                   all tags with usage counts
DELETE /api/admin/tags/{id}              only if unused

# Client
GET    /api/videos                       ?audience=&bodyArea=&equipment=&tags=a,b&search=&page=
                                         equipment omitted = both; true/false to narrow
                                         published + Ready only
GET    /api/videos/{id}                  detail
GET    /api/videos/{id}/playback         short-lived signed playback URL
GET    /api/videos/filters               available tags, for building the filter UI

# Webhook
POST   /api/webhooks/video-ready         anonymous, signature-verified
```

`GET /api/videos` must never return unpublished or non-Ready videos, regardless of query
parameters. Enforce it in the handler, not by trusting the caller.

---

## 6. UI

**Admin — video list**

Table: thumbnail, title, audience, body area, equipment, tags, status pill, published toggle. Actions per
row: edit, delete. A prominent "Νέο βίντεο" button.

**Admin — add/edit form**

Title, description, audience (three radio buttons), body area (three radio buttons),
equipment (two radio buttons — no default, so it cannot be saved unset by accident), tags
(`mat-chip-grid` with autocomplete over existing tags, Enter creates a new one), file drop zone
with a real progress bar. Save is disabled until title, audience, body area, and equipment are all set.

The progress bar matters. A 500 MB upload over Greek mobile takes minutes, and without feedback
the trainer will assume it froze and refresh the page.

**Client — library**

Filter bar at the top: audience chips, body area chips, equipment chips, tag chips, search box. Responsive card
grid below — one column on phones, two on tablets, three or four on desktop. Each card shows
thumbnail, title, duration, its body-area badge, and an equipment icon.

Filter state lives in the URL query string so a filtered view can be bookmarked and shared, and
the back button behaves.

**Client — player**

Provider's embedded player, title, description, tags. Nothing else.

---

## 7. Revised phases

| Phase | Contents | Status |
|---|---|---|
| 1 | Auth, roles, admin approval, email 2FA, plain shell UI | in progress |
| 2 | Video + Tag schema, admin CRUD, upload, webhook | next |
| 3 | Client library: filters, search, player | |
| 4 | Deploy to Azure, custom domain, real content | |

Booking, slots, and change requests are recorded in `docs/backlog.md` under "dropped — may
return". The design work is not wasted if a calendar comes back later.
