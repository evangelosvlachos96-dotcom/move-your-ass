namespace Mya.Domain.Entities;

/// <summary>
/// Outbound side effect written in the same transaction as the state change that caused it.
/// Dispatched by a background service (docs/02 section 8).
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; }

    public string Type { get; init; } = default!;

    public string PayloadJson { get; init; } = default!;

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? ProcessedAtUtc { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    public DateTime? LockedUntilUtc { get; set; }
}
