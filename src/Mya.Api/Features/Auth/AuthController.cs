using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mya.Api.Authorization;
using Mya.Api.Extensions;
using Mya.Application.Features.Auth.ChangePassword;
using Mya.Application.Features.Auth.AcceptInvitation;
using Mya.Application.Features.Auth.ForgotPassword;
using Mya.Application.Features.Auth.Login;
using Mya.Application.Features.Auth.Logout;
using Mya.Application.Features.Auth.Me;
using Mya.Application.Features.Auth.Refresh;
using Mya.Application.Features.Auth.Register;
using Mya.Application.Features.Auth.UpdateProfile;

namespace Mya.Api.Features.Auth;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    AcceptInvitationHandler acceptInvitation,
    RegisterHandler register,
    LoginHandler login,
    RefreshHandler refresh,
    GetMeHandler me,
    ChangePasswordHandler changePassword,
    UpdateProfileHandler updateProfile,
    LogoutHandler logout,
    ForgotPasswordHandler forgotPassword,
    ResetPasswordHandler resetPassword) : ControllerBase
{
    [HttpPost("accept-invitation")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthPerEmail)]
    public async Task<IActionResult> AcceptInvitation(AcceptInvitationCommand command, CancellationToken ct) =>
        (await acceptInvitation.Handle(command, ct)).ToActionResult(HttpContext);

    /// <summary>
    /// Always 204, whether or not the address exists: anything else is an enumeration oracle.
    /// Rate-limited per email like login, which is what actually stops someone probing.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthPerEmail)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordCommand command, CancellationToken ct) =>
        (await forgotPassword.Handle(command, ct)).ToActionResult(HttpContext);

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthPerEmail)]
    public async Task<IActionResult> ResetPassword(ResetPasswordCommand command, CancellationToken ct) =>
        (await resetPassword.Handle(command, ct)).ToActionResult(HttpContext);

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthPerEmail)]
    public async Task<IActionResult> Register(RegisterCommand command, CancellationToken ct) =>
        (await register.Handle(command, ct)).ToActionResult(HttpContext, response => Accepted(response));

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthPerEmail)]
    public async Task<IActionResult> Login(LoginCommand command, CancellationToken ct) =>
        (await login.Handle(command, ct)).ToActionResult(HttpContext, result =>
        {
            RefreshCookie.Set(Response, result.RefreshToken, result.RefreshTokenExpiresAtUtc);
            return Ok(new LoginResponse(result.AccessToken, result.ExpiresIn, result.User));
        });

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(CancellationToken ct) =>
        (await refresh.Handle(new RefreshCommand(Request.Cookies[RefreshCookie.Name]), ct)).ToActionResult(HttpContext, result =>
        {
            // No new token means another tab on this device rotated first; the browser already
            // holds the newer cookie, so overwriting it here would replace it with nothing.
            if (result.RefreshToken is { } token && result.RefreshTokenExpiresAtUtc is { } expires)
            {
                RefreshCookie.Set(Response, token, expires);
            }

            return Ok(new RefreshResponse(result.AccessToken, result.ExpiresIn));
        });

    [HttpGet("me")]
    [Authorize]
    [AllowWhilePasswordChangeRequired]
    public async Task<IActionResult> Me(CancellationToken ct) =>
        (await me.Handle(ct)).ToActionResult(HttpContext, user => Ok(user));

    [HttpPost("change-password")]
    [Authorize]
    [AllowWhilePasswordChangeRequired]
    public async Task<IActionResult> ChangePassword(ChangePasswordCommand command, CancellationToken ct) =>
        (await changePassword.Handle(command, ct)).ToActionResult(HttpContext);

    [HttpPut("profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile(UpdateProfileCommand command, CancellationToken ct) =>
        (await updateProfile.Handle(command, ct)).ToActionResult(HttpContext);

    [HttpPost("logout")]
    [Authorize]
    [AllowWhilePasswordChangeRequired]
    public async Task<IActionResult> Logout(CancellationToken ct) =>
        (await logout.Handle(ct)).ToActionResult(HttpContext, () =>
        {
            RefreshCookie.Clear(Response);
            return Mya.Api.Http.ApiSuccess.Create(HttpContext);
        });
}
