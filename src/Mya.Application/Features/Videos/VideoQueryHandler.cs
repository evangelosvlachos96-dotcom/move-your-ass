using Microsoft.EntityFrameworkCore;
using Mya.Application.Abstractions.Persistence;
using Mya.Application.Abstractions.Media;
using Mya.Application.Common.Paging;
using Mya.Application.Common.Results;
using Mya.Domain.Entities;
using Mya.Domain.Enums;
namespace Mya.Application.Features.Videos;
public sealed record VideoSummary(int Total, int Published, int Processing, int Failed, bool ProviderConfigured);
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
        return Result.Success(new PagedResult<VideoDto>(rows.Select(VideoRules.Map).ToList(), query.Page, query.PageSize, count));
    }
    public async Task<Result<VideoDto>> DetailAsync(Guid id, bool admin, CancellationToken ct)
    {
        if (!await access.AllowedAsync(admin, ct)) return Result.Failure<VideoDto>(VideoRules.Forbidden);
        var row = await Visible(admin).Include(v => v.VideoTags).ThenInclude(t => t.Tag).SingleOrDefaultAsync(v => v.Id == id, ct);
        return row is null ? Result.Failure<VideoDto>(VideoRules.Missing) : Result.Success(VideoRules.Map(row));
    }
    public async Task<Result<PlaybackLink>> PlaybackAsync(Guid id, bool admin, CancellationToken ct)
    {
        if (!await access.AllowedAsync(admin, ct)) return Result.Failure<PlaybackLink>(VideoRules.Forbidden);
        var row = await Visible(admin).SingleOrDefaultAsync(v => v.Id == id, ct);
        if (row is null) return Result.Failure<PlaybackLink>(VideoRules.Missing);
        if (row.Status != VideoStatus.Ready || row.ExternalId is null) return Result.Failure<PlaybackLink>(VideoRules.Invalid);
        return storage.IsConfigured ? Result.Success(storage.Playback(row.ExternalId)) : Result.Failure<PlaybackLink>(VideoRules.Unavailable);
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
        return Result.Success(new VideoSummary(await db.Videos.CountAsync(ct), await db.Videos.CountAsync(v => v.IsPublished && v.Status == VideoStatus.Ready, ct),
            await db.Videos.CountAsync(v => v.Status == VideoStatus.Uploading || v.Status == VideoStatus.Processing, ct), await db.Videos.CountAsync(v => v.Status == VideoStatus.Failed, ct), storage.IsConfigured));
    }
}
