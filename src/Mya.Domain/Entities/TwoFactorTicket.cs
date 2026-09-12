namespace Mya.Domain.Entities;

/// <summary>Server-side ticket for the email 2FA step (docs/03 section 4.2).</summary>
public sealed class TwoFactorTicket
{
    public Guid Id { get; init; }

    public string UserId { get; init; } = default!;

    public byte[] CodeHash { get; init; } = default!;

    public DateTime ExpiresAtUtc { get; init; }

    public int Attempts { get; set; }

    public DateTime? ConsumedAtUtc { get; set; }
}
