using System.Globalization;
using System.Text;
using FluentValidation;
using Mya.Application.Common.Results;
using Mya.Domain.Entities;
using Mya.Domain.Enums;
namespace Mya.Application.Features.Videos;
public sealed record VideoInput(string Title, string? Description, VideoAudience? Audience, VideoBodyArea? BodyArea, bool? RequiresEquipment, Guid[] TagIds, Guid? Revision);
public sealed class VideoInputValidator : AbstractValidator<VideoInput>
{
    public VideoInputValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Audience).NotNull().IsInEnum(); RuleFor(x => x.BodyArea).NotNull().IsInEnum();
        RuleFor(x => x.RequiresEquipment).NotNull();
        RuleFor(x => x.TagIds).NotNull().Must(x => x is not null && x.Length <= 20);
    }
}
public sealed class VideoQuery
{
    public VideoAudience? Audience { get; init; }
    public VideoBodyArea? BodyArea { get; init; }
    public bool? Equipment { get; init; }
    public Guid[] Tags { get; init; } = [];
    public string? Search { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 12;
}
public sealed class VideoQueryValidator : AbstractValidator<VideoQuery>
{
    public VideoQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 10000); RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(200); RuleFor(x => x.Tags).Must(x => x.Length <= 20);
        RuleFor(x => x.Audience).IsInEnum().When(x => x.Audience.HasValue);
        RuleFor(x => x.BodyArea).IsInEnum().When(x => x.BodyArea.HasValue);
    }
}
public sealed record TagInput(string Name);
public sealed class TagInputValidator : AbstractValidator<TagInput>
{
    public TagInputValidator() { RuleFor(x => x.Name).NotEmpty().MaximumLength(60); }
}
public sealed record TagDto(Guid Id, string Name, int UsageCount);
public sealed record VideoDto(Guid Id, string Title, string? Description, VideoAudience Audience, VideoBodyArea BodyArea,
    bool RequiresEquipment, VideoStatus Status, bool IsPublished, int SortOrder, int? DurationSeconds,
    string? ThumbnailUrl, Guid Revision, IReadOnlyList<TagDto> Tags);
public sealed record RevisionInput(Guid Revision);
public sealed record VideoOrder(Guid Id, int SortOrder, Guid Revision);
public static class VideoRules
{
    public static readonly Error Missing = new("VIDEO_NOT_FOUND", "Video not found", ResultStatus.NotFound);
    public static readonly Error Conflict = new("VIDEO_CONFLICT", "Video changed; reload before trying again", ResultStatus.Conflict);
    public static readonly Error Invalid = new("VIDEO_INVALID", "Invalid video request", ResultStatus.Invalid);
    public static readonly Error Unavailable = new("VIDEO_PROVIDER_UNAVAILABLE", "Video provider is not available", ResultStatus.Conflict);
    public static readonly Error Forbidden = new("FORBIDDEN", "Active account required", ResultStatus.Forbidden);
    public static VideoDto Map(Video v)
    {
        ArgumentNullException.ThrowIfNull(v);
        return new(v.Id, v.Title, v.Description, v.Audience, v.BodyArea, v.RequiresEquipment,
        v.Status, v.IsPublished, v.SortOrder, v.DurationSeconds, v.ThumbnailUrl, v.Revision,
        v.VideoTags.Select(t => new TagDto(t.TagId, t.Tag.Name, 0)).OrderBy(t => t.Name).ToList());
    }
    public static string NormalizeTag(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return string.Concat(name.Trim().Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }
    public static IQueryable<Video> Filter(IQueryable<Video> q, VideoQuery f)
    {
        ArgumentNullException.ThrowIfNull(q);
        ArgumentNullException.ThrowIfNull(f);
        if (f.Audience is VideoAudience.Male or VideoAudience.Female) q = q.Where(v => v.Audience == f.Audience || v.Audience == VideoAudience.Both);
        else if (f.Audience == VideoAudience.Both) q = q.Where(v => v.Audience == VideoAudience.Both);
        if (f.BodyArea.HasValue) q = q.Where(v => v.BodyArea == f.BodyArea);
        if (f.Equipment.HasValue) q = q.Where(v => v.RequiresEquipment == f.Equipment);
        if (f.Tags.Length > 0) q = q.Where(v => v.VideoTags.Any(t => f.Tags.Contains(t.TagId)));
        if (!string.IsNullOrWhiteSpace(f.Search)) { var term = f.Search.Trim().ToUpperInvariant(); q = q.Where(v => v.Title.ToUpper().Contains(term) || (v.Description != null && v.Description.ToUpper().Contains(term))); }
        return q;
    }
}
