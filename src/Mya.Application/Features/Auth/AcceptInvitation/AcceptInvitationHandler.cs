using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Mya.Application.Abstractions.Identity;
using Mya.Application.Abstractions.Persistence;
using Mya.Application.Abstractions.System;
using Mya.Application.Common.Results;
using Mya.Application.Common.Security;
using Mya.Application.Common.Validation;
using Mya.Domain.Enums;

namespace Mya.Application.Features.Auth.AcceptInvitation;

public sealed record AcceptInvitationCommand(string Token, string NewPassword);

public sealed class AcceptInvitationValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationValidator()
    {
        RuleFor(x => x.Token).NotEmpty().Length(64).Matches("^[A-F0-9]+$");
        RuleFor(x => x.NewPassword).Password();
    }
}

public sealed class AcceptInvitationHandler(IAppDbContext db, IUserService users, IClock clock)
{
    public async Task<Result> Handle(AcceptInvitationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var hash = PasswordInvitations.Hash(command.Token);
        var now = clock.UtcNow;
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        var invitation = await db.PasswordInvitations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (invitation is null) { return Result.Failure(Errors.InvalidInvitation); }

        // The conditional update is the single-use guarantee, including concurrent submissions.
        var consumed = await db.PasswordInvitations
            .Where(x => x.Id == invitation.Id && x.ConsumedAtUtc == null && x.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ConsumedAtUtc, now), cancellationToken);
        if (consumed != 1) { return Result.Failure(Errors.InvalidInvitation); }

        var user = await users.FindByIdAsync(invitation.UserId, cancellationToken);
        if (user is null || user.Status != UserStatus.Invited)
        {
            return Result.Failure(Errors.InvalidInvitation);
        }

        await users.ResetPasswordAsync(user.Id, command.NewPassword, cancellationToken);
        await users.SetMustChangePasswordAsync(user.Id, false, cancellationToken);
        if (!await users.SetStatusAsync(user.Id, UserStatus.Active, null, null, cancellationToken))
        {
            // The account changed while the link was being used; the token is no longer good.
            return Result.Failure(Errors.InvalidInvitation);
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
