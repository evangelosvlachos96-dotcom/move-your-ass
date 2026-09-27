using Microsoft.EntityFrameworkCore;
using Mya.Application.Abstractions.Identity;
using Mya.Application.Abstractions.Persistence;
using Mya.Application.Abstractions.System;
using Mya.Application.Common.Results;
using Mya.Domain.Constants;

namespace Mya.Application.Features.Users.UpdateUser;

/// <summary>
/// Names and role. Three rules guard the role, all server-side because the UI is a convenience:
/// an admin may never change their own role, the last Admin may not be demoted, and a role change
/// ends the affected user's session so the new role applies on their very next request.
/// </summary>
public sealed class UpdateUserHandler(IUserService users, IAppDbContext db, ICurrentUser currentUser, IClock clock)
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

        // Self-demotion is how an administrator locks themselves out, and self-promotion is how a
        // compromised client account becomes an admin. Neither is ever legitimate here.
        if (roleChanges && string.Equals(command.UserId, currentUser.UserId, StringComparison.Ordinal))
        {
            return Result.Failure(Errors.CannotModifySelf);
        }

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

            // The access token carries the old role for up to its lifetime. Ending the session and
            // revoking the refresh tokens means the next refresh fails and they sign in again;
            // CurrentAdminHandler covers the gap until then by re-reading the role per request.
            await users.EndSessionAsync(user.Id, cancellationToken);
            await db.RefreshTokens
                .Where(t => t.UserId == user.Id && t.RevokedAtUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, clock.UtcNow), cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
