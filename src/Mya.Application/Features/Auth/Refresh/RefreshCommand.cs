namespace Mya.Application.Features.Auth.Refresh;

/// <summary>The raw token from the refresh cookie; null when the cookie is absent.</summary>
public sealed record RefreshCommand(string? RefreshToken);

/// <summary>
/// A successful refresh. <see cref="RefreshToken"/> is null when the caller presented a token
/// that had just been rotated by another tab on the same device: the session is genuinely alive
/// and gets a fresh access token, but nothing is rotated and the cookie is left alone — the
/// browser already holds the newer one that the other tab's response set.
/// </summary>
public sealed record RefreshResult(
    string AccessToken,
    int ExpiresIn,
    string? RefreshToken,
    DateTime? RefreshTokenExpiresAtUtc);

public sealed record RefreshResponse(string AccessToken, int ExpiresIn);
