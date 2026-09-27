using Microsoft.EntityFrameworkCore;
using Mya.Application.Abstractions.Identity;
using Mya.Application.Abstractions.Persistence;
using Mya.Application.Abstractions.System;
using Mya.Application.Common.Results;
using Mya.Domain.Constants;

namespace Mya.Application.Features.Users.DeleteUser;

public sealed record DeleteUserCommand(string UserId);

/// <summary>
/// Hard delete (docs/04-roadmap.md). Refresh tokens and tickets go with the row via cascade.
/// Unsent emails about this account go too: an approval racing a deletion would otherwise leave
/// "your account was approved" queued to somebody who no longer has one.
/// </summary>
public sealed class DeleteUserHandler(IUserService users, ICurrentUser currentUser, IAppDbContext db)
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

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        await users.DeleteAsync(user.Id, cancellationToken);
        await db.OutboxMessages
            .Where(m => m.SubjectUserId == user.Id && m.ProcessedAtUtc == null)
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
