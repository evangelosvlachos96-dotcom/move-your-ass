using Microsoft.EntityFrameworkCore;
using Mya.Application.Abstractions.Identity;
using Mya.Application.Abstractions.Persistence;
using Mya.Application.Abstractions.System;
using Mya.Application.Common.Results;

namespace Mya.Application.Features.Auth.Logout;

/// <summary>Revokes every live refresh token and clears the active session. The access token expires on its own.</summary>
public sealed class LogoutHandler(IUserService users, IAppDbContext db, IClock clock, ICurrentUser currentUser)
{
    public async Task<Result> Handle(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Success();
        }

        var now = clock.UtcNow;

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), cancellationToken);

        await users.EndSessionAsync(userId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
