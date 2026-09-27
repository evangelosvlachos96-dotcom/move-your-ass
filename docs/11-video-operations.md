# 11 — Video operations (Backblaze B2)

Video is stored in **Backblaze B2** through its S3-compatible API, behind a provider-neutral
adapter (ADR-019). B2's free tier is **10 GB of storage with no card**. There is no transcoding,
no HLS, no provider player and no webhook: the object the trainer uploads is the object clients
play.

The code is implemented and tested against a real S3 implementation in a container. **No
Backblaze account, bucket, key or CORS rule has been created by any agent.** Everything in
sections 1 to 4 is owner work, and section 6 is the acceptance run that has to pass before the
trainer uploads real content.

> Values are typed by the owner into user-secrets locally or the Render dashboard in production.
> No key, secret or connection value belongs in this file, in the repository, or in a chat.

## 1. Account and bucket

1. Create a Backblaze account at backblaze.com and enable **B2 Cloud Storage**. No card is
   needed for the free tier.
2. **Buckets → Create a Bucket.** Give it a name (bucket names are globally unique, so something
   like `moveyourass-videos` is sensible). Set **Files in Bucket are: Private**. Leave object lock
   off and default encryption off.
3. Note the **Endpoint** shown on the bucket, for example `s3.eu-central-003.backblazeb2.com`.
   Choose an EU region if offered, to sit near the Frankfurt Render service and the clients.
4. **The region is the middle segment of that hostname.** For `s3.eu-central-003.backblazeb2.com`
   the region is `eu-central-003`. This is what `Video:S3:Region` needs, and getting it wrong
   produces a SigV4 signature mismatch rather than a helpful message.

## 2. Application key

**Application Keys → Add a New Application Key.**

- **Name:** something identifiable, such as `moveyourass-api`.
- **Allow access to Bucket(s):** the one bucket above, never "All".
- **Type of Access:** **Read and Write**.
- Leave the file-name prefix and duration empty.

Backblaze shows `keyID` and `applicationKey` **once**. `keyID` is `Video:S3:AccessKeyId` and
`applicationKey` is `Video:S3:SecretAccessKey`.

> The master application key will not work here. Use a bucket-scoped key: it is the difference
> between a leaked credential costing one bucket and costing the whole account.

## 3. CORS — the browser uploads directly, so this is required

Without CORS the browser cannot PUT to the bucket at all and every upload fails at part 1.

**What is actually needed is small**, because the multipart lifecycle is server-side: the browser
only ever sends `PUT` for parts and the poster frame, and `GET` for playback. **It never reads an
`ETag`** — the API collects part ETags itself with `ListParts` — so no `exposeHeaders` entry is
strictly required. `ETag` is included below anyway as a harmless belt-and-braces measure; if
Backblaze rejects it, remove that line and everything still works.

### Option A — the Backblaze web UI (try this first)

**Buckets → your bucket → CORS Rules → Custom CORS rules**, then add one rule:

| Field | Value |
|---|---|
| CORS Rule Name | `moveyourass-web` |
| Origins | `https://moveyourass.gr` and `http://localhost:4200` |
| Allowed Operations | `s3_put`, `s3_get`, `s3_head` |
| Allowed Headers | `*` |
| Expose Headers | `ETag` |
| Max Age Seconds | `3600` |

B2 only offers `s3_put`, `s3_get`, `s3_head` and `s3_delete` for the S3 API — there is no
`s3_post`, and none is needed. Do **not** grant `s3_delete`: deletion is a server-side call.

### Option B — the AWS CLI, if the UI cannot express the rule

B2's S3 API accepts `PutBucketCors`, so the standard AWS CLI works. Save this as `cors.json`
**outside the repository**:

```json
{
  "CORSRules": [
    {
      "AllowedOrigins": ["https://moveyourass.gr", "http://localhost:4200"],
      "AllowedMethods": ["PUT", "GET", "HEAD"],
      "AllowedHeaders": ["*"],
      "ExposeHeaders": ["ETag"],
      "MaxAgeSeconds": 3600
    }
  ]
}
```

Then, with the key from section 2 configured in the AWS CLI:

```powershell
aws s3api put-bucket-cors --endpoint-url https://<your endpoint> --bucket <your bucket> --cors-configuration file://cors.json
aws s3api get-bucket-cors --endpoint-url https://<your endpoint> --bucket <your bucket>
```

### Option C — the b2 CLI

```powershell
b2 bucket update <your bucket> allPrivate --cors-rules '[{"corsRuleName":"moveyourass-web","allowedOrigins":["https://moveyourass.gr","http://localhost:4200"],"allowedOperations":["s3_put","s3_get","s3_head"],"allowedHeaders":["*"],"exposeHeaders":["ETag"],"maxAgeSeconds":3600}]'
```

**Check:** open the site, start an upload, and watch the browser's network tab. The `OPTIONS`
preflight to the bucket must return 200 and the `PUT` that follows must return 200. A CORS error
in the console means the rule's origin does not match exactly — scheme and port included.

**Remove `http://localhost:4200` from the rule once local testing is finished**, or leave it and
accept that a page served from a developer machine can talk to the bucket.

## 4. Settings

| Local user-secret | Render environment variable | Value |
|---|---|---|
| `Video:Provider` | `Video__Provider` | `S3` |
| `Video:S3:Enabled` | `Video__S3__Enabled` | `true` |
| `Video:S3:ServiceUrl` | `Video__S3__ServiceUrl` | `https://` + the bucket's endpoint |
| `Video:S3:Region` | `Video__S3__Region` | the region inside that hostname |
| `Video:S3:AccessKeyId` | `Video__S3__AccessKeyId` | **secret** — the key's `keyID` |
| `Video:S3:SecretAccessKey` | `Video__S3__SecretAccessKey` | **secret** — the key's `applicationKey` |
| `Video:S3:BucketName` | `Video__S3__BucketName` | the bucket name |

Optional, with defaults that are already right for the free tier:

| Setting | Default | Why you might change it |
|---|---|---|
| `Video:S3:MaxFileBytes` | 2 GiB | A hard ceiling on one recording |
| `Video:S3:StorageCapBytes` | 9 GiB | Headroom under B2's 10 GB free tier |
| `Video:S3:PartSizeBytes` | 16 MiB | Smaller parts survive a worse connection; must be ≥ 5 MiB |
| `Video:S3:PlaybackMinutes` | 120 | How long a playback link stays usable — and shareable |
| `Video:S3:UploadMinutes` | 360 | How long upload URLs last before a resume re-issues them |

**Startup validation refuses to start** with `Enabled=true` and a missing or malformed value:
the endpoint must be absolute HTTPS with no path, the part size must be between 5 MiB and 5 GiB,
and the max file size must be reachable within 10,000 parts. `Video:Provider` set to anything
other than `S3` also fails startup rather than silently disabling uploads.

Local setup, with placeholders — **the owner types the real values**:

```powershell
dotnet user-secrets set "Video:Provider" "S3" --project src/Mya.Api
dotnet user-secrets set "Video:S3:Enabled" "true" --project src/Mya.Api
dotnet user-secrets set "Video:S3:ServiceUrl" "<https://s3.<region>.backblazeb2.com>" --project src/Mya.Api
dotnet user-secrets set "Video:S3:Region" "<region>" --project src/Mya.Api
dotnet user-secrets set "Video:S3:AccessKeyId" "<keyID>" --project src/Mya.Api
dotnet user-secrets set "Video:S3:SecretAccessKey" "<applicationKey>" --project src/Mya.Api
dotnet user-secrets set "Video:S3:BucketName" "<bucket name>" --project src/Mya.Api
```

## 5. Free limits, and which one bites first

| Allowance | Free | What happens past it |
|---|---|---|
| Storage | **10 GB** | The app's own 9 GiB cap refuses new uploads first, with a Greek message |
| **Download (egress)** | **3× the average monthly data stored** | $0.01 per GB |
| Class A/B/C transactions | free | — |
| Class D transactions | 2,500/day free | $0.004 per 10,000 |

**Egress is the one to watch, not storage.** At 9 GB stored the free egress is roughly 27 GB a
month. One 200 MB workout watched 135 times uses all of it. Every play streams the whole original
file, because there is no adaptive bitrate to serve a smaller rendition.

**Where to look:** Backblaze dashboard → **Reports** (or **Caps & Alerts**). It shows stored
bytes and downloaded bytes for the month. Check it in the same weekly pass as Neon and Render.
**Set a caps alert** while you are there: Backblaze lets you cap daily download bytes and
transactions, which turns a runaway bill into a failed download.

Two things reduce egress if it becomes a problem: smaller source files — 720p instead of 1080p is
roughly half — and Cloudflare R2, which charges no egress at all and needs only different values
in section 4.

## 6. Live acceptance — mandatory before the trainer uploads real content

Nothing below is proven by the automated tests. They run against MinIO in a container, which is
evidence about the adapter, not about Backblaze.

- Upload a real phone recording through the admin form. Watch the network tab: **only presigned
  URLs**, never a key. Confirm byte progress moves and the preflight and PUTs return 200.
- Confirm B2 accepts the SDK's requests at all. **If a request fails with HTTP 400 mentioning
  `x-amz-checksum-…`**, the checksum setting needs revisiting — `RequestChecksumCalculation` is
  already `WHEN_REQUIRED`, so report the exact message rather than guessing.
- Pause mid-upload, resume, and confirm it continues rather than restarting. Then reload the
  page, reopen the draft, choose the same file and confirm the already-uploaded parts are skipped.
- Cancel an upload and confirm the draft is `Failed`, and that the bucket is not holding its
  parts (Backblaze shows unfinished large files in the bucket browser).
- Confirm the video becomes **Ready** without any manual step, and that its size in the storage
  bar matches the file.
- Confirm a draft is absent from the client list and returns 404 for client detail and playback,
  even with the ID. Anonymous API access returns 401; a client hitting an admin route gets 403.
- Preview as admin, publish, then watch as an approved client on a phone over Wi-Fi **and**
  mobile data. Seek forwards and backwards — that is the range-request path.
- Copy a playback URL and open it in a private window. **It will play.** Confirm that it stops
  working after `Video:S3:PlaybackMinutes`. This is the documented trade-off, not a bug; confirm
  it behaves as documented.
- Unpublish and confirm no new playback link is issued. An already-issued link keeps working
  until it expires.
- Delete a disposable test video. Confirm the object **and** its poster frame are gone from the
  bucket, and the row is gone from the list.
- Check the storage bar: upload until it passes 80% if practical, and confirm the warning shows
  and that an upload past the cap is refused with the Greek message rather than a generic error.
- Record the date, device, browser and outcome in docs/08.

## 7. Recovery and cost control

**Upload keeps failing at one part.** Almost always CORS or an expired URL. Check the console for
a CORS message first; if instead the PUT returns 403, the part URLs have outlived
`Video:S3:UploadMinutes` — press Συνέχεια, which asks the API for fresh ones.

**A draft is stuck `Uploading`.** Press Έλεγχος. It HEADs the object: if the object is there the
row becomes Ready, and if it is not the draft stays as it is so its parts survive. Use
Νέο ανέβασμα αρχείου to resume or replace.

**`VIDEO_UPLOAD_MISMATCH`.** The completed object was not the size or type declared. The API has
already deleted it and marked the draft Failed. Re-upload; if it recurs on the same file, the
file is the problem.

**Storage full.** Delete old workouts, or raise `Video:S3:StorageCapBytes` **only** after
checking Backblaze's own usage — the app's cap is a guard rail, not the bill.

**Unfinished large files in the bucket.** Cancelling or deleting a draft aborts its upload, but a
crash at the wrong moment can leave parts behind. Backblaze's bucket browser lists unfinished
large files and can remove them; B2 can also expire them automatically with a lifecycle rule.

**Rotating the key.** Create a new application key scoped to the same bucket, update
`Video__S3__AccessKeyId` and `Video__S3__SecretAccessKey` on Render and in user-secrets, redeploy,
then delete the old key. Confirm an upload, a playback and a delete afterwards. Playback links
issued under the old key keep working until they expire.

**Keep the original recordings.** A database backup does not contain the video files, and neither
does anything else this project owns. The bucket is the only copy unless the trainer keeps hers.

## 8. Migration and deployment

Follow docs/10-production.md. The additive `VideoObjectStorage` migration must be applied to the
Neon **production** branch with the **direct** connection string before the new code serves
traffic. Production startup performs neither migrations nor seeding. Check `/health` and a deep
link before enabling uploads.

Provider references:
[B2 S3-compatible API](https://www.backblaze.com/docs/cloud-storage-s3-compatible-api),
[supported S3 operations](https://www.backblaze.com/apidocs/introduction-to-the-s3-compatible-api),
[CORS rules](https://www.backblaze.com/docs/cloud-storage-cross-origin-resource-sharing-rules),
[pricing and free tier](https://www.backblaze.com/cloud-storage/pricing).
Dashboard labels change; verify the behaviour above rather than trusting a label.
