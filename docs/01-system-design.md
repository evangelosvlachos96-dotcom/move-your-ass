# 01 — System Design

## 1. Context

```
                          ┌──────────────────────────┐
                          │        Admin             │
                          │  (trainer, 1 person)     │
                          └────────────┬─────────────┘
                                       │ approves users, uploads video,
                                       │ publishes slots, resolves requests
                                       ▼
   ┌────────────┐            ┌──────────────────────┐          ┌──────────────────┐
   │  Clients   │───────────▶│   Training Platform  │─────────▶│  Email provider  │
   │ (phone /   │            │                      │          │  (Brevo/Resend)  │
   │  tablet /  │◀───────────│                      │          └──────────────────┘
   │  desktop)  │            └──────────┬───────────┘
   └────────────┘                       │
                                        ▼
                              ┌──────────────────────┐
                              │  Blob Storage        │
                              │  (video content)     │
                              └──────────────────────┘
```

No SMS. No payment provider. No third-party identity provider.

## 2. Containers

```
┌─────────────────────────────────────────────────────────────────────┐
│  Browser                                                            │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │  Angular SPA                                                  │  │
│  │  core/http → ApiClient → interceptors (auth, error, loading)  │  │
│  │  hls.js player · Angular Material · signals                   │  │
│  └───────────────────────────────────────────────────────────────┘  │
└──────────┬──────────────────────────────────────┬───────────────────┘
           │ HTTPS / JSON                         │ HTTPS, direct
           │ Bearer JWT (15 min)                  │ (SAS-scoped, bypasses API)
           │ Refresh cookie (HttpOnly)            │
           ▼                                      ▼
┌──────────────────────────┐          ┌────────────────────────────────┐
│  Azure Static Web Apps   │          │  Azure Blob Storage            │
│  serves the SPA bundle   │          │  ┌──────────────────────────┐  │
└──────────────────────────┘          │  │ videos/   (private)      │  │
                                      │  │ thumbs/   (private)      │  │
┌──────────────────────────┐          │  └──────────────────────────┘  │
│  Azure App Service       │─────────▶│  issues user-delegation SAS    │
│  ASP.NET Core Web API    │          └────────────────────────────────┘
│                          │
│  ┌────────────────────┐  │          ┌────────────────────────────────┐
│  │ Outbox dispatcher  │──┼─────────▶│  Email provider (SMTP/API)     │
│  │ (BackgroundService)│  │          └────────────────────────────────┘
│  └────────────────────┘  │
└────────────┬─────────────┘
             │ EF Core
             ▼
┌──────────────────────────┐
│  Azure SQL Database      │
│  serverless, auto-pause  │
└──────────────────────────┘
```

**Key property:** video bytes never transit the API. The API only mints short-lived SAS URLs.
App Service F1 has 1 GB of RAM and would die streaming a 3 GB file.

## 3. Azure resource list

| Resource | SKU | Purpose | Cost |
|---|---|---|---|
| Resource Group | — | `rg-pt-prod` (+ `rg-pt-dev`) | — |
| Static Web App | Free | Angular bundle, SSL, custom domain | €0 |
| App Service Plan | F1 → B1 | API host | €0 → ~€12/mo |
| App Service | — | `app-pt-api-prod` | — |
| SQL Server (logical) | — | `sql-pt-prod` | €0 |
| SQL Database | Free offer (serverless GP) | 100k vCore-s, 32 GB | €0 |
| Storage Account | StorageV2, LRS, Hot | video + thumbnails | ~€0.02/GB/mo |
| Application Insights | Free ingest tier | logs, traces, failures | €0 up to 5 GB/mo |
| Key Vault | Standard | connection strings, JWT signing key | ~€0 (per-op) |

Set a **budget alert at €1** in Cost Management on day one. Azure has no hard spend cap.

## 4. Environments

| Env | API | DB | SPA | Notes |
|---|---|---|---|---|
| Local | Kestrel `https://localhost:7001` | SQL Server in Docker | `ng serve` :4200 | user-secrets for config |
| Dev (Azure) | F1 | free-offer DB #1 | SWA preview env | deployed from `develop` |
| Prod (Azure) | F1 → B1 | free-offer DB #2 | SWA production | deployed from `main`, tagged |

CORS: API allows exactly the SWA origin per environment. No wildcards, ever.

## 5. Video storage — the decision

High-quality video is the one part of this system where the naive approach actively hurts.

### The problem with a single MP4

One hour of 1080p at 8 Mbps is ~3.5 GB. Blob Storage does serve HTTP Range requests, so
`<video src="...mp4">` will seek correctly. But every viewer gets that one bitrate. A client on
a phone on mobile data will buffer constantly, and you will pay full egress for bytes they
never watch.

### Recommended: Blob + pre-encoded HLS ladder

Encode **before upload**, with ffmpeg, into three renditions plus a master playlist:

| Rendition | Resolution | Video bitrate | ~size / hour |
|---|---|---|---|
| High | 1920×1080 | 5.0 Mbps | 2.3 GB |
| Medium | 1280×720 | 2.8 Mbps | 1.3 GB |
| Low | 854×480 | 1.2 Mbps | 0.6 GB |

All H.264 High profile + AAC 128 kbps, 6-second segments, closed GOP.

```bash
ffmpeg -i source.mov \
  -filter_complex "[0:v]split=3[v1][v2][v3]; \
    [v1]scale=w=1920:h=1080[v1out];[v2]scale=w=1280:h=720[v2out];[v3]scale=w=854:h=480[v3out]" \
  -map "[v1out]" -c:v:0 libx264 -b:v:0 5000k -profile:v:0 high \
  -map "[v2out]" -c:v:1 libx264 -b:v:1 2800k -profile:v:1 high \
  -map "[v3out]" -c:v:2 libx264 -b:v:2 1200k -profile:v:2 main \
  -map a:0 -map a:0 -map a:0 -c:a aac -b:a 128k -ac 2 \
  -g 48 -keyint_min 48 -sc_threshold 0 \
  -f hls -hls_time 6 -hls_playlist_type vod -hls_flags independent_segments \
  -master_pl_name master.m3u8 \
  -var_stream_map "v:0,a:0 v:1,a:1 v:2,a:2" \
  out/stream_%v/playlist.m3u8
```

Upload the whole `out/` tree to `videos/{videoId}/`. Play with **hls.js** (native HLS on Safari/iOS).

**Access control.** A directory-scoped SAS covers every segment under the video's prefix:

```csharp
// one SAS, valid 4 hours, scoped to videos/{videoId}/
var sas = blobContainerClient.GenerateSasUri(new BlobSasBuilder {
    BlobContainerName = "videos",
    BlobName          = $"{videoId}/",
    Resource          = "d",                       // directory
    ExpiresOn         = DateTimeOffset.UtcNow.AddHours(4),
    Protocol          = SasProtocol.Https
});
```

In hls.js, append the token to every request:

```ts
new Hls({ xhrSetup: (xhr, url) => xhr.open('GET', `${url}?${sasToken}`, true) });
```

**Cost.** Storage is trivial (~€0.02/GB/month — 100 GB ≈ €2/month). Egress is the variable:
Azure includes **100 GB/month outbound free**, then ~€0.08/GB. With the HLS ladder, a typical
phone viewer pulls the 1.2 Mbps rendition, so an hour costs you ~0.6 GB instead of 3.5 GB.
That is a 5–6× reduction in your only unpredictable bill.

### Alternative: managed video platform

If you would rather not run ffmpeg and manage manifests:

| Provider | Roughly | Gives you |
|---|---|---|
| **Bunny Stream** | ~€0.01/GB stored, ~€0.005/GB delivered | transcoding, HLS, player, token auth, CDN |
| **Cloudflare Stream** | ~$5 per 1,000 min stored, ~$1 per 1,000 min delivered | same, predictable per-minute pricing |
| **Mux** | notably more | best API, analytics, overkill here |

Bunny is the cheapest credible option and would cost single-digit euros per month at your
scale. The tradeoff is a second vendor and video living outside Azure.

**Decision: start with Blob + HLS.** The `IVideoStorage` abstraction means swapping to Bunny
later is one implementation class, not a migration. Do *not* go looking for Azure Media
Services — it was retired in 2024.

## 6. Observability

Application Insights, free tier, with:

- Request/dependency telemetry (automatic)
- Custom events on the things that matter: `UserRegistered`, `UserApproved`, `LoginFailed`,
  `SlotBooked`, `SlotConflict`, `OutboxFailed`
- An alert on `SlotConflict` rate — a spike means the calendar polling interval is too slow
- An alert on App Service CPU quota at 80%

Serilog → App Insights sink. Structured logs, correlation ID per request, never log tokens,
passwords, or email bodies.

## 7. CI/CD

GitHub Actions, two workflows:

```
api.yml   on push to main  → dotnet test → publish → deploy to App Service (via publish profile)
web.yml   on push to main  → npm ci → lint → test → build → Static Web Apps deploy action
```

Migrations run on API startup **for dev only**. For prod, generate an idempotent script
(`dotnet ef migrations script --idempotent`) and apply it as a gated workflow step. Applying
migrations automatically from a free-tier app that can be killed mid-run is how you corrupt a
schema.
