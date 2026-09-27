using Microsoft.EntityFrameworkCore;
using Mya.Application.Abstractions.Persistence;
using Mya.Application.Abstractions.System;
using Mya.Application.Abstractions.Media;
using Mya.Application.Common.Results;
using Mya.Domain.Enums;
namespace Mya.Application.Features.Videos;

/// <summary>
/// Reconciles a row with what the provider actually holds. Plain object storage has no webhook
/// and no processing pipeline (ADR-019), so this is a HEAD: the object either exists with the
/// expected size, or the row is not Ready.
/// </summary>
public sealed class VideoProcessingHandler(IAppDbContext db, IVideoStorage storage, VideoAccess access, IClock clock)
{
    public async Task<Result> RefreshAsync(Guid id, CancellationToken ct)
    {
        if (!await access.AllowedAsync(true, ct)) return Result.Failure(VideoRules.Forbidden);

        var row = await db.Videos.SingleOrDefaultAsync(v => v.Id == id, ct);
        if (row is null) return Result.Failure(VideoRules.Missing);
        if (row.Status == VideoStatus.Deleting) return Result.Success();
        if (row.ExternalId is null) return Result.Failure(VideoRules.Invalid);
        if (!storage.IsConfigured) return Result.Failure(VideoRules.Unavailable);

        StoredObject? stored;
        try { stored = await storage.HeadAsync(row.ExternalId, ct); }
        catch (VideoStorageException) { return Result.Failure(VideoRules.Unavailable); }

        if (stored is null)
        {
            // Nothing landed. An upload still in flight stays Uploading so its parts survive.
            if (row.Status != VideoStatus.Uploading)
            {
                row.Status = VideoStatus.Failed;
                row.IsPublished = false;
                Touch(row);
            }

            return await SaveAsync(ct);
        }

        row.SizeBytes = stored.SizeBytes;
        row.ContentType = stored.ContentType;
        row.UploadId = null;
        row.Status = VideoStatus.Ready;
        Touch(row);
        return await SaveAsync(ct);
    }

    private void Touch(Domain.Entities.Video row)
    {
        row.Revision = Guid.NewGuid();
        row.UpdatedAtUtc = clock.UtcNow;
    }

    private async Task<Result> SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); return Result.Success(); }
        catch (DbUpdateConcurrencyException) { return Result.Failure(VideoRules.Conflict); }
    }
}
