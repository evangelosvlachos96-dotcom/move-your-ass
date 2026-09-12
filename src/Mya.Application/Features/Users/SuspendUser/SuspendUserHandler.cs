using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Mya.Application.Abstractions.Identity;
using Mya.Application.Abstractions.Persistence;
using Mya.Application.Abstractions.System;
using Mya.Application.Common.Results;
using Mya.Application.Common.Validation;
using Mya.Domain.Enums;

namespace Mya.Application.Features.Users.SuspendUser;

/// <summary>Body of POST /api/admin/users/{id}/suspend; the controller fills <see cref="UserId"/>.</summary>
public sealed record SuspendUserCommand(string? Reason)
{
    public string UserId { get; init; } = string.Empty;
}

public sealed class SuspendUserValidator : AbstractValidator<SuspendUserCommand>
{
    public SuspendUserValidator()
    {
        RuleFor(x => x.Reason).OptionalReason();
    }
}

/// <summary>
/// docs/03 section 4.4: status, security stamp, refresh tokens and session all go at once. The
/// user's current access token keeps working for at most 15 minutes (ADR-005).
/// </summary>
public sealed class SuspendUserHandler(IUserService users, IAppDbContext db, IClock clock, ICurrentUser currentUser)
{
    public async Task<Result> Handle(SuspendUserCommand command, CancellationToken cancellationToken)
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

        if (user.Status is UserStatus.Suspended)
        {
            return Result.Success();
        }

        var reason = string.IsNullOrWhiteSpace(command.Reason) ? null : command.Reason.Trim();
        var now = clock.UtcNow;

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        await users.SetStatusAsync(user.Id, UserStatus.Suspended, reason, currentUser.UserId, cancellationToken);
        await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), cancellationToken);
        await users.EndSessionAsync(user.Id, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
