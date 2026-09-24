using Mya.Application.Abstractions.Identity;
using Mya.Application.Abstractions.Persistence;
using Mya.Application.Common.Results;
using Mya.Application.Common.Security;
using Mya.Domain.Constants;
using Mya.Domain.Enums;

namespace Mya.Application.Features.Users.CreateUser;

/// <summary>Creates an inactive invited account and queues its password setup email atomically.</summary>
public sealed class CreateUserHandler(IUserService users, IAppDbContext db, PasswordInvitations invitations)
{
    public async Task<Result<CreateUserResponse>> Handle(CreateUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        var created = await users.CreateAsync(new NewUserAccount(
            command.Email.Trim(), command.FirstName.Trim(), command.LastName.Trim(),
            command.Role ?? Roles.Client, UserStatus.Invited, MustChangePassword: true),
            TemporaryPassword.Generate(), cancellationToken);
        if (created.IsFailure)
        {
            return Result.Failure<CreateUserResponse>(created.Error!);
        }

        await invitations.QueueAsync(created.Value, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success(new CreateUserResponse(created.Value.Id));
    }
}
