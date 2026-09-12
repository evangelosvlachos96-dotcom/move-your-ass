using Mya.Application.Abstractions.Identity;
using Mya.Application.Abstractions.System;
using Mya.Application.Common.Results;
using Mya.Domain.Enums;

namespace Mya.Application.Features.Users.ReactivateUser;

public sealed record ReactivateUserCommand(string UserId);

/// <summary>
/// Suspended or Declined becomes Active again. Already-active and still-pending accounts are a
/// no-op: a pending registration is approved (with its email), never reactivated.
/// </summary>
public sealed class ReactivateUserHandler(IUserService users, ICurrentUser currentUser)
{
    public async Task<Result> Handle(ReactivateUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await users.FindByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(Errors.UserNotFound);
        }

        if (user.Status is not (UserStatus.Suspended or UserStatus.Declined))
        {
            return Result.Success();
        }

        await users.SetStatusAsync(user.Id, UserStatus.Active, reason: null, currentUser.UserId, cancellationToken);
        return Result.Success();
    }
}
