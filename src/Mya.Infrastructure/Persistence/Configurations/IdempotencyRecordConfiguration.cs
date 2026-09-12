using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mya.Domain.Entities;

namespace Mya.Infrastructure.Persistence.Configurations;

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("IdempotencyRecord");
        builder.HasKey(r => r.Id);

        // No FK to AspNetUsers: POST /auth/register is anonymous and also takes an Idempotency-Key.
        builder.Property(r => r.UserId).HasMaxLength(450).IsRequired();
        builder.Property(r => r.Key).HasMaxLength(100).IsRequired();
        builder.Property(r => r.ResponseJson).HasMaxLength(-1).IsRequired();

        builder.HasIndex(r => new { r.UserId, r.Key })
            .IsUnique()
            .HasDatabaseName("UX_Idempotency_User_Key");
    }
}
