using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Mya.Application.Abstractions.Identity;
using Mya.Application.Abstractions.System;
using Mya.Application.Common.Security;

namespace Mya.Infrastructure.Identity;

/// <summary>
/// Access token: HS256 JWT with sub, email, role, sid, stamp (docs/03 section 2) plus a
/// must_change_password marker the host enforces. Refresh token: 256 random bits, Base64Url,
/// stored only as its SHA-256.
/// </summary>
public sealed class JwtTokenService(IOptions<JwtSettings> options, IClock clock) : ITokenService
{
    private const int RefreshTokenBytes = 32;
    private static readonly JsonWebTokenHandler Handler = new();

    private readonly JwtSettings _settings = options.Value;

    public AccessToken CreateAccessToken(UserAccount user, Guid sessionId)
    {
        ArgumentNullException.ThrowIfNull(user);

        var now = clock.UtcNow;
        var expires = now.AddMinutes(_settings.AccessTokenMinutes);

        var claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = user.Id,
            [JwtRegisteredClaimNames.Email] = user.Email,
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N"),
            [AuthClaims.Role] = user.Role,
            [AuthClaims.SessionId] = sessionId.ToString(),
            [AuthClaims.SecurityStamp] = user.SecurityStamp,
        };

        if (user.MustChangePassword)
        {
            claims[AuthClaims.MustChangePassword] = "true";
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            Claims = claims,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey)),
                SecurityAlgorithms.HmacSha256),
        };

        return new AccessToken(Handler.CreateToken(descriptor), (int)(expires - now).TotalSeconds);
    }

    public RefreshTokenMaterial CreateRefreshToken()
    {
        var raw = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(RefreshTokenBytes));
        return new RefreshTokenMaterial(raw, HashRefreshToken(raw), clock.UtcNow.AddDays(_settings.RefreshTokenDays));
    }

    public byte[] HashRefreshToken(string rawToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(rawToken);
        return SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
    }
}
