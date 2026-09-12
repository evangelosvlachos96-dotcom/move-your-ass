using Microsoft.EntityFrameworkCore;
using Mya.Application.Abstractions.Identity;
using Mya.Application.Abstractions.Persistence;
using Mya.Application.Abstractions.System;
using Mya.Application.Common.Results;
using Mya.Application.Common.Security;

namespace Mya.Application.Features.Users.ResetPassword;

public sealed record ResetPasswordCommand(string UserId);

public sealed record ResetPasswordResponse(string TemporaryPassword);

/// <summary>Issues a new temporary password, forces a change on next login and ends any live session.</summary>
public sealed class ResetPasswordHandler(IUserService users, IAppDbContext db, IClock clock)
{
    public async Task<Result<ResetPasswordResponse>> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await users.FindByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<ResetPasswordResponse>(Errors.UserNotFound);
        }

        var temporaryPassword = TemporaryPassword.Generate();
        var now = clock.UtcNow;

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        await users.ResetPasswordAsync(user.Id, temporaryPassword, cancellationToken);
        await users.SetMustChangePasswordAsync(user.Id, true, cancellationToken);
        await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), cancellationToken);
        await users.EndSessionAsync(user.Id, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Result.Success(new ResetPasswordResponse(temporaryPassword));
    }
}
