# 11 — Video operations

Code is implemented; no live Bunny library has been configured or verified by the agent. This
guide is the remaining provider setup, not evidence of a completed deployment. Bunny may require
billing after its trial; the owner chooses and authorizes the account/plan.

## Configure a private library

1. Create a Bunny Stream library in the owner's account and select an appropriate storage region.
2. Enable embed/player token authentication. Disable direct play, MP4 fallback and exposed original
   downloads. Configure the Stream/CDN access settings so raw unsigned playlist/segment/original
   URLs cannot bypass the authenticated embed. Restrict allowed embedding domains to the production
   site (add a local origin only for deliberate development testing).
3. Save the library ID, write API key, read-only API key, embed token key and CDN hostname in
   user-secrets locally and environment variables on Render. Never paste keys into chat or docs.
4. Configure the webhook URL to the production origin plus /api/webhooks/video-ready. Enable signed
   webhooks using Bunny's versioned HMAC headers. The code verifies with the library read-only key.
5. Enable Video:Bunny:Enabled only after all settings are present. Incomplete enabled settings fail
   startup validation. Disabled video configuration leaves account management usable.

| Local key | Render environment key |
|---|---|
| Video:Bunny:Enabled | Video__Bunny__Enabled |
| Video:Bunny:LibraryId | Video__Bunny__LibraryId |
| Video:Bunny:ApiKey | Video__Bunny__ApiKey |
| Video:Bunny:ReadOnlyApiKey | Video__Bunny__ReadOnlyApiKey |
| Video:Bunny:TokenKey | Video__Bunny__TokenKey |
| Video:Bunny:CdnHost | Video__Bunny__CdnHost |

CdnHost is the library's b-cdn.net hostname without scheme/path. Do not place any provider key in
Angular environment files. The browser receives temporary signatures only. The write key is used
for management API calls and TUS authorization; the embed key signs playback; the read-only key
verifies webhook authenticity. They are distinct settings and not interchangeable.

Provider references: [TUS upload](https://bunny.net/docs/stream/tus-resumable-uploads),
[embed tokens](https://bunny.net/docs/stream/token-authentication),
[signed webhooks](https://bunny.net/docs/stream/webhooks),
[library security options](https://docs.bunny.net/reference/videolibrarypublic_update).
Dashboard labels can change; verify the behavior below instead of relying on labels alone.

## Migration and deployment

Follow docs/10-production.md. Apply all committed migrations, including VideoCatalogue, deliberately
using the Neon production direct connection. Back up first if production has acquired data. The
running app uses the pooled endpoint. Production startup performs neither migrations nor seeding.
Deploy the reviewed Docker image. Check /health and deep links before enabling video uploads.

## Live acceptance — mandatory before inviting real clients

- Upload a short phone recording through the admin form; verify real progress, pause/resume and
  successful processing. Include the trainer's usual iPhone/Android recording format.
- Observe a signed webhook, Ready status, duration and thumbnail. Repeat/delay an event and verify
  it does not regress current status or publish the video. Check manual Refresh as recovery.
- Confirm a draft is absent from the client list and returns 404 for client detail/playback, even
  when its ID is known. Confirm anonymous API access returns 401 and client admin access 403.
- Preview as admin, publish, then watch as an approved client on a phone over Wi-Fi and mobile data.
- Verify unsigned/expired embed URLs fail. Verify raw unsigned playlist, segment, MP4 and original
  URLs fail too. Do not call the deployment private until these provider-side negative tests pass.
- Unpublish and confirm no new playback link is issued. Existing issued links may remain valid for
  up to fifteen minutes, and an already started stream is not instantly revoked.
- Interrupt an upload and recover from the draft. Delete a disposable test video and verify both
  provider and database removal. Simulate provider unavailability and retry deletion after recovery.
- Inspect browser network requests for temporary signatures only, not provider API/token keys.
- Verify production forwarded headers and rate limits, real client invitation inbox flow, and
  trainer login on moveyourass.gr. Record date, browser/device and outcome in docs/08.

## Recovery and cost control

Failed upload with an external ID: choose the draft's new upload action and select the source file.
Processing stuck: press Check; inspect the provider dashboard and webhook delivery. Do not publish
until Ready. The app intentionally has no idle status poller that keeps Neon awake.

Uncertain create response: inspect Bunny for an orphan before deleting the failed draft or creating
another. A remote asset can exist despite a timed-out API response. Never assume a retry is free.
Deleting state: retry Delete after provider recovery; it stays hidden from clients in the meantime.
Conflict message: reload before editing again; another tab or webhook changed the revision.

Review provider storage/traffic usage and configure available account alerts. No spend limits or
alerts were configured by this agent. Keep original recordings separately; database backups alone
do not preserve hosted video files. Rotate compromised provider keys and update the matching app
settings; confirm uploads, playback and webhook verification after rotation.
