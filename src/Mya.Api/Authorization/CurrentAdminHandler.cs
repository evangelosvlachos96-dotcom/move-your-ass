using Microsoft.AspNetCore.Authorization;
using Mya.Application.Abstractions.Identity;
using Mya.Application.Abstractions.System;
using Mya.Application.Common.Security;
using Mya.Domain.Constants;
using Mya.Domain.Enums;

namespace Mya.Api.Authorization;

/// <summary>
/// Requires that the caller is an Admin <em>now</em>, not merely that their access token says so.
/// </summary>
public sealed class CurrentAdminRequirement : IAuthorizationRequirement;

/// <summary>
/// Re-reads the caller from the database on every admin request and checks role, account status
/// and session against the token's claims.
///
/// The access token carries the role and lives fifteen minutes, so without this a demoted or
/// suspended admin would keep full admin access for up to a quarter of an hour — which is exactly
/// the window during which someone's access is most likely to have been revoked deliberately.
///
/// This is scoped to admin endpoints on purpose. Doing it for every authenticated request is the
/// "instant JWT revocation" item that docs/03 and the backlog deliberately defer: it would add a
/// database read to every call for the whole client base. Admin traffic is one trainer, so the
/// cost here is negligible and the property is worth having.
/// </summary>
public sealed class CurrentAdminHandler(IUserService users, ICurrentUser currentUser)
    : AuthorizationHandler<CurrentAdminRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CurrentAdminRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (currentUser.UserId is not { Length: > 0 } userId)
        {
            return;
        }

        var user = await users.FindByIdAsync(userId, CancellationToken.None);
        if (user is null || user.Role != Roles.Admin || user.Status is not UserStatus.Active)
        {
            return;
        }

        // A token from a session a newer login replaced must not act as an admin either.
        var session = context.User.FindFirst(AuthClaims.SessionId)?.Value;
        if (user.ActiveSessionId is null
            || !Guid.TryParse(session, out var presented)
            || presented != user.ActiveSessionId)
        {
            return;
        }

        context.Succeed(requirement);
    }
}
