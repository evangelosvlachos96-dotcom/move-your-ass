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
            // Already rotated and presented again. Two tabs on one device legitimately do this:
            // both read the same cookie, both refresh, and the loser arrives a moment late. Inside
            // the grace window that is not an attack, so the session survives and the caller gets
            // an access token without rotating anything. Outside it, this is reuse.
            return await GraceAsync(presented, now, cancellationToken);
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
            // Lost the race to another presenter of the same token between the read above and
            // this conditional update. Same situation as the replaced branch, decided the same
            // way: re-read the row so the grace window sees when it was actually consumed.
            var raced = await db.RefreshTokens.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == presented.Id, cancellationToken);
            return raced is null
                ? Result.Failure<RefreshResult>(Errors.InvalidCredentials)
                : await GraceAsync(raced, now, cancellationToken);
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

    /// <summary>
    /// How long an already-rotated token still buys an access token instead of revoking the
    /// family. Two tabs refreshing together are milliseconds apart; a stolen token replayed later
    /// is not. Long enough to absorb a slow mobile round trip, short enough that a captured
    /// cookie is worth little.
    /// </summary>
    internal static readonly TimeSpan ReuseGrace = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Decides whether a token that has already been rotated is a racing second tab or genuine
    /// reuse. The session must still be alive and current either way: a grace replay can refresh
    /// an access token, never resurrect a session that logout, suspension or a newer login ended.
    /// </summary>
    private async Task<Result<RefreshResult>> GraceAsync(RefreshToken presented, DateTime now, CancellationToken cancellationToken)
    {
        var rotatedAt = presented.RevokedAtUtc;
        if (rotatedAt is null || now - rotatedAt > ReuseGrace)
        {
            await RevokeFamilyAsync(presented.FamilyId, now, cancellationToken);
            return Result.Failure<RefreshResult>(Errors.InvalidCredentials);
        }

        var user = await users.FindByIdAsync(presented.UserId, cancellationToken);
        if (user is null || user.Status is not UserStatus.Active)
        {
            return Result.Failure<RefreshResult>(Errors.InvalidCredentials);
        }

        if (user.ActiveSessionId is null)
        {
            return Result.Failure<RefreshResult>(Errors.InvalidCredentials);
        }

        if (user.ActiveSessionId != presented.SessionId)
        {
            return Result.Failure<RefreshResult>(Errors.SessionSuperseded);
        }

        // The replacement this token already produced is the live one, and the browser holds it.
        // Issue only an access token: rotating again would leave two live tokens in the family.
        var access = tokens.CreateAccessToken(user, presented.SessionId);
        return Result.Success(new RefreshResult(access.Token, access.ExpiresInSeconds, null, null));
    }

    private Task<int> RevokeFamilyAsync(Guid familyId, DateTime now, CancellationToken cancellationToken) =>
        db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), cancellationToken);
}
