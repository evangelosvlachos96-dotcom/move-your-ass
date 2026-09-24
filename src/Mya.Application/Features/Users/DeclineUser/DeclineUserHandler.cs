using AutoMapper;
using FluentValidation;
using Mya.Application.Abstractions.Identity;
using Mya.Application.Abstractions.Persistence;
using Mya.Application.Abstractions.System;
using Mya.Application.Common.Notifications;
using Mya.Application.Common.Results;
using Mya.Application.Common.Validation;
using Mya.Application.Features.Common;
using Mya.Domain.Enums;

namespace Mya.Application.Features.Users.DeclineUser;

/// <summary>Optional body of POST /api/admin/users/{id}/decline. The target user comes from the route only.</summary>
public sealed record DeclineUserRequest(string? Reason);

/// <summary>Built by the controller from the route id and the <see cref="DeclineUserRequest"/> body.</summary>
public sealed record DeclineUserCommand(string? Reason)
{
    public string UserId { get; init; } = string.Empty;
}

public sealed class DeclineUserValidator : AbstractValidator<DeclineUserRequest>
{
    public DeclineUserValidator()
    {
        RuleFor(x => x.Reason).OptionalReason();
    }
}

/// <summary>
/// The reason travels only in the email; the account keeps no record of it. Returns the updated
/// user so the admin list can patch the row without a second request.
/// </summary>
public sealed class DeclineUserHandler(IUserService users, IAppDbContext db, IClock clock, ICurrentUser currentUser, IMapper mapper)
{
    public async Task<Result<UserDto>> Handle(DeclineUserCommand command, CancellationToken cancellationToken)
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

        var reason = string.IsNullOrWhiteSpace(command.Reason) ? null : command.Reason.Trim();

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        await users.SetStatusAsync(user.Id, UserStatus.Declined, reason, currentUser.UserId, cancellationToken);
        db.OutboxMessages.Add(EmailOutbox.AccountDeclined(user, reason, clock.UtcNow));
        await db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var updated = await users.FindByIdAsync(user.Id, cancellationToken);
        return Result.Success(mapper.Map<UserDto>(updated));
    }
}
