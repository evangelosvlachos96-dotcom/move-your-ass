using System.ComponentModel.DataAnnotations;

namespace Mya.Infrastructure.Identity;

/// <summary>
/// Bound from the "Jwt" section. The signing key comes from user-secrets locally and Key Vault in
/// Azure (docs/03 section 2); the lifetimes are the ones fixed in docs/03 and are not secrets.
/// </summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    /// <summary>HS256 needs at least 256 bits; 32 characters of a random key is the floor.</summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Range(1, 120)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(1, 90)]
    public int RefreshTokenDays { get; set; } = 14;
}
