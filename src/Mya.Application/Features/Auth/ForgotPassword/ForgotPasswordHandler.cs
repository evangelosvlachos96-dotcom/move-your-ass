using FluentValidation;
using Mya.Application.Abstractions.Identity;
using Mya.Application.Abstractions.Persistence;
using Mya.Application.Common.Results;
using Mya.Application.Common.Security;
using Mya.Domain.Enums;

namespace Mya.Application.Features.Auth.ForgotPassword;

public sealed record ForgotPasswordCommand(string Email);

public sealed class ForgotPasswordValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordValidator() => RuleFor(x => x.Email).NotEmpty().MaximumLength(256).EmailAddress();
}

/// <summary>
/// Sends a single-use reset link, and says nothing about whether the address exists.
///
/// **Every outcome returns success.** An unknown address, a suspended account and a declined one
/// all look identical from outside, because a forgotten-password form that answers "no such user"
/// is an account enumeration oracle — and this is a private library where the client list is
/// itself worth keeping quiet. The rate limiter, not the response, is what stops someone probing.
/// </summary>
public sealed class ForgotPasswordHandler(IUserService users, IAppDbContext db, PasswordInvitations invitations)
{
    public async Task<Result> Handle(ForgotPasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await users.FindByEmailAsync(command.Email.Trim(), cancellationToken);

        // Only an account that can actually sign in gets a link. An Invited account already has
        // an invitation, and suspended or declined accounts must not be reactivated this way.
        if (user is { Status: UserStatus.Active })
        {
            await using var transaction = await db.BeginTransactionAsync(cancellationToken);
            await invitations.QueueResetAsync(user, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        return Result.Success();
    }
}
