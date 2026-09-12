using Mya.Application.Features.Common;

namespace Mya.Application.Features.Auth.Login;

public sealed record LoginCommand(string Email, string Password);

/// <summary>
/// The controller puts <see cref="RefreshToken"/> in the HttpOnly cookie and returns the rest
/// as the body. The raw refresh token never appears in a response body.
/// </summary>
public sealed record LoginResult(
    string AccessToken,
    int ExpiresIn,
    UserDto User,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);

public sealed record LoginResponse(string AccessToken, int ExpiresIn, UserDto User);
