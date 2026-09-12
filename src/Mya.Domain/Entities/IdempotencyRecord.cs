namespace Mya.Domain.Entities;

/// <summary>Stored outcome of a mutating request, replayed on a repeated Idempotency-Key (docs/03 section 5.2).</summary>
public sealed class IdempotencyRecord
{
    public Guid Id { get; init; }

    public string UserId { get; init; } = default!;

    public string Key { get; init; } = default!;

    public int StatusCode { get; init; }

    public string ResponseJson { get; init; } = default!;

    public DateTime CreatedAtUtc { get; init; }

    public DateTime ExpiresAtUtc { get; init; }
}
