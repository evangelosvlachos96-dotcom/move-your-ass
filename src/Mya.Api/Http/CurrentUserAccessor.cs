using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Mya.Application.Abstractions.System;

namespace Mya.Api.Http;

/// <summary>Reads the caller from the validated JWT. Inbound claim mapping is off, so "sub" is "sub".</summary>
public sealed class CurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private HttpContext? Http => httpContextAccessor.HttpContext;

    public string? UserId => Http?.User.FindFirstValue(JwtRegisteredClaimNames.Sub);

    public string? UserAgent => Http?.Request.Headers.UserAgent.ToString() is { Length: > 0 } agent ? agent : null;

    public string? IpAddress => Http?.Connection.RemoteIpAddress?.ToString();
}
