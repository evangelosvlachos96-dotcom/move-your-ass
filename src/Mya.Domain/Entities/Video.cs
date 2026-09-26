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
    public string StorageProvider { get; set; } = "BunnyStream";
    public string? ExternalId { get; set; }
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
