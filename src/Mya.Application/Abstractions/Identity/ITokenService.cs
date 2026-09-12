namespace Mya.Application.Abstractions.Identity;

public sealed record AccessToken(string Token, int ExpiresInSeconds);

/// <summary>The raw token goes to the client once; only the hash is ever stored.</summary>
public sealed record RefreshTokenMaterial(string RawToken, byte[] Hash, DateTime ExpiresAtUtc);

public interface ITokenService
{
    public AccessToken CreateAccessToken(UserAccount user, Guid sessionId);

    public RefreshTokenMaterial CreateRefreshToken();

    public byte[] HashRefreshToken(string rawToken);
}
