using Microsoft.EntityFrameworkCore;
using Mya.Application.Abstractions.Persistence;
using Mya.Application.Abstractions.Media;
using Mya.Application.Common.Paging;
using Mya.Application.Common.Results;
using Mya.Domain.Entities;
using Mya.Domain.Enums;
namespace Mya.Application.Features.Videos;

/// <summary>Storage usage against the cap, so the admin can see the allowance before it runs out.</summary>
public sealed record StorageUsage(long UsedBytes, long CapBytes, long MaxFileBytes);

public sealed record VideoSummary(int Total, int Published, int Processing, int Failed, bool ProviderConfigured, StorageUsage Storage);

public sealed class VideoQueryHandler(IAppDbContext db, VideoAccess access, IVideoStorage storage)
{
    private IQueryable<Video> Visible(bool admin) => db.Videos.AsNoTracking().Where(v => admin || (v.IsPublished && v.Status == VideoStatus.Ready));
    public async Task<Result<PagedResult<VideoDto>>> ListAsync(VideoQuery query, bool admin, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!await access.AllowedAsync(admin, ct)) return Result.Failure<PagedResult<VideoDto>>(VideoRules.Forbidden);
        var filtered = VideoRules.Filter(Visible(admin), query);
        var count = await filtered.CountAsync(ct);
        var rows = await filtered.Include(v => v.VideoTags).ThenInclude(t => t.Tag).OrderBy(v => v.SortOrder)
            .ThenByDescending(v => v.CreatedAtUtc).ThenBy(v => v.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return Result.Success(new PagedResult<VideoDto>(rows.Select(v => VideoRules.Map(v, Poster)).ToList(), query.Page, query.PageSize, count));
    }
    public async Task<Result<VideoDto>> DetailAsync(Guid id, bool admin, CancellationToken ct)
    {
        if (!await access.AllowedAsync(admin, ct)) return Result.Failure<VideoDto>(VideoRules.Forbidden);
        var row = await Visible(admin).Include(v => v.VideoTags).ThenInclude(t => t.Tag).SingleOrDefaultAsync(v => v.Id == id, ct);
        return row is null ? Result.Failure<VideoDto>(VideoRules.Missing) : Result.Success(VideoRules.Map(row, Poster));
    }

    /// <summary>
    /// A short-lived presigned GET, played in a native video element. The link works for anyone
    /// holding it until it expires; that is an accepted trade-off, recorded in ADR-019.
    /// </summary>
    public async Task<Result<PlaybackLink>> PlaybackAsync(Guid id, bool admin, CancellationToken ct)
    {
        if (!await access.AllowedAsync(admin, ct)) return Result.Failure<PlaybackLink>(VideoRules.Forbidden);
        var row = await Visible(admin).SingleOrDefaultAsync(v => v.Id == id, ct);
        if (row is null) return Result.Failure<PlaybackLink>(VideoRules.Missing);
        if (row.Status != VideoStatus.Ready || row.ExternalId is null) return Result.Failure<PlaybackLink>(VideoRules.Invalid);
        if (!storage.IsConfigured) return Result.Failure<PlaybackLink>(VideoRules.Unavailable);
        try { return Result.Success(storage.PresignGet(row.ExternalId, storage.PlaybackLifetime)); }
        catch (VideoStorageException) { return Result.Failure<PlaybackLink>(VideoRules.Unavailable); }
    }

    /// <summary>
    /// A short-lived URL for a stored poster frame, or null when there is none and the card falls
    /// back to the branded placeholder. Signed locally, so presigning a page of cards is free.
    /// </summary>
    private string? Poster(string key)
    {
        if (!storage.IsConfigured) return null;
        try { return storage.PresignGet(key, storage.PlaybackLifetime).Url; }
        catch (VideoStorageException) { return null; }
    }

    public async Task<Result<IReadOnlyList<TagDto>>> TagsAsync(bool admin, CancellationToken ct)
    {
        if (!await access.AllowedAsync(admin, ct)) return Result.Failure<IReadOnlyList<TagDto>>(VideoRules.Forbidden);
        var tags = await db.Tags.AsNoTracking().Where(t => admin || t.VideoTags.Any(v => v.Video.IsPublished && v.Video.Status == VideoStatus.Ready))
            .OrderBy(t => t.Name).Select(t => new TagDto(t.Id, t.Name, t.VideoTags.Count(v => admin || (v.Video.IsPublished && v.Video.Status == VideoStatus.Ready)))).ToListAsync(ct);
        return Result.Success<IReadOnlyList<TagDto>>(tags);
    }
    public async Task<Result<VideoSummary>> SummaryAsync(CancellationToken ct)
    {
        if (!await access.AllowedAsync(true, ct)) return Result.Failure<VideoSummary>(VideoRules.Forbidden);
        return Result.Success(new VideoSummary(
            await db.Videos.CountAsync(ct),
            await db.Videos.CountAsync(v => v.IsPublished && v.Status == VideoStatus.Ready, ct),
            await db.Videos.CountAsync(v => v.Status == VideoStatus.Uploading || v.Status == VideoStatus.Processing, ct),
            await db.Videos.CountAsync(v => v.Status == VideoStatus.Failed, ct),
            storage.IsConfigured,
            new StorageUsage(
                // Recordings, poster frames, covers and the trainer photo all live in the same
                // bucket, so the bar has to count all of them or it understates what is used.
                await db.Videos.SumAsync(v => (v.SizeBytes ?? 0L) + (v.ThumbnailSizeBytes ?? 0L) + (v.CoverSizeBytes ?? 0L), ct)
                    + await db.SiteContent.SumAsync(x => x.PhotoSizeBytes ?? 0L, ct),
                storage.StorageCapBytes,
                storage.MaxFileBytes)));
    }
}
