using Mya.Application.Abstractions.Identity;
using Mya.Application.Abstractions.System;
using Mya.Application.Common.Results;

namespace Mya.Application.Features.Auth.UpdateProfile;

public sealed class UpdateProfileHandler(IUserService users, ICurrentUser currentUser)
{
    public async Task<Result> Handle(UpdateProfileCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure(Errors.UserNotFound);
        }

        await users.UpdateNamesAsync(userId, command.FirstName.Trim(), command.LastName.Trim(), cancellationToken);
        return Result.Success();
    }
}
