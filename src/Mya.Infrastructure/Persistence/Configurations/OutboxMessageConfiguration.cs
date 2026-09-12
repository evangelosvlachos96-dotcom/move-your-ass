using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mya.Domain.Entities;

namespace Mya.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("OutboxMessage");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Type).IsRequired();
        builder.Property(m => m.PayloadJson).HasMaxLength(-1).IsRequired();
        builder.Property(m => m.LastError).HasMaxLength(-1);

        builder.HasIndex(m => m.CreatedAtUtc)
            .HasDatabaseName("IX_Outbox_Pending")
            .HasFilter("[ProcessedAtUtc] IS NULL");
    }
}
