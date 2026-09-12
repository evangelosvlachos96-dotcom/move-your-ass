using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mya.Application.Abstractions.Persistence;
using Mya.Domain.Entities;
using Mya.Infrastructure.Identity;
using Mya.Infrastructure.Persistence.Conventions;

namespace Mya.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser>(options), IAppDbContext
{
    /// <summary>Identity's default key length; every UserId column in docs/04 section 4 is nvarchar(450).</summary>
    private const int StringKeyLength = 450;

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<TwoFactorTicket> TwoFactorTickets => Set<TwoFactorTicket>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        Database.BeginTransactionAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        RestoreStringKeyLengths(builder);
    }

    /// <summary>
    /// The 256 default string length in <see cref="ConfigureConventions"/> would also shrink
    /// Identity's string keys, and SQL Server refuses a foreign key whose columns differ in length
    /// from the key they reference. Keep every string key at Identity's default 450 and make every
    /// foreign key column identical to its principal.
    /// </summary>
    private static void RestoreStringKeyLengths(ModelBuilder builder)
    {
        var entityTypes = builder.Model.GetEntityTypes().ToList();

        foreach (var property in entityTypes.SelectMany(e => e.GetKeys()).SelectMany(k => k.Properties))
        {
            if (property.ClrType == typeof(string))
            {
                property.SetMaxLength(StringKeyLength);
            }
        }

        foreach (var foreignKey in entityTypes.SelectMany(e => e.GetForeignKeys()))
        {
            for (var i = 0; i < foreignKey.Properties.Count; i++)
            {
                var dependent = foreignKey.Properties[i];
                if (dependent.ClrType == typeof(string))
                {
                    dependent.SetMaxLength(foreignKey.PrincipalKey.Properties[i].GetMaxLength());
                }
            }
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        configurationBuilder.Properties<DateTime>()
            .HaveConversion<UtcDateTimeConverter>()
            .HaveColumnType("datetime2(3)");

        configurationBuilder.Properties<string>().HaveMaxLength(256);
    }
}
