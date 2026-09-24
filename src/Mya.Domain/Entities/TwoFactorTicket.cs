namespace Mya.Domain.Entities;

/// <summary>Unused legacy email 2FA ticket; retained with existing migrations (ADR-013).</summary>
public sealed class TwoFactorTicket
{
    public Guid Id { get; init; }

    public string UserId { get; init; } = default!;

    public byte[] CodeHash { get; init; } = default!;

    public DateTime ExpiresAtUtc { get; init; }

    public int Attempts { get; set; }

    public DateTime? ConsumedAtUtc { get; set; }
}
