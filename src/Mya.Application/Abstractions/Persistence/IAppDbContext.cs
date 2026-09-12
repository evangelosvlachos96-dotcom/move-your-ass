using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mya.Domain.Entities;

namespace Mya.Application.Abstractions.Persistence;

/// <summary>
/// Persistence seam for handlers. Exposes EF Core's <see cref="DbSet{TEntity}"/> so handlers
/// can use the async LINQ operators (docs/02 section 1: Application may reference
/// Microsoft.EntityFrameworkCore, never the SqlServer provider). Users are reached through
/// <see cref="Identity.IUserService"/>, which shares this context and therefore this transaction.
/// </summary>
public interface IAppDbContext
{
    public DbSet<RefreshToken> RefreshTokens { get; }

    public DbSet<TwoFactorTicket> TwoFactorTickets { get; }

    public DbSet<OutboxMessage> OutboxMessages { get; }

    public DbSet<IdempotencyRecord> IdempotencyRecords { get; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Wraps a user-store change and its outbox rows in one transaction (ADR-010).</summary>
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
}
