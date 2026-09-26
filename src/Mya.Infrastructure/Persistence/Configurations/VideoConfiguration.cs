using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mya.Domain.Entities;
namespace Mya.Infrastructure.Persistence.Configurations;
public sealed class VideoConfiguration : IEntityTypeConfiguration<Video>
{
    public void Configure(EntityTypeBuilder<Video> b)
    {
        ArgumentNullException.ThrowIfNull(b);
        b.ToTable("Video"); b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.ExternalId).HasMaxLength(64);
        b.Property(x => x.ThumbnailUrl).HasMaxLength(1000);
        b.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
        b.Property(x => x.CreationKey).HasMaxLength(100).IsRequired();
        b.Property(x => x.CreationHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.Revision).IsConcurrencyToken();
        b.HasIndex(x => new { x.CreatedByUserId, x.CreationKey }).IsUnique();
        b.HasIndex(x => x.ExternalId).IsUnique();
        b.HasIndex(x => new { x.IsPublished, x.Status, x.SortOrder });
    }
}
public sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> b)
    {
        ArgumentNullException.ThrowIfNull(b);
        b.ToTable("Tag"); b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(60).IsRequired();
        b.Property(x => x.NormalizedName).HasMaxLength(120).IsRequired();
        b.HasIndex(x => x.NormalizedName).IsUnique();
    }
}
public sealed class VideoTagConfiguration : IEntityTypeConfiguration<VideoTag>
{
    public void Configure(EntityTypeBuilder<VideoTag> b)
    {
        ArgumentNullException.ThrowIfNull(b);
        b.ToTable("VideoTag"); b.HasKey(x => new { x.VideoId, x.TagId });
        b.HasOne(x => x.Video).WithMany(x => x.VideoTags).HasForeignKey(x => x.VideoId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Tag).WithMany(x => x.VideoTags).HasForeignKey(x => x.TagId).OnDelete(DeleteBehavior.Restrict);
    }
}
