using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Mya.Application.Abstractions.Identity;
using Mya.Application.Abstractions.Persistence;
using Mya.Application.Abstractions.System;
using Mya.Application.Common.Results;
using Mya.Application.Common.Security;
using Mya.Application.Common.Validation;
using Mya.Domain.Enums;

namespace Mya.Application.Features.Auth.ForgotPassword;

public sealed record ResetPasswordCommand(string Token, string NewPassword);

public sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Token).NotEmpty().Length(64).Matches("^[A-F0-9]+$");
        RuleFor(x => x.NewPassword).Password();
    }
}

/// <summary>
/// Consumes a reset link and sets the new password. A successful reset **ends every session**:
/// whoever asked for the reset may well be recovering an account someone else has access to, so
/// leaving other devices signed in would defeat the point.
/// </summary>
public sealed class ResetPasswordHandler(IAppDbContext db, IUserService users, IClock clock)
{
    public async Task<Result> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var hash = PasswordInvitations.Hash(command.Token);
        var now = clock.UtcNow;

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        var credential = await db.PasswordInvitations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (credential is null || credential.Purpose != PasswordInvitationPurpose.Reset)
        {
            // A token issued to activate an invited account must not double as a reset.
            return Result.Failure(Errors.InvalidInvitation);
        }

        // The conditional update is the single-use guarantee, including concurrent submissions.
        var consumed = await db.PasswordInvitations
            .Where(x => x.Id == credential.Id && x.ConsumedAtUtc == null && x.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ConsumedAtUtc, now), cancellationToken);
        if (consumed != 1)
        {
            return Result.Failure(Errors.InvalidInvitation);
        }

        var user = await users.FindByIdAsync(credential.UserId, cancellationToken);
        if (user is not { Status: UserStatus.Active })
        {
            return Result.Failure(Errors.InvalidInvitation);
        }

        await users.ResetPasswordAsync(user.Id, command.NewPassword, cancellationToken);
        await users.SetMustChangePasswordAsync(user.Id, false, cancellationToken);

        // Sign out everywhere, including the device that asked.
        await users.EndSessionAsync(user.Id, cancellationToken);
        await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
