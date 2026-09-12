using Microsoft.EntityFrameworkCore;
using Mya.Application.Abstractions.Identity;
using Mya.Application.Abstractions.Persistence;
using Mya.Application.Abstractions.System;
using Mya.Application.Common.Results;
using Mya.Domain.Entities;
using Mya.Domain.Enums;

namespace Mya.Application.Features.Auth.Refresh;

/// <summary>
/// docs/03 section 4.3: rotation with reuse detection. Unknown, expired, reused and
/// logged-out tokens all get the vague 401 INVALID_CREDENTIALS on purpose. Two refusals are
/// specific: an inactive account reports its status, and a token from a session that a newer
/// login replaced gets SESSION_SUPERSEDED, so the SPA can say "you were signed out because you
/// logged in elsewhere". Consuming the presented token is one conditional UPDATE, so two
/// concurrent presenters cannot both win.
/// </summary>
public sealed class RefreshHandler(
    IUserService users,
    ITokenService tokens,
    IAppDbContext db,
    IClock clock,
    ICurrentUser currentUser)
{
    public async Task<Result<RefreshResult>> Handle(RefreshCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return Result.Failure<RefreshResult>(Errors.InvalidCredentials);
        }

        var hash = tokens.HashRefreshToken(command.RefreshToken);
        var presented = await db.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (presented is null)
        {
            return Result.Failure<RefreshResult>(Errors.InvalidCredentials);
        }

        var now = clock.UtcNow;

        if (presented.ReplacedByTokenId is not null)
        {
            // Already rotated and presented again: one of the two presenters is an attacker.
            await RevokeFamilyAsync(presented.FamilyId, now, cancellationToken);
            return Result.Failure<RefreshResult>(Errors.InvalidCredentials);
        }

        if (presented.ExpiresAtUtc <= now)
        {
            return Result.Failure<RefreshResult>(Errors.InvalidCredentials);
        }

        var user = await users.FindByIdAsync(presented.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<RefreshResult>(Errors.InvalidCredentials);
        }

        if (user.Status is not UserStatus.Active)
        {
            return Result.Failure<RefreshResult>(
                Errors.ForInactiveStatus(user.Status) with { Status = ResultStatus.Unauthorized });
        }

        if (user.ActiveSessionId is { } activeSession && activeSession != presented.SessionId)
        {
            // A newer login on another device replaced this session (docs/03 section 5.4, lazy mode).
            // Login already revoked this token, which is why this check runs before the revoked one.
            return Result.Failure<RefreshResult>(Errors.SessionSuperseded);
        }

        if (presented.RevokedAtUtc is not null || user.ActiveSessionId is null)
        {
            // Revoked by logout, suspension or an admin password reset: no session to continue.
            return Result.Failure<RefreshResult>(Errors.InvalidCredentials);
        }

        var replacementId = Guid.NewGuid();
        var consumed = await db.RefreshTokens
            .Where(t => t.Id == presented.Id && t.RevokedAtUtc == null && t.ReplacedByTokenId == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(t => t.RevokedAtUtc, now).SetProperty(t => t.ReplacedByTokenId, replacementId),
                cancellationToken);

        if (consumed == 0)
        {
            // Lost a race with another presenter of the same token: treat as reuse.
            await RevokeFamilyAsync(presented.FamilyId, now, cancellationToken);
            return Result.Failure<RefreshResult>(Errors.InvalidCredentials);
        }

        var refresh = tokens.CreateRefreshToken();
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = replacementId,
            UserId = user.Id,
            TokenHash = refresh.Hash,
            SessionId = presented.SessionId,
            FamilyId = presented.FamilyId,
            CreatedAtUtc = now,
            ExpiresAtUtc = refresh.ExpiresAtUtc,
            CreatedByIp = currentUser.IpAddress,
        });
        await db.SaveChangesAsync(cancellationToken);

        var access = tokens.CreateAccessToken(user, presented.SessionId);
        return Result.Success(new RefreshResult(access.Token, access.ExpiresInSeconds, refresh.RawToken, refresh.ExpiresAtUtc));
    }

    private Task<int> RevokeFamilyAsync(Guid familyId, DateTime now, CancellationToken cancellationToken) =>
        db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), cancellationToken);
}
