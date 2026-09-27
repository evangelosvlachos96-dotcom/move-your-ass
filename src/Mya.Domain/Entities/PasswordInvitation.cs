using Mya.Domain.Enums;

namespace Mya.Domain.Entities;

/// <summary>Expiring, single-use password setup credential. Only its hash is stored here.</summary>
public sealed class PasswordInvitation
{
    public Guid Id { get; init; }
    public string UserId { get; init; } = default!;

    /// <summary>
    /// What this credential is for. The two flows are deliberately separate: an invitation
    /// activates an Invited account, a reset re-passwords an Active one, and a token issued for
    /// one must not work for the other.
    /// </summary>
    public PasswordInvitationPurpose Purpose { get; init; }
    public byte[] TokenHash { get; init; } = default!;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
    public DateTime? ConsumedAtUtc { get; set; }
}
