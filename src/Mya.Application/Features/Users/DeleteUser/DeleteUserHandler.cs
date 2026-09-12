using Mya.Application.Abstractions.Identity;
using Mya.Application.Abstractions.System;
using Mya.Application.Common.Results;
using Mya.Domain.Constants;

namespace Mya.Application.Features.Users.DeleteUser;

public sealed record DeleteUserCommand(string UserId);

/// <summary>Hard delete (docs/04-roadmap.md). Refresh tokens and tickets go with the row via cascade.</summary>
public sealed class DeleteUserHandler(IUserService users, ICurrentUser currentUser)
{
    public async Task<Result> Handle(DeleteUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.Equals(command.UserId, currentUser.UserId, StringComparison.Ordinal))
        {
            return Result.Failure(Errors.CannotDeleteSelf);
        }

        var user = await users.FindByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(Errors.UserNotFound);
        }

        if (user.Role == Roles.Admin && await users.CountInRoleAsync(Roles.Admin, cancellationToken) <= 1)
        {
            return Result.Failure(Errors.CannotDeleteLastAdmin);
        }

        await users.DeleteAsync(user.Id, cancellationToken);
        return Result.Success();
    }
}
