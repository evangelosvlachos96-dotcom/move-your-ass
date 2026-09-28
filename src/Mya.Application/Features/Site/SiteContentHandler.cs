using Microsoft.EntityFrameworkCore;
using Mya.Application.Abstractions.Media;
using Mya.Application.Abstractions.Persistence;
using Mya.Application.Abstractions.System;
using Mya.Application.Common.Results;
using Mya.Application.Features.Videos;
using Mya.Domain.Entities;

namespace Mya.Application.Features.Site;

/// <summary>Where to PUT the trainer photo, and the key to send back once it is there.</summary>
public sealed record PhotoTicket(string ObjectKey, string UploadUrl);

public sealed record PhotoConfirm(string ObjectKey);

/// <summary>
/// The About page: one row, read by every approved user, written only by admins.
///
/// Reads deliberately do not create the row. A client opening the page on a fresh install would
/// otherwise be writing to the database, and every empty field is already rendered as "hidden".
/// </summary>
public sealed class SiteContentHandler(IAppDbContext db, VideoAccess access, IClock clock, IVideoStorage storage)
{
    public async Task<Result<AboutDto>> GetAsync(CancellationToken ct)
    {
        if (!await access.AllowedAsync(false, ct)) return Result.Failure<AboutDto>(SiteRules.Forbidden);

        var row = await db.SiteContent.AsNoTracking().SingleOrDefaultAsync(ct);
        return Result.Success(Map(row));
    }

    public async Task<Result> UpdateAsync(AboutInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!await access.AllowedAsync(true, ct)) return Result.Failure(SiteRules.Forbidden);

        var row = await LoadOrCreateAsync(ct);
        if (row.Revision != input.Revision) return Result.Failure(SiteRules.Conflict);

        row.TrainerName = SiteRules.Clean(input.TrainerName);
        row.Tagline = SiteRules.Clean(input.Tagline);
        row.AboutMarkdown = SiteRules.SanitizeMarkdown(input.AboutMarkdown);
        row.ContactEmail = SiteRules.Clean(input.ContactEmail);
        row.Phone = SiteRules.Clean(input.Phone);
        row.Instagram = SiteRules.Clean(input.Instagram);
        row.YouTube = SiteRules.Clean(input.YouTube);
        row.TikTok = SiteRules.Clean(input.TikTok);
        row.Facebook = SiteRules.Clean(input.Facebook);
        row.WhatsApp = SiteRules.Clean(input.WhatsApp);
        row.Website = SiteRules.Clean(input.Website);
        row.UpdatedAtUtc = clock.UtcNow;
        row.Revision = Guid.NewGuid();

        return await SaveAsync(ct);
    }

    public async Task<Result<PhotoTicket>> PhotoUploadAsync(CoverRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!await access.AllowedAsync(true, ct)) return Result.Failure<PhotoTicket>(SiteRules.Forbidden);
        if (!storage.IsConfigured) return Result.Failure<PhotoTicket>(VideoRules.Unavailable);

        if (request.ContentType is null || !SiteRules.PhotoContentTypes.Contains(request.ContentType, StringComparer.OrdinalIgnoreCase))
            return Result.Failure<PhotoTicket>(VideoRules.CoverType);
        if (request.SizeBytes <= 0 || request.SizeBytes > SiteRules.MaxPhotoBytes)
            return Result.Failure<PhotoTicket>(VideoRules.CoverTooLarge);

        var key = SiteRules.PhotoKey(request.ContentType);
        try { return Result.Success(new PhotoTicket(key, storage.PresignPut(key, storage.UploadLifetime))); }
        catch (VideoStorageException) { return Result.Failure<PhotoTicket>(VideoRules.Unavailable); }
    }

    public async Task<Result> PhotoConfirmAsync(PhotoConfirm input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!await access.AllowedAsync(true, ct)) return Result.Failure(SiteRules.Forbidden);
        if (!storage.IsConfigured) return Result.Failure(VideoRules.Unavailable);
        if (!input.ObjectKey.StartsWith("site/trainer-", StringComparison.Ordinal))
            return Result.Failure(SiteRules.Invalid);

        StoredObject? stored;
        try { stored = await storage.HeadAsync(input.ObjectKey, ct); }
        catch (VideoStorageException) { return Result.Failure(VideoRules.Unavailable); }

        if (stored is null || stored.SizeBytes <= 0 || stored.SizeBytes > SiteRules.MaxPhotoBytes)
            return Result.Failure(VideoRules.CoverTooLarge);
        if (!SiteRules.PhotoContentTypes.Contains(stored.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            await ForgetAsync(input.ObjectKey, ct);
            return Result.Failure(VideoRules.CoverType);
        }

        var row = await LoadOrCreateAsync(ct);
        var replaced = row.PhotoObjectKey;
        row.PhotoObjectKey = input.ObjectKey;
        row.PhotoSizeBytes = stored.SizeBytes;
        row.UpdatedAtUtc = clock.UtcNow;
        row.Revision = Guid.NewGuid();

        var saved = await SaveAsync(ct);
        if (saved.IsFailure) return saved;

        if (replaced is not null) await ForgetAsync(replaced, ct);
        return Result.Success();
    }

    public async Task<Result> PhotoRemoveAsync(CancellationToken ct)
    {
        if (!await access.AllowedAsync(true, ct)) return Result.Failure(SiteRules.Forbidden);

        var row = await db.SiteContent.SingleOrDefaultAsync(ct);
        if (row?.PhotoObjectKey is null) return Result.Success();

        var removed = row.PhotoObjectKey;
        row.PhotoObjectKey = null;
        row.PhotoSizeBytes = null;
        row.UpdatedAtUtc = clock.UtcNow;
        row.Revision = Guid.NewGuid();

        var saved = await SaveAsync(ct);
        if (saved.IsFailure) return saved;

        await ForgetAsync(removed, ct);
        return Result.Success();
    }

    /// <summary>Bytes the trainer photo occupies, counted against the same cap as the videos.</summary>
    public Task<long> PhotoBytesAsync(CancellationToken ct) =>
        db.SiteContent.SumAsync(x => x.PhotoSizeBytes ?? 0L, ct);

    private AboutDto Map(SiteContent? row)
    {
        if (row is null)
        {
            return new AboutDto(null, null, null, null, null, null, null, null, null, null, null, null, Guid.Empty);
        }

        return new AboutDto(
            Photo(row.PhotoObjectKey),
            row.TrainerName, row.Tagline, row.AboutMarkdown,
            row.ContactEmail, row.Phone, row.Instagram, row.YouTube,
            row.TikTok, row.Facebook, row.WhatsApp, row.Website,
            row.Revision);
    }

    private string? Photo(string? key)
    {
        if (key is null || !storage.IsConfigured) return null;
        try { return storage.PresignGet(key, storage.PlaybackLifetime).Url; }
        catch (VideoStorageException) { return null; }
    }

    /// <summary>
    /// The row is created on the first admin write, never on a read. Its id is fixed, so two
    /// concurrent first-saves collide on the primary key instead of both inserting.
    /// </summary>
    private async Task<SiteContent> LoadOrCreateAsync(CancellationToken ct)
    {
        var row = await db.SiteContent.SingleOrDefaultAsync(ct);
        if (row is not null) return row;

        row = new SiteContent { Id = SiteContent.SingletonId, Revision = Guid.Empty };
        db.SiteContent.Add(row);
        return row;
    }

    private async Task ForgetAsync(string objectKey, CancellationToken ct)
    {
        try { await storage.DeleteAsync(objectKey, ct); }
        catch (VideoStorageException) { /* Orphan in the bucket; the row is already correct. */ }
    }

    private async Task<Result> SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); return Result.Success(); }
        catch (DbUpdateConcurrencyException) { return Result.Failure(SiteRules.Conflict); }
        catch (DbUpdateException) { return Result.Failure(SiteRules.Conflict); }
    }
}
