using Mya.Application.Abstractions.Identity;
using Mya.Application.Abstractions.Persistence;
using Mya.Application.Abstractions.System;
using Mya.Application.Common.Results;

namespace Mya.Application.Features.Auth.ChangePassword;

/// <summary>
/// Allowed while MustChangePassword is set (it is how the flag gets cleared). The current
/// session survives: refresh does not compare security stamps in lazy mode (ADR-005), so the
/// client only needs to refresh once to drop the must-change claim from its access token.
/// </summary>
public sealed class ChangePasswordHandler(IUserService users, IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure(Errors.UserNotFound);
        }

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        var changed = await users.ChangePasswordAsync(userId, command.CurrentPassword, command.NewPassword, cancellationToken);
        if (changed.IsFailure)
        {
            return changed;
        }

        await users.SetMustChangePasswordAsync(userId, false, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
