using Mya.Application.Abstractions.Identity;
using Mya.Application.Abstractions.Persistence;
using Mya.Application.Common.Results;
using Mya.Domain.Constants;

namespace Mya.Application.Features.Users.UpdateUser;

/// <summary>Demoting the last Admin is refused for the same reason deleting them is.</summary>
public sealed class UpdateUserHandler(IUserService users, IAppDbContext db)
{
    public async Task<Result> Handle(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await users.FindByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(Errors.UserNotFound);
        }

        var roleChanges = !string.Equals(user.Role, command.Role, StringComparison.Ordinal);

        if (roleChanges
            && user.Role == Roles.Admin
            && await users.CountInRoleAsync(Roles.Admin, cancellationToken) <= 1)
        {
            return Result.Failure(Errors.CannotDeleteLastAdmin);
        }

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        await users.UpdateNamesAsync(user.Id, command.FirstName.Trim(), command.LastName.Trim(), cancellationToken);
        if (roleChanges)
        {
            await users.SetRoleAsync(user.Id, command.Role, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
