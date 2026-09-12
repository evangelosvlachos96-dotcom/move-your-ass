using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mya.Domain.Entities;
using Mya.Infrastructure.Identity;

namespace Mya.Infrastructure.Persistence.Configurations;

public sealed class TwoFactorTicketConfiguration : IEntityTypeConfiguration<TwoFactorTicket>
{
    public void Configure(EntityTypeBuilder<TwoFactorTicket> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("TwoFactorTicket");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.UserId).HasMaxLength(450).IsRequired();
        builder.Property(t => t.CodeHash).HasColumnType("binary(32)").IsRequired();
        builder.Property(t => t.Attempts).HasDefaultValue(0);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
