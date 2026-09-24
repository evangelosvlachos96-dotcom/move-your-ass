using Mya.Application.Abstractions.Identity;
using Mya.Application.Abstractions.Persistence;
using Mya.Application.Abstractions.System;
using Mya.Application.Common.Results;

namespace Mya.Application.Features.Auth.ChangePassword;

/// <summary>
/// Allowed while MustChangePassword is set (it is how the flag gets cleared). The current
/// session survives: refresh does not compare security stamps in lazy mode (ADR-005), so the
/// client only needs to refresh once to drop the must-change claim from its access token.
///
/// Two paths, decided by the database and never by the request:
/// <list type="bullet">
///   <item>Forced change (<c>MustChangePassword == true</c>): the user authenticated seconds ago
///   with a temporary password the admin handed them, so the current password is not required.
///   If it is sent anyway it is still checked.</item>
///   <item>Voluntary change: the current password is required and must match, otherwise
///   400 CURRENT_PASSWORD_WRONG. A missing current password counts as wrong.</item>
/// </list>
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

        var user = await users.FindByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(Errors.UserNotFound);
        }

        var currentPasswordOmitted = string.IsNullOrEmpty(command.CurrentPassword);

        if (currentPasswordOmitted && !user.MustChangePassword)
        {
            return Result.Failure(Errors.CurrentPasswordWrong);
        }

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        if (currentPasswordOmitted)
        {
            // Independently confirmed above: the account is on a temporary password.
            await users.ResetPasswordAsync(userId, command.NewPassword, cancellationToken);
        }
        else
        {
            var changed = await users.ChangePasswordAsync(userId, command.CurrentPassword!, command.NewPassword, cancellationToken);
            if (changed.IsFailure)
            {
                return changed;
            }
        }

        await users.SetMustChangePasswordAsync(userId, false, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
