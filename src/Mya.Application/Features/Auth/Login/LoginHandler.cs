using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Mya.Application.Abstractions.Identity;
using Mya.Application.Abstractions.Persistence;
using Mya.Application.Abstractions.System;
using Mya.Application.Common.Results;
using Mya.Application.Features.Common;
using Mya.Domain.Entities;
using Mya.Domain.Enums;

namespace Mya.Application.Features.Auth.Login;

/// <summary>
/// docs/03 section 4.2 without the 2FA step (ADR-013). A successful login starts a new single
/// active session: ActiveSessionId rotates and every earlier refresh token is revoked.
/// </summary>
public sealed class LoginHandler(
    IUserService users,
    ITokenService tokens,
    IAppDbContext db,
    IClock clock,
    ICurrentUser currentUser,
    IMapper mapper)
{
    public async Task<Result<LoginResult>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Unknown email and wrong password produce the same response: do not leak account existence.
        var user = await users.FindByEmailAsync(command.Email.Trim(), cancellationToken);
        if (user is null)
        {
            return Result.Failure<LoginResult>(Errors.InvalidCredentials);
        }

        var check = await users.CheckPasswordAsync(user.Id, command.Password, cancellationToken);
        if (check is not PasswordCheckOutcome.Success)
        {
            return Result.Failure<LoginResult>(Errors.InvalidCredentials);
        }

        if (user.Status is not UserStatus.Active)
        {
            return Result.Failure<LoginResult>(Errors.ForInactiveStatus(user.Status));
        }

        var now = clock.UtcNow;

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        var sessionId = await users.StartSessionAsync(user.Id, currentUser.UserAgent, cancellationToken);

        await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), cancellationToken);

        var refresh = tokens.CreateRefreshToken();
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refresh.Hash,
            SessionId = sessionId,
            FamilyId = Guid.NewGuid(),
            CreatedAtUtc = now,
            ExpiresAtUtc = refresh.ExpiresAtUtc,
            CreatedByIp = currentUser.IpAddress,
        });

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var signedIn = user with { ActiveSessionId = sessionId };
        var access = tokens.CreateAccessToken(signedIn, sessionId);

        return Result.Success(new LoginResult(
            access.Token,
            access.ExpiresInSeconds,
            mapper.Map<UserDto>(signedIn),
            refresh.RawToken,
            refresh.ExpiresAtUtc));
    }
}
