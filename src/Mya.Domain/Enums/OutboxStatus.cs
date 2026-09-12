namespace Mya.Domain.Enums;

/// <summary>
/// Logical state of an <see cref="Entities.OutboxMessage"/>. Not a column in the phase 1 schema;
/// derived from ProcessedAtUtc and Attempts by the dispatcher (docs/02 section 8).
/// </summary>
public enum OutboxStatus
{
    Pending = 0,
    Processed = 1,
    DeadLettered = 2,
}
