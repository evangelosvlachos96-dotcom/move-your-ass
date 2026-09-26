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
public sealed record CreatedVideo(Guid Id, UploadCredentials? Upload);
public sealed class VideoAdminHandler(IAppDbContext db, VideoAccess access, ICurrentUser current, IClock clock, IVideoStorage storage)
{
    public async Task<Result<CreatedVideo>> CreateAsync(VideoInput input, string key, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!await access.AllowedAsync(true, ct)) return Result.Failure<CreatedVideo>(VideoRules.Forbidden);
        if (string.IsNullOrWhiteSpace(key) || key.Length > 100) return Result.Failure<CreatedVideo>(VideoRules.Invalid);
        if (!storage.IsConfigured) return Result.Failure<CreatedVideo>(VideoRules.Unavailable);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input))));
        var old = await db.Videos.SingleOrDefaultAsync(v => v.CreatedByUserId == current.UserId && v.CreationKey == key, ct);
        if (old is not null)
        {
            if (old.CreationHash != hash) return Result.Failure<CreatedVideo>(VideoRules.Conflict);
            return Result.Success(new CreatedVideo(old.Id, old.ExternalId is null || old.Status != VideoStatus.Uploading ? null : storage.Upload(old.ExternalId)));
        }
        if (!await TagsExistAsync(input.TagIds, ct)) return Result.Failure<CreatedVideo>(VideoRules.Invalid);
        var video = new Video { Id = Guid.NewGuid(), CreatedByUserId = current.UserId!, CreatedAtUtc = clock.UtcNow, CreationKey = key, CreationHash = hash };
        Apply(video, input);
        video.SortOrder = (await db.Videos.MaxAsync(v => (int?)v.SortOrder, ct) ?? -1) + 1;
        db.Videos.Add(video);
        // Reserve the key before contacting the provider. A racing request cannot create a second asset.
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Result.Failure<CreatedVideo>(VideoRules.Conflict); }
        try
        {
            video.ExternalId = await storage.CreateAsync(video.Title, ct);
            await db.SaveChangesAsync(ct);
            return Result.Success(new CreatedVideo(video.Id, storage.Upload(video.ExternalId)));
        }
        catch (HttpRequestException)
        {
            video.Status = VideoStatus.Failed; await db.SaveChangesAsync(ct);
            return Result.Failure<CreatedVideo>(VideoRules.Unavailable);
        }
    }
    public async Task<Result<UploadCredentials>> UploadAsync(Guid id, CancellationToken ct)
    {
        if (!await access.AllowedAsync(true, ct)) return Result.Failure<UploadCredentials>(VideoRules.Forbidden);
        var v = await db.Videos.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (v is null) return Result.Failure<UploadCredentials>(VideoRules.Missing);
        if (!storage.IsConfigured) return Result.Failure<UploadCredentials>(VideoRules.Unavailable);
        if (v.ExternalId is null || v.Status is not (VideoStatus.Uploading or VideoStatus.Failed)) return Result.Failure<UploadCredentials>(VideoRules.Invalid);
        v.Status = VideoStatus.Uploading; v.IsPublished = false; v.Revision = Guid.NewGuid();
        var saved = await SaveAsync(ct);
        return saved.IsFailure ? Result.Failure<UploadCredentials>(saved.Error!) : Result.Success(storage.Upload(v.ExternalId));
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
        try { if (v.ExternalId is not null) await storage.DeleteAsync(v.ExternalId, ct); }
        catch (HttpRequestException) { return Result.Failure(VideoRules.Unavailable); }
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
