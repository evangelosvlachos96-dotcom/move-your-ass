using Mya.Application.Abstractions.Identity;
using Mya.Application.Abstractions.Persistence;
using Mya.Application.Abstractions.System;
using Mya.Application.Common.Notifications;
using Mya.Application.Common.Results;
using Mya.Domain.Enums;

namespace Mya.Application.Features.Users.ApproveUser;

public sealed record ApproveUserCommand(string UserId);

public sealed class ApproveUserHandler(IUserService users, IAppDbContext db, IClock clock, ICurrentUser currentUser)
{
    public async Task<Result> Handle(ApproveUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await users.FindByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(Errors.UserNotFound);
        }

        if (user.Status is not UserStatus.PendingApproval)
        {
            return Result.Failure(Errors.UserNotPending);
        }

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        await users.SetStatusAsync(user.Id, UserStatus.Active, reason: null, currentUser.UserId, cancellationToken);
        db.OutboxMessages.Add(EmailOutbox.AccountApproved(user, clock.UtcNow));
        await db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
