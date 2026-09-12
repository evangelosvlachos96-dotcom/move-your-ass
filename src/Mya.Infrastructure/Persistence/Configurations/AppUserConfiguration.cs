using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mya.Infrastructure.Identity;

namespace Mya.Infrastructure.Persistence.Configurations;

public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    private const int NameMaxLength = 80;

    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(t => t.HasCheckConstraint("CK_AspNetUsers_Status", "[Status] IN (0, 1, 2, 3)"));

        builder.Property(u => u.FirstName).HasMaxLength(NameMaxLength).IsRequired();
        builder.Property(u => u.LastName).HasMaxLength(NameMaxLength).IsRequired();
        builder.Property(u => u.Status).HasConversion<int>().IsRequired();
        builder.Property(u => u.MustChangePassword).HasDefaultValue(false);
        builder.Property(u => u.ApprovedByUserId).HasMaxLength(450);
        builder.Property(u => u.SuspensionReason).HasMaxLength(500);
        builder.Property(u => u.ActiveSessionUserAgent).HasMaxLength(256);

        builder.HasIndex(u => u.Status).HasDatabaseName("IX_User_Status");
    }
}
