using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mya.Application.Abstractions.Persistence;
using Mya.Application.Abstractions.System;
using Mya.Application.Abstractions.Media;
using Mya.Application.Common.Results;
using Mya.Domain.Entities;
using Mya.Domain.Enums;
namespace Mya.Application.Features.Videos;
public sealed record CreatedVideo(Guid Id, UploadTicket? Upload);

/// <summary>
/// Admin video management (ADR-019). Uploads go from the browser straight to object storage with
/// presigned part URLs; this handler decides whether an upload may start, issues those URLs, and
/// is the only thing that talks to the provider's multipart API.
/// </summary>
public sealed class VideoAdminHandler(IAppDbContext db, VideoAccess access, ICurrentUser current, IClock clock, IVideoStorage storage)
{
    /// <summary>A poster frame is a small JPEG. Anything larger is not the frame we asked for.</summary>
    private const long MaxThumbnailBytes = 2L * 1024 * 1024;

    public async Task<Result<CreatedVideo>> CreateAsync(VideoCreateInput input, string key, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!await access.AllowedAsync(true, ct)) return Result.Failure<CreatedVideo>(VideoRules.Forbidden);
        if (string.IsNullOrWhiteSpace(key) || key.Length > 100) return Result.Failure<CreatedVideo>(VideoRules.Invalid);
        if (!storage.IsConfigured) return Result.Failure<CreatedVideo>(VideoRules.Unavailable);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input.Video))));
        var old = await db.Videos.SingleOrDefaultAsync(v => v.CreatedByUserId == current.UserId && v.CreationKey == key, ct);
        if (old is not null)
        {
            if (old.CreationHash != hash) return Result.Failure<CreatedVideo>(VideoRules.Conflict);
            // Replay of the same create: hand back a ticket for the upload already in flight.
            return old.Status == VideoStatus.Uploading && old.ExternalId is not null && old.UploadId is not null
                ? Result.Success(new CreatedVideo(old.Id, await TicketAsync(old, ct)))
                : Result.Success(new CreatedVideo(old.Id, null));
        }
        var allowed = await CheckFileAsync(input.File, null, ct);
        if (allowed is not null) return Result.Failure<CreatedVideo>(allowed);
        if (!await TagsExistAsync(input.Video.TagIds, ct)) return Result.Failure<CreatedVideo>(VideoRules.Invalid);
        var video = new Video
        {
            Id = Guid.NewGuid(), CreatedByUserId = current.UserId!, CreatedAtUtc = clock.UtcNow,
            CreationKey = key, CreationHash = hash, Status = VideoStatus.Uploading,
            ContentType = input.File.ContentType, SizeBytes = input.File.SizeBytes,
        };
        video.ExternalId = VideoRules.ObjectKey(video.Id, video.ContentType!);
        Apply(video, input.Video);
        video.SortOrder = (await db.Videos.MaxAsync(v => (int?)v.SortOrder, ct) ?? -1) + 1;
        db.Videos.Add(video);
        // Reserve the key before contacting the provider. A racing request cannot start a second upload.
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Result.Failure<CreatedVideo>(VideoRules.Conflict); }
        var started = await StartAsync(video, ct);
        return started.IsFailure
            ? Result.Failure<CreatedVideo>(started.Error!)
            : Result.Success(new CreatedVideo(video.Id, started.Value));
    }

    /// <summary>
    /// Issues upload URLs for an existing draft. The same file resumes the upload already in
    /// flight; a different file abandons it and starts a new one.
    /// </summary>
    public async Task<Result<UploadTicket>> UploadAsync(Guid id, UploadRequest file, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (!await access.AllowedAsync(true, ct)) return Result.Failure<UploadTicket>(VideoRules.Forbidden);
        var v = await db.Videos.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (v is null) return Result.Failure<UploadTicket>(VideoRules.Missing);
        if (!storage.IsConfigured) return Result.Failure<UploadTicket>(VideoRules.Unavailable);
        if (v.Status is not (VideoStatus.Uploading or VideoStatus.Failed)) return Result.Failure<UploadTicket>(VideoRules.Invalid);

        var sameFile = v.UploadId is not null && v.ExternalId is not null
            && v.ContentType == file.ContentType && v.SizeBytes == file.SizeBytes;
        if (sameFile)
        {
            v.Status = VideoStatus.Uploading; v.IsPublished = false; v.Revision = Guid.NewGuid();
            var resumed = await SaveAsync(ct);
            if (resumed.IsFailure) return Result.Failure<UploadTicket>(resumed.Error!);
            return Result.Success(await TicketAsync(v, ct));
        }

        var allowed = await CheckFileAsync(file, v.Id, ct);
        if (allowed is not null) return Result.Failure<UploadTicket>(allowed);
        await DiscardUploadAsync(v, ct);
        v.ContentType = file.ContentType; v.SizeBytes = file.SizeBytes;
        v.ExternalId = VideoRules.ObjectKey(v.Id, v.ContentType!);
        v.Status = VideoStatus.Uploading; v.IsPublished = false;
        var started = await StartAsync(v, ct);
        return started.IsFailure ? Result.Failure<UploadTicket>(started.Error!) : Result.Success(started.Value);
    }

    /// <summary>
    /// Completes the multipart upload and verifies the result. There is no provider webhook
    /// (ADR-019): the object is read back with HEAD, and only a matching object becomes Ready.
    /// </summary>
    public async Task<Result> CompleteAsync(Guid id, CompleteUploadInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!await access.AllowedAsync(true, ct)) return Result.Failure(VideoRules.Forbidden);
        var v = await db.Videos.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (v is null) return Result.Failure(VideoRules.Missing);
        if (!storage.IsConfigured) return Result.Failure(VideoRules.Unavailable);
        if (v.Status != VideoStatus.Uploading || v.ExternalId is null || v.UploadId is null) return Result.Failure(VideoRules.Invalid);

        StoredObject stored;
        try { stored = await storage.CompleteUploadAsync(v.ExternalId, v.UploadId, ct); }
        catch (VideoStorageException) { return Result.Failure(VideoRules.Unavailable); }

        if (stored.SizeBytes != v.SizeBytes || !storage.AllowedContentTypes.Contains(stored.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            // Not what was authorised. Remove it rather than leave unverified bytes in the bucket.
            try { await storage.DeleteAsync(v.ExternalId, ct); } catch (VideoStorageException) { /* retried by Delete */ }
            v.Status = VideoStatus.Failed; v.UploadId = null; v.IsPublished = false;
            v.Revision = Guid.NewGuid(); v.UpdatedAtUtc = clock.UtcNow;
            await SaveAsync(ct);
            return Result.Failure(VideoRules.UploadMismatch);
        }

        v.SizeBytes = stored.SizeBytes; v.ContentType = stored.ContentType; v.UploadId = null;
        v.DurationSeconds = input.DurationSeconds;
        var poster = input.ThumbnailUploaded ? await VerifyThumbnailAsync(v.Id, ct) : null;
        v.ThumbnailObjectKey = poster?.Key;
        v.ThumbnailSizeBytes = poster?.SizeBytes;
        v.Status = VideoStatus.Ready; v.Revision = Guid.NewGuid(); v.UpdatedAtUtc = clock.UtcNow;
        return await SaveAsync(ct);
    }

    /// <summary>
    /// Issues a presigned PUT for a cover image. Type, size and the storage cap are decided here,
    /// before any URL exists, exactly as they are for the recording itself.
    /// </summary>
    public async Task<Result<CoverTicket>> CoverUploadAsync(Guid id, CoverRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!await access.AllowedAsync(true, ct)) return Result.Failure<CoverTicket>(VideoRules.Forbidden);
        var v = await db.Videos.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (v is null) return Result.Failure<CoverTicket>(VideoRules.Missing);
        if (!storage.IsConfigured) return Result.Failure<CoverTicket>(VideoRules.Unavailable);
        if (v.Status == VideoStatus.Deleting) return Result.Failure<CoverTicket>(VideoRules.Conflict);

        if (request.ContentType is null || !VideoRules.CoverContentTypes.Contains(request.ContentType, StringComparer.OrdinalIgnoreCase))
            return Result.Failure<CoverTicket>(VideoRules.CoverType);
        if (request.SizeBytes <= 0 || request.SizeBytes > VideoRules.MaxCoverBytes)
            return Result.Failure<CoverTicket>(VideoRules.CoverTooLarge);

        // The cover replaces whatever is there, so this video's own current cover is excluded.
        var used = await UsedBytesAsync(v.Id, ct) + (v.SizeBytes ?? 0) + (v.ThumbnailSizeBytes ?? 0);
        if (used + request.SizeBytes > storage.StorageCapBytes)
            return Result.Failure<CoverTicket>(VideoRules.StorageFull);

        var key = VideoRules.CoverKey(v.Id, request.ContentType);
        try { return Result.Success(new CoverTicket(key, storage.PresignPut(key, storage.UploadLifetime))); }
        catch (VideoStorageException) { return Result.Failure<CoverTicket>(VideoRules.Unavailable); }
    }

    /// <summary>
    /// Adopts an uploaded cover once the provider confirms it, and deletes the one it replaces.
    /// The key has to be one this video could have been issued, so a caller cannot point the row
    /// at an arbitrary object already in the bucket.
    /// </summary>
    public async Task<Result> CoverConfirmAsync(Guid id, CoverConfirm input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!await access.AllowedAsync(true, ct)) return Result.Failure(VideoRules.Forbidden);
        var v = await db.Videos.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (v is null) return Result.Failure(VideoRules.Missing);
        if (!storage.IsConfigured) return Result.Failure(VideoRules.Unavailable);
        if (v.Status == VideoStatus.Deleting) return Result.Failure(VideoRules.Conflict);
        if (!input.ObjectKey.StartsWith($"videos/{v.Id:D}-cover-", StringComparison.Ordinal))
            return Result.Failure(VideoRules.Invalid);

        StoredObject? stored;
        try { stored = await storage.HeadAsync(input.ObjectKey, ct); }
        catch (VideoStorageException) { return Result.Failure(VideoRules.Unavailable); }

        if (stored is null || stored.SizeBytes <= 0 || stored.SizeBytes > VideoRules.MaxCoverBytes)
            return Result.Failure(VideoRules.CoverTooLarge);
        if (!VideoRules.CoverContentTypes.Contains(stored.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            await ForgetObjectAsync(input.ObjectKey, ct);
            return Result.Failure(VideoRules.CoverType);
        }

        var replaced = v.CoverObjectKey;
        v.CoverObjectKey = input.ObjectKey;
        v.CoverSizeBytes = stored.SizeBytes;
        v.Revision = Guid.NewGuid(); v.UpdatedAtUtc = clock.UtcNow;
        var saved = await SaveAsync(ct);
        if (saved.IsFailure) return saved;

        // Only once the row points at the new one: a failure here costs an orphan object in the
        // bucket, not a video whose cover has vanished.
        if (replaced is not null) await ForgetObjectAsync(replaced, ct);
        return Result.Success();
    }

    /// <summary>Removes the cover, falling back to the captured frame and then the placeholder.</summary>
    public async Task<Result> CoverRemoveAsync(Guid id, CancellationToken ct)
    {
        if (!await access.AllowedAsync(true, ct)) return Result.Failure(VideoRules.Forbidden);
        var v = await db.Videos.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (v is null) return Result.Failure(VideoRules.Missing);
        if (v.CoverObjectKey is null) return Result.Success();

        var removed = v.CoverObjectKey;
        v.CoverObjectKey = null; v.CoverSizeBytes = null;
        v.Revision = Guid.NewGuid(); v.UpdatedAtUtc = clock.UtcNow;
        var saved = await SaveAsync(ct);
        if (saved.IsFailure) return saved;

        await ForgetObjectAsync(removed, ct);
        return Result.Success();
    }

    /// <summary>Best-effort delete of an object the database no longer references.</summary>
    private async Task ForgetObjectAsync(string objectKey, CancellationToken ct)
    {
        try { await storage.DeleteAsync(objectKey, ct); }
        catch (VideoStorageException) { /* Orphan in the bucket; the row is already correct. */ }
    }

    /// <summary>Abandons an upload in flight so its parts stop occupying the storage allowance.</summary>
    public async Task<Result> AbortAsync(Guid id, CancellationToken ct)
    {
        if (!await access.AllowedAsync(true, ct)) return Result.Failure(VideoRules.Forbidden);
        var v = await db.Videos.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (v is null) return Result.Failure(VideoRules.Missing);
        if (v.Status != VideoStatus.Uploading) return Result.Failure(VideoRules.Invalid);
        try { await DiscardUploadAsync(v, ct); }
        catch (VideoStorageException) { return Result.Failure(VideoRules.Unavailable); }
        v.Status = VideoStatus.Failed; v.IsPublished = false;
        v.Revision = Guid.NewGuid(); v.UpdatedAtUtc = clock.UtcNow;
        return await SaveAsync(ct);
    }

    public async Task<Result> UpdateAsync(Guid id, VideoInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!await access.AllowedAsync(true, ct)) return Result.Failure(VideoRules.Forbidden);
        var v = await db.Videos.Include(x => x.VideoTags).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (v is null) return Result.Failure(VideoRules.Missing);
        if (input.Revision != v.Revision || v.Status == VideoStatus.Deleting) return Result.Failure(VideoRules.Conflict);
        if (!await TagsExistAsync(input.TagIds, ct)) return Result.Failure(VideoRules.Invalid);
        Apply(v, input); v.UpdatedAtUtc = clock.UtcNow; v.Revision = Guid.NewGuid();
        return await SaveAsync(ct);
    }
    public async Task<Result> PublishAsync(Guid id, RevisionInput input, bool publish, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!await access.AllowedAsync(true, ct)) return Result.Failure(VideoRules.Forbidden);
        var v = await db.Videos.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (v is null) return Result.Failure(VideoRules.Missing);
        if (v.Revision != input.Revision || v.Status == VideoStatus.Deleting) return Result.Failure(VideoRules.Conflict);
        if (publish && (v.Status != VideoStatus.Ready || v.ExternalId is null)) return Result.Failure(VideoRules.Invalid);
        v.IsPublished = publish; v.Revision = Guid.NewGuid(); v.UpdatedAtUtc = clock.UtcNow;
        return await SaveAsync(ct);
    }
    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct)
    {
        if (!await access.AllowedAsync(true, ct)) return Result.Failure(VideoRules.Forbidden);
        var v = await db.Videos.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (v is null) return Result.Success();
        if (v.ExternalId is not null && !storage.IsConfigured) return Result.Failure(VideoRules.Unavailable);
        v.Status = VideoStatus.Deleting; v.IsPublished = false; v.Revision = Guid.NewGuid();
        var saved = await SaveAsync(ct); if (saved.IsFailure) return saved;
        try
        {
            await DiscardUploadAsync(v, ct);
            if (v.ExternalId is not null) await storage.DeleteAsync(v.ExternalId, ct);
            if (v.ThumbnailObjectKey is not null) await storage.DeleteAsync(v.ThumbnailObjectKey, ct);
            if (v.CoverObjectKey is not null) await storage.DeleteAsync(v.CoverObjectKey, ct);
        }
        catch (VideoStorageException) { return Result.Failure(VideoRules.Unavailable); }
        db.Videos.Remove(v); return await SaveAsync(ct);
    }
    public async Task<Result> ReorderAsync(VideoOrder[] order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (!await access.AllowedAsync(true, ct)) return Result.Failure(VideoRules.Forbidden);
        if (order.Length is 0 or > 100 || order.Select(x => x.Id).Distinct().Count() != order.Length || order.Any(x => x.SortOrder < 0)) return Result.Failure(VideoRules.Invalid);
        var ids = order.Select(x => x.Id).ToArray(); var rows = await db.Videos.Where(v => ids.Contains(v.Id)).ToListAsync(ct);
        if (rows.Count != order.Length) return Result.Failure(VideoRules.Missing);
        foreach (var row in rows) { var requested = order.Single(x => x.Id == row.Id); if (requested.Revision != row.Revision) return Result.Failure(VideoRules.Conflict); row.SortOrder = requested.SortOrder; row.Revision = Guid.NewGuid(); }
        return await SaveAsync(ct);
    }
    public async Task<Result<TagDto>> AddTagAsync(TagInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!await access.AllowedAsync(true, ct)) return Result.Failure<TagDto>(VideoRules.Forbidden);
        var normalized = VideoRules.NormalizeTag(input.Name);
        var existing = await db.Tags.SingleOrDefaultAsync(t => t.NormalizedName == normalized, ct);
        if (existing is not null) return Result.Success(new TagDto(existing.Id, existing.Name, 0));
        var tag = new Tag { Id = Guid.NewGuid(), Name = input.Name.Trim(), NormalizedName = normalized, CreatedAtUtc = clock.UtcNow };
        db.Tags.Add(tag);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Result.Failure<TagDto>(VideoRules.Conflict); }
        return Result.Success(new TagDto(tag.Id, tag.Name, 0));
    }
    public async Task<Result> DeleteTagAsync(Guid id, CancellationToken ct)
    {
        if (!await access.AllowedAsync(true, ct)) return Result.Failure(VideoRules.Forbidden);
        var tag = await db.Tags.SingleOrDefaultAsync(t => t.Id == id, ct); if (tag is null) return Result.Success();
        if (await db.VideoTags.AnyAsync(t => t.TagId == id, ct)) return Result.Failure(VideoRules.Conflict);
        db.Tags.Remove(tag);
        try { await db.SaveChangesAsync(ct); return Result.Success(); } catch (DbUpdateException) { return Result.Failure(VideoRules.Conflict); }
    }

    /// <summary>
    /// Type, size and the storage cap, decided before a single presigned URL exists. The cap
    /// counts drafts too: their parts occupy provider storage until they complete or are aborted.
    /// </summary>
    private async Task<Error?> CheckFileAsync(UploadRequest file, Guid? excluding, CancellationToken ct)
    {
        if (file.ContentType is null || !storage.AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            return VideoRules.FileType;
        if (file.SizeBytes <= 0 || file.SizeBytes > storage.MaxFileBytes) return VideoRules.FileTooLarge;
        var used = await UsedBytesAsync(excluding, ct);
        return used + file.SizeBytes > storage.StorageCapBytes ? VideoRules.StorageFull : null;
    }

    /// <summary>Bytes counted against the cap, optionally ignoring one row that is being replaced.</summary>
    public Task<long> UsedBytesAsync(Guid? excluding, CancellationToken ct) =>
        db.Videos.Where(v => excluding == null || v.Id != excluding)
            .SumAsync(v => (v.SizeBytes ?? 0L) + (v.ThumbnailSizeBytes ?? 0L) + (v.CoverSizeBytes ?? 0L), ct);

    private async Task<Result<UploadTicket>> StartAsync(Video video, CancellationToken ct)
    {
        try
        {
            var session = await storage.BeginUploadAsync(video.ExternalId!, video.ContentType!, video.SizeBytes!.Value, ct);
            video.UploadId = session.UploadId;
            video.Revision = Guid.NewGuid();
            await db.SaveChangesAsync(ct);
            return Result.Success(new UploadTicket(session.PartSizeBytes, session.PartCount,
                storage.PresignPartUrls(session.ObjectKey, session.UploadId, 1, session.PartCount),
                [], storage.PresignPut(VideoRules.ThumbnailKey(video.Id), storage.UploadLifetime)));
        }
        catch (VideoStorageException)
        {
            video.Status = VideoStatus.Failed; video.UploadId = null;
            await db.SaveChangesAsync(ct);
            return Result.Failure<UploadTicket>(VideoRules.Unavailable);
        }
    }

    private async Task<UploadTicket> TicketAsync(Video video, CancellationToken ct)
    {
        var parts = VideoStorageMath.PartCount(video.SizeBytes!.Value, storage.PartSizeBytes);
        IReadOnlyList<int> uploaded;
        try { uploaded = await storage.ListUploadedPartsAsync(video.ExternalId!, video.UploadId!, ct); }
        catch (VideoStorageException) { uploaded = []; }
        return new UploadTicket(storage.PartSizeBytes, parts,
            storage.PresignPartUrls(video.ExternalId!, video.UploadId!, 1, parts), uploaded,
            storage.PresignPut(VideoRules.ThumbnailKey(video.Id), storage.UploadLifetime));
    }

    /// <summary>A poster frame only counts once the provider confirms a plausible object.</summary>
    private async Task<(string Key, long SizeBytes)?> VerifyThumbnailAsync(Guid id, CancellationToken ct)
    {
        var key = VideoRules.ThumbnailKey(id);
        try
        {
            var head = await storage.HeadAsync(key, ct);
            return head is { SizeBytes: > 0 and <= MaxThumbnailBytes } ? (key, head.SizeBytes) : null;
        }
        catch (VideoStorageException) { return null; }
    }

    private async Task DiscardUploadAsync(Video video, CancellationToken ct)
    {
        if (video.ExternalId is null || video.UploadId is null) return;
        await storage.AbortUploadAsync(video.ExternalId, video.UploadId, ct);
        video.UploadId = null;
    }

    private async Task<bool> TagsExistAsync(Guid[] ids, CancellationToken ct) => await db.Tags.CountAsync(t => ids.Contains(t.Id), ct) == ids.Distinct().Count();
    private static void Apply(Video v, VideoInput input)
    {
        v.Title = input.Title.Trim(); v.Description = input.Description?.Trim(); v.Audience = input.Audience!.Value;
        v.BodyArea = input.BodyArea!.Value; v.RequiresEquipment = input.RequiresEquipment!.Value;
        foreach (var link in v.VideoTags.Where(t => !input.TagIds.Contains(t.TagId)).ToList()) v.VideoTags.Remove(link);
        foreach (var id in input.TagIds.Distinct().Where(id => v.VideoTags.All(t => t.TagId != id))) v.VideoTags.Add(new VideoTag { VideoId = v.Id, TagId = id });
    }
    private async Task<Result> SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); return Result.Success(); }
        catch (DbUpdateConcurrencyException) { return Result.Failure(VideoRules.Conflict); }
    }
}
