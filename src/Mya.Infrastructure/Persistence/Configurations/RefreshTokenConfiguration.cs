using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mya.Domain.Entities;
using Mya.Infrastructure.Identity;

namespace Mya.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("RefreshToken");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.UserId).HasMaxLength(450).IsRequired();
        builder.Property(t => t.TokenHash).HasColumnType("binary(32)").IsRequired();
        builder.Property(t => t.CreatedByIp).HasMaxLength(45);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.UserId)
            .HasDatabaseName("IX_RefreshToken_UserActive")
            .HasFilter("[RevokedAtUtc] IS NULL");

        builder.HasIndex(t => t.FamilyId).HasDatabaseName("IX_RefreshToken_Family");
    }
}
