using Mya.Domain.Enums;
namespace Mya.Domain.Entities;
public sealed class Video
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public VideoAudience Audience { get; set; }
    public VideoBodyArea BodyArea { get; set; }
    public bool RequiresEquipment { get; set; }
    public VideoStatus Status { get; set; }
    public bool IsPublished { get; set; }
    public int SortOrder { get; set; }
    public string StorageProvider { get; set; } = "S3";

    /// <summary>Provider object key for the original recording. Unique while the row exists.</summary>
    public string? ExternalId { get; set; }

    /// <summary>Provider multipart upload id, held while an upload is in flight so the server
    /// can complete or abort it after a restart. Cleared once the object exists (ADR-019).</summary>
    public string? UploadId { get; set; }

    /// <summary>Bytes the object occupies. Declared at creation, replaced by the HEAD value once
    /// the upload completes. Summed to enforce the storage cap, so it counts drafts too.</summary>
    public long? SizeBytes { get; set; }

    /// <summary>Content type of the original, as accepted at creation and confirmed by HEAD.</summary>
    public string? ContentType { get; set; }

    /// <summary>Provider object key of the poster frame, when one was captured.</summary>
    public string? ThumbnailObjectKey { get; set; }

    public int? DurationSeconds { get; set; }
    public string? ThumbnailUrl { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public Guid Revision { get; set; } = Guid.NewGuid();
    public string CreationKey { get; set; } = string.Empty;
    public string CreationHash { get; set; } = string.Empty;
    public ICollection<VideoTag> VideoTags { get; set; } = new List<VideoTag>();
}
