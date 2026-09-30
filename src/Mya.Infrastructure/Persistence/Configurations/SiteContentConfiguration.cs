using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mya.Domain.Entities;

namespace Mya.Infrastructure.Persistence.Configurations;

public sealed class SiteContentConfiguration : IEntityTypeConfiguration<SiteContent>
{
    public void Configure(EntityTypeBuilder<SiteContent> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("SiteContent");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PhotoObjectKey).HasMaxLength(200);
        builder.Property(x => x.TrainerName).HasMaxLength(120);
        builder.Property(x => x.Tagline).HasMaxLength(200);
        builder.Property(x => x.AboutMarkdown).HasMaxLength(4000);
        builder.Property(x => x.ContactEmail).HasMaxLength(256);
        builder.Property(x => x.Phone).HasMaxLength(40);
        builder.Property(x => x.BookingUrl).HasMaxLength(200);
        builder.Property(x => x.SocialLinksJson).HasMaxLength(4000);
        builder.Property(x => x.Instagram).HasMaxLength(200);
        builder.Property(x => x.YouTube).HasMaxLength(200);
        builder.Property(x => x.TikTok).HasMaxLength(200);
        builder.Property(x => x.Facebook).HasMaxLength(200);
        builder.Property(x => x.WhatsApp).HasMaxLength(20);
        builder.Property(x => x.Website).HasMaxLength(200);

        builder.Property(x => x.Revision).IsConcurrencyToken();
    }
}
