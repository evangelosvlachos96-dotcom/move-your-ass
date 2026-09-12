using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mya.Api.Authorization;
using Mya.Api.Extensions;
using Mya.Application.Features.Auth.ChangePassword;
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
    RegisterHandler register,
    LoginHandler login,
    RefreshHandler refresh,
    GetMeHandler me,
    ChangePasswordHandler changePassword,
    UpdateProfileHandler updateProfile,
    LogoutHandler logout) : ControllerBase
{
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
            RefreshCookie.Set(Response, result.RefreshToken, result.RefreshTokenExpiresAtUtc);
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
        (await changePassword.Handle(command, ct)).ToActionResult(HttpContext, NoContent);

    [HttpPut("profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile(UpdateProfileCommand command, CancellationToken ct) =>
        (await updateProfile.Handle(command, ct)).ToActionResult(HttpContext, NoContent);

    [HttpPost("logout")]
    [Authorize]
    [AllowWhilePasswordChangeRequired]
    public async Task<IActionResult> Logout(CancellationToken ct) =>
        (await logout.Handle(ct)).ToActionResult(HttpContext, () =>
        {
            RefreshCookie.Clear(Response);
            return NoContent();
        });
}
