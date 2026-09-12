namespace Mya.Application.Features.Auth.Refresh;

/// <summary>The raw token from the refresh cookie; null when the cookie is absent.</summary>
public sealed record RefreshCommand(string? RefreshToken);

public sealed record RefreshResult(
    string AccessToken,
    int ExpiresIn,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);

public sealed record RefreshResponse(string AccessToken, int ExpiresIn);
