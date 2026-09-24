using AutoMapper;
using Mya.Application.Abstractions.Identity;
using Mya.Application.Abstractions.System;
using Mya.Application.Common.Results;
using Mya.Application.Features.Common;
using Mya.Domain.Enums;

namespace Mya.Application.Features.Users.ReactivateUser;

public sealed record ReactivateUserCommand(string UserId);

/// <summary>
/// Suspended or Declined becomes Active again. Already-active and still-pending accounts are a
/// no-op: a pending registration is approved (with its email), never reactivated. Returns the
/// updated user so the admin list can patch the row without a second request.
/// </summary>
public sealed class ReactivateUserHandler(IUserService users, ICurrentUser currentUser, IMapper mapper)
{
    public async Task<Result<UserDto>> Handle(ReactivateUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await users.FindByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<UserDto>(Errors.UserNotFound);
        }

        if (user.Status is not (UserStatus.Suspended or UserStatus.Declined))
        {
            return Result.Success(mapper.Map<UserDto>(user));
        }

        await users.SetStatusAsync(user.Id, UserStatus.Active, reason: null, currentUser.UserId, cancellationToken);

        var updated = await users.FindByIdAsync(user.Id, cancellationToken);
        return Result.Success(mapper.Map<UserDto>(updated));
    }
}
