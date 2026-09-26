using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mya.Application.Abstractions.Persistence;
using Mya.Application.Abstractions.System;
using Mya.Application.Abstractions.Media;
using Mya.Application.Common.Results;
using Mya.Domain.Enums;
namespace Mya.Application.Features.Videos;
public sealed class VideoProcessingHandler(IAppDbContext db, IVideoStorage storage, VideoAccess access, IClock clock)
{
    public async Task<Result> WebhookAsync(byte[] body, string signature, string version, string algorithm, CancellationToken ct)
    {
        if (!storage.VerifyWebhook(body, signature, version, algorithm)) return Result.Failure(new Error("WEBHOOK_INVALID", "Invalid webhook signature", ResultStatus.Unauthorized));
        try
        {
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("VideoLibraryId", out var library) || library.ValueKind != JsonValueKind.Number || !library.TryGetInt64(out var libraryId) || !storage.OwnsLibrary(libraryId)
                || !root.TryGetProperty("VideoGuid", out var video) || video.ValueKind != JsonValueKind.String || !video.TryGetGuid(out var guid)) return Result.Failure(VideoRules.Invalid);
            var row = await db.Videos.SingleOrDefaultAsync(v => v.ExternalId == guid.ToString("D"), ct);
            return row is null ? Result.Success() : await SynchronizeAsync(row.Id, ct);
        }
        catch (JsonException) { return Result.Failure(VideoRules.Invalid); }
    }
    public async Task<Result> RefreshAsync(Guid id, CancellationToken ct)
    {
        if (!await access.AllowedAsync(true, ct)) return Result.Failure(VideoRules.Forbidden);
        return await SynchronizeAsync(id, ct);
    }
    private async Task<Result> SynchronizeAsync(Guid id, CancellationToken ct)
    {
        var row = await db.Videos.SingleOrDefaultAsync(v => v.Id == id, ct);
        if (row is null) return Result.Failure(VideoRules.Missing);
        if (row.Status == VideoStatus.Deleting) return Result.Success();
        if (row.ExternalId is null) return Result.Failure(VideoRules.Invalid);
        if (!storage.IsConfigured) return Result.Failure(VideoRules.Unavailable);
        RemoteVideo remote;
        try { remote = await storage.GetAsync(row.ExternalId, ct); }
        catch (HttpRequestException) { return Result.Failure(VideoRules.Unavailable); }
        // Re-read authoritative provider state: delayed/replayed webhook payloads cannot regress it.
        row.Status = remote.Status switch { 3 => VideoStatus.Ready, 5 or 8 => VideoStatus.Failed, 6 => VideoStatus.Uploading, _ => VideoStatus.Processing };
        row.DurationSeconds = remote.DurationSeconds; row.ThumbnailUrl = remote.ThumbnailUrl;
        if (row.Status != VideoStatus.Ready) row.IsPublished = false;
        row.Revision = Guid.NewGuid(); row.UpdatedAtUtc = clock.UtcNow;
        try { await db.SaveChangesAsync(ct); return Result.Success(); }
        catch (DbUpdateConcurrencyException) { return Result.Failure(VideoRules.Conflict); }
    }
}
