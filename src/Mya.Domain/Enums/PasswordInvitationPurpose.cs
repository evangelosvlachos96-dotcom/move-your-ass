namespace Mya.Domain.Enums;

/// <summary>
/// Why a password credential was issued. Persisted as an integer, so existing rows keep
/// <see cref="Invitation"/> — every row that existed before self-service reset was one.
/// </summary>
public enum PasswordInvitationPurpose
{
    /// <summary>Activates an Invited account and sets its first password.</summary>
    Invitation = 0,

    /// <summary>Re-passwords an existing account after a forgotten-password request.</summary>
    Reset = 1,
}
