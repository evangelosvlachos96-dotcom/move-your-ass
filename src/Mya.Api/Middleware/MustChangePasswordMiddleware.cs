using Microsoft.AspNetCore.Authorization;
using Mya.Api.Authorization;
using Mya.Api.Http;
using Mya.Application.Common.Results;
using Mya.Application.Common.Security;

namespace Mya.Api.Middleware;

/// <summary>
/// Server-side enforcement of the forced password change. The access token carries a
/// must_change_password claim set at issue time; while it is present, every authenticated
/// endpoint that is not explicitly allowed returns 403 MUST_CHANGE_PASSWORD. After changing the
/// password the client refreshes once and receives a token without the claim.
/// </summary>
public sealed class MustChangePasswordMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (RequiresPasswordChange(context) && !IsAllowed(context))
        {
            await ApiProblems.WriteAsync(
                context,
                StatusCodes.Status403Forbidden,
                ErrorCodes.MustChangePassword,
                Errors.MustChangePassword.Title);
            return;
        }

        await next(context);
    }

    private static bool RequiresPasswordChange(HttpContext context) =>
        context.User.Identity?.IsAuthenticated == true
        && context.User.HasClaim(AuthClaims.MustChangePassword, "true");

    private static bool IsAllowed(HttpContext context)
    {
        var metadata = context.GetEndpoint()?.Metadata;
        return metadata is null
            || metadata.GetMetadata<IAllowAnonymous>() is not null
            || metadata.GetMetadata<AllowWhilePasswordChangeRequiredAttribute>() is not null;
    }
}
