using Mya.Application.Abstractions.Identity;
using Mya.Application.Common.Results;
using Mya.Application.Common.Security;
using Mya.Domain.Constants;
using Mya.Domain.Enums;

namespace Mya.Application.Features.Users.CreateUser;

/// <summary>Admin-created path: Active immediately, forced to change the temporary password on first login.</summary>
public sealed class CreateUserHandler(IUserService users)
{
    public async Task<Result<CreateUserResponse>> Handle(CreateUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var temporaryPassword = TemporaryPassword.Generate();

        var created = await users.CreateAsync(
            new NewUserAccount(
                command.Email.Trim(),
                command.FirstName.Trim(),
                command.LastName.Trim(),
                command.Role ?? Roles.Client,
                UserStatus.Active,
                MustChangePassword: true),
            temporaryPassword,
            cancellationToken);

        return created.IsFailure
            ? Result.Failure<CreateUserResponse>(created.Error!)
            : Result.Success(new CreateUserResponse(created.Value.Id, temporaryPassword));
    }
}
