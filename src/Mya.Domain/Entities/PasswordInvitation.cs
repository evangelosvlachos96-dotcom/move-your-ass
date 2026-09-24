namespace Mya.Domain.Entities;

/// <summary>Expiring, single-use password setup credential. Only its hash is stored here.</summary>
public sealed class PasswordInvitation
{
    public Guid Id { get; init; }
    public string UserId { get; init; } = default!;
    public byte[] TokenHash { get; init; } = default!;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
    public DateTime? ConsumedAtUtc { get; set; }
}
