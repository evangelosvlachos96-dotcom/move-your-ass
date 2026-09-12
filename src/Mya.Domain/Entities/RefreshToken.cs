namespace Mya.Domain.Entities;

/// <summary>Rotating refresh token. The raw token is never stored, only its SHA-256 hash.</summary>
public sealed class RefreshToken
{
    public Guid Id { get; init; }

    public string UserId { get; init; } = default!;

    public byte[] TokenHash { get; init; } = default!;

    public Guid SessionId { get; init; }

    public Guid FamilyId { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime ExpiresAtUtc { get; init; }

    public DateTime? RevokedAtUtc { get; set; }

    public Guid? ReplacedByTokenId { get; set; }

    public string? CreatedByIp { get; init; }
}
