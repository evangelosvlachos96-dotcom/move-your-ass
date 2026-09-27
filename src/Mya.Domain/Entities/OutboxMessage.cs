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

    /// <summary>
    /// The account this message is about, when there is one. Deliberately not a foreign key: the
    /// outbox must not gain a cascade that deletes history, and messages addressed to somebody
    /// else about this user (the admin's new-registration notice) carry no subject at all.
    ///
    /// It exists so a hard delete can withdraw the messages that have not gone out yet. Approving
    /// and deleting an account race in the admin UI; without this, the loser leaves "your account
    /// was approved" queued to an account that no longer exists, and a password link that can
    /// never work.
    /// </summary>
    public string? SubjectUserId { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? ProcessedAtUtc { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    public DateTime? LockedUntilUtc { get; set; }
}
