using Mya.Application.Abstractions.Identity;
using Mya.Application.Abstractions.Persistence;
using Mya.Application.Common.Results;
using Mya.Application.Common.Security;
using Mya.Domain.Enums;

namespace Mya.Application.Features.Users.ResendInvitation;

public sealed class ResendInvitationHandler(IUserService users, IAppDbContext db, PasswordInvitations invitations)
{
    public async Task<Result> Handle(string userId, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId, cancellationToken);
        if (user is null) { return Result.Failure(Errors.UserNotFound); }
        if (user.Status != UserStatus.Invited) { return Result.Failure(Errors.InvalidUserState); }
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await invitations.QueueAsync(user, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
