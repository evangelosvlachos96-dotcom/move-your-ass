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

/// <summary>What the browser says it is about to upload. Checked against the provider's HEAD later.</summary>
public sealed record UploadRequest(string? ContentType, long SizeBytes);
public sealed class UploadRequestValidator : AbstractValidator<UploadRequest>
{
    public UploadRequestValidator()
    {
        RuleFor(x => x.ContentType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SizeBytes).GreaterThan(0);
    }
}

/// <summary>Create carries the file description so the cap and type are decided before any presign.</summary>
public sealed record VideoCreateInput(VideoInput Video, UploadRequest File);
public sealed class VideoCreateInputValidator : AbstractValidator<VideoCreateInput>
{
    public VideoCreateInputValidator()
    {
        RuleFor(x => x.Video).NotNull().SetValidator(new VideoInputValidator()!);
        RuleFor(x => x.File).NotNull().SetValidator(new UploadRequestValidator()!);
    }
}

/// <summary>A cover image the trainer picked, described before any URL is issued.</summary>
public sealed record CoverRequest(string? ContentType, long SizeBytes);
public sealed class CoverRequestValidator : AbstractValidator<CoverRequest>
{
    public CoverRequestValidator()
    {
        RuleFor(x => x.ContentType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SizeBytes).GreaterThan(0);
    }
}

/// <summary>Where to PUT the cover, and the key to send back once it is there.</summary>
public sealed record CoverTicket(string ObjectKey, string UploadUrl);

/// <summary>Confirms a cover the browser has finished uploading.</summary>
public sealed record CoverConfirm(string ObjectKey);
public sealed class CoverConfirmValidator : AbstractValidator<CoverConfirm>
{
    public CoverConfirmValidator() { RuleFor(x => x.ObjectKey).NotEmpty().MaximumLength(200); }
}

/// <summary>Reported by the browser after the last part, then verified server-side.</summary>
public sealed record CompleteUploadInput(bool ThumbnailUploaded, int? DurationSeconds);
public sealed class CompleteUploadInputValidator : AbstractValidator<CompleteUploadInput>
{
    public CompleteUploadInputValidator() { RuleFor(x => x.DurationSeconds).InclusiveBetween(0, 86400).When(x => x.DurationSeconds.HasValue); }
}

/// <summary>
/// Everything the browser needs to upload directly to the provider: the part size, one presigned
/// PUT per part, the parts the provider already holds (so a reload resumes rather than restarts)
/// and one presigned PUT for the optional poster frame. No provider credential is ever included.
/// </summary>
public sealed record UploadTicket(long PartSizeBytes, int PartCount, IReadOnlyList<Abstractions.Media.PartUrl> Parts,
    IReadOnlyList<int> UploadedParts, string ThumbnailUploadUrl);

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
    string? ThumbnailUrl, bool HasCustomCover, long? SizeBytes, DateTime CreatedAtUtc, Guid Revision, IReadOnlyList<TagDto> Tags);
public sealed record RevisionInput(Guid Revision);
public sealed record VideoOrder(Guid Id, int SortOrder, Guid Revision);
public static class VideoRules
{
    public static readonly Error Missing = new("VIDEO_NOT_FOUND", "Video not found", ResultStatus.NotFound);
    public static readonly Error Conflict = new("VIDEO_CONFLICT", "Video changed; reload before trying again", ResultStatus.Conflict);
    public static readonly Error Invalid = new("VIDEO_INVALID", "Invalid video request", ResultStatus.Invalid);
    public static readonly Error Unavailable = new("VIDEO_PROVIDER_UNAVAILABLE", "Video storage is not available", ResultStatus.Conflict);
    public static readonly Error Forbidden = new("FORBIDDEN", "Active account required", ResultStatus.Forbidden);

    /// <summary>The file is not one of the accepted recording formats.</summary>
    public static readonly Error FileType = new("VIDEO_FILE_TYPE", "Only MP4 and QuickTime recordings are accepted", ResultStatus.Invalid);

    /// <summary>The file is larger than Video:S3:MaxFileBytes.</summary>
    public static readonly Error FileTooLarge = new("VIDEO_FILE_TOO_LARGE", "The recording is larger than the allowed size", ResultStatus.Invalid);

    /// <summary>Accepting this file would take stored bytes past Video:S3:StorageCapBytes.</summary>
    public static readonly Error StorageFull = new("VIDEO_STORAGE_FULL", "The video storage allowance is full", ResultStatus.Conflict);

    /// <summary>The provider's object does not match what was declared, so the upload is not trusted.</summary>
    public static readonly Error UploadMismatch = new("VIDEO_UPLOAD_MISMATCH", "The uploaded file does not match what was expected", ResultStatus.Invalid);

    /// <summary>The cover image is not one of the accepted picture formats.</summary>
    public static readonly Error CoverType = new("VIDEO_COVER_TYPE", "Only JPG, PNG and WebP cover images are accepted", ResultStatus.Invalid);

    /// <summary>The cover image is larger than the allowed size.</summary>
    public static readonly Error CoverTooLarge = new("VIDEO_COVER_TOO_LARGE", "The cover image is larger than the allowed size", ResultStatus.Invalid);

    /// <summary>Picture formats accepted for a cover image.</summary>
    public static readonly string[] CoverContentTypes = ["image/jpeg", "image/png", "image/webp"];

    /// <summary>Largest cover the API will presign, before the browser resizes it. 5 MB.</summary>
    public const long MaxCoverBytes = 5L * 1024 * 1024;

    /// <summary>Object key for the original recording. Deterministic, so a retry cannot orphan bytes.</summary>
    public static string ObjectKey(Guid id, string contentType) =>
        $"videos/{id:D}{(contentType == "video/quicktime" ? ".mov" : ".mp4")}";

    /// <summary>Object key for the optional auto-captured poster frame of the same video.</summary>
    public static string ThumbnailKey(Guid id) => $"videos/{id:D}-poster.jpg";

    /// <summary>
    /// Object key for an uploaded cover. The random suffix matters: replacing a cover must not
    /// reuse a key, or a presigned URL already in a client's cache would still serve the old
    /// picture. It also makes the delete-then-replace order safe, since the two never collide.
    /// </summary>
    public static string CoverKey(Guid id, string contentType) =>
        $"videos/{id:D}-cover-{Guid.NewGuid():N}{CoverExtension(contentType)}";

    private static string CoverExtension(string contentType) => contentType switch
    {
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => ".jpg",
    };

    /// <summary>
    /// Maps a row for the wire. <paramref name="presign"/> turns the stored poster-frame key into
    /// a short-lived URL; a list presigns every card in one pass, because presigning is a local
    /// signature and costs no provider round trip.
    /// </summary>
    public static VideoDto Map(Video v, Func<string, string?>? presign = null)
    {
        ArgumentNullException.ThrowIfNull(v);
        // Priority: the trainer's cover, then the frame captured at upload, then nothing —
        // which the UI renders as the branded placeholder.
        var thumbnailKey = v.CoverObjectKey ?? v.ThumbnailObjectKey;
        var thumbnail = thumbnailKey is null || presign is null ? null : presign(thumbnailKey);
        return new(v.Id, v.Title, v.Description, v.Audience, v.BodyArea, v.RequiresEquipment,
        v.Status, v.IsPublished, v.SortOrder, v.DurationSeconds, thumbnail, v.CoverObjectKey is not null, v.SizeBytes, v.CreatedAtUtc, v.Revision,
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
