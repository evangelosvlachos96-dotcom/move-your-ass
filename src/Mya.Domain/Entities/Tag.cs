namespace Mya.Domain.Entities;
public sealed class Tag
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public ICollection<VideoTag> VideoTags { get; set; } = new List<VideoTag>();
}
public sealed class VideoTag
{
    public Guid VideoId { get; set; }
    public Video Video { get; set; } = null!;
    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}
