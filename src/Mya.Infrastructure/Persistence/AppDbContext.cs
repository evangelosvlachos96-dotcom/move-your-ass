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
    /// <summary>Identity's default string-key length; every UserId column matches it.</summary>
    private const int StringKeyLength = 450;

    public DbSet<PasswordInvitation> PasswordInvitations => Set<PasswordInvitation>();

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
    /// Identity's string keys. Keep every string key at Identity's default 450 and make every
    /// foreign key column identical to its principal, so the two never drift apart.
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

        // timestamptz is the only correct choice on PostgreSQL: Npgsql throws when a DateTime
        // whose Kind is not Utc is written to it, and reads it back as Kind = Utc. The converter
        // is what guarantees that Kind, so it is load-bearing rather than belt-and-braces.
        configurationBuilder.Properties<DateTime>()
            .HaveConversion<UtcDateTimeConverter>()
            .HaveColumnType("timestamp with time zone");

        configurationBuilder.Properties<string>().HaveMaxLength(256);
    }
}
