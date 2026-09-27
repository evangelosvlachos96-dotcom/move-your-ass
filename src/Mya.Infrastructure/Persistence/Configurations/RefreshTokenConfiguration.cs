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
        builder.Property(t => t.TokenHash).HasColumnType("bytea").IsRequired();
        builder.Property(t => t.CreatedByIp).HasMaxLength(45);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.UserId)
            .HasDatabaseName("IX_RefreshToken_UserActive")
            .HasFilter("\"RevokedAtUtc\" IS NULL");

        builder.HasIndex(t => t.FamilyId).HasDatabaseName("IX_RefreshToken_Family");

        // Every refresh looks a token up by its hash, and without this it was a sequential scan
        // over the whole table — measured at 20,000 rows: 7.2 ms and 19,999 rows discarded,
        // against 0.17 ms with the index. Unique because two rows sharing a hash would mean
        // either a SHA-256 collision or a bug, and neither should be stored quietly.
        builder.HasIndex(t => t.TokenHash)
            .IsUnique()
            .HasDatabaseName("IX_RefreshToken_TokenHash");
    }
}
