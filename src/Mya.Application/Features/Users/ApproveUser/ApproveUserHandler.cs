using AutoMapper;
using Mya.Application.Abstractions.Identity;
using Mya.Application.Abstractions.Persistence;
using Mya.Application.Abstractions.System;
using Mya.Application.Common.Notifications;
using Mya.Application.Common.Results;
using Mya.Application.Features.Common;
using Mya.Domain.Enums;

namespace Mya.Application.Features.Users.ApproveUser;

public sealed record ApproveUserCommand(string UserId);

/// <summary>Returns the updated user so the admin list can patch the row without a second request.</summary>
public sealed class ApproveUserHandler(IUserService users, IAppDbContext db, IClock clock, ICurrentUser currentUser, IMapper mapper)
{
    public async Task<Result<UserDto>> Handle(ApproveUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await users.FindByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<UserDto>(Errors.UserNotFound);
        }

        if (user.Status is not UserStatus.PendingApproval)
        {
            return Result.Failure<UserDto>(Errors.UserNotPending);
        }

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        if (!await users.SetStatusAsync(user.Id, UserStatus.Active, reason: null, currentUser.UserId, cancellationToken))
        {
            // Someone else acted on this account between the check above and here.
            return Result.Failure<UserDto>(Errors.InvalidUserState);
        }
        db.OutboxMessages.Add(EmailOutbox.AccountApproved(user, clock.UtcNow));
        await db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var updated = await users.FindByIdAsync(user.Id, cancellationToken);
        return Result.Success(mapper.Map<UserDto>(updated));
    }
}
