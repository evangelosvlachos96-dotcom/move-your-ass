using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mya.Domain.Entities;
using Mya.Infrastructure.Identity;

namespace Mya.Infrastructure.Persistence.Configurations;

public sealed class PasswordInvitationConfiguration : IEntityTypeConfiguration<PasswordInvitation>
{
    public void Configure(EntityTypeBuilder<PasswordInvitation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("PasswordInvitation");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.TokenHash).HasColumnType("bytea").IsRequired();
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
