using Mya.Application.Common.Paging;
using Mya.Application.Common.Results;
using Mya.Domain.Enums;

namespace Mya.Application.Abstractions.Identity;

/// <summary>
/// Thin seam over the Identity user store. It persists state; it decides nothing. Business rules
/// (who may be deleted, which transitions are legal) live in the handlers.
/// </summary>
public interface IUserService
{
    public Task<UserAccount?> FindByIdAsync(string userId, CancellationToken cancellationToken);

    public Task<UserAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    public Task<PagedResult<UserAccount>> ListAsync(UserListFilter filter, CancellationToken cancellationToken);

    public Task<int> CountInRoleAsync(string role, CancellationToken cancellationToken);

    public Task<IReadOnlyList<string>> GetEmailsInRoleAsync(string role, CancellationToken cancellationToken);

    /// <summary>Fails with EMAIL_ALREADY_EXISTS when the unique index rejects the email.</summary>
    public Task<Result<UserAccount>> CreateAsync(NewUserAccount account, string password, CancellationToken cancellationToken);

    /// <summary>
    /// Re-registration of a Declined account: new names and password, back to PendingApproval,
    /// decline traces and lockout cleared. Throws if the account is not Declined.
    /// </summary>
    public Task<UserAccount> ReRegisterDeclinedAsync(string userId, string firstName, string lastName, string password, CancellationToken cancellationToken);

    /// <summary>Lockout-aware: counts failures and reports a locked account as such.</summary>
    public Task<PasswordCheckOutcome> CheckPasswordAsync(string userId, string password, CancellationToken cancellationToken);

    /// <summary>Fails with CURRENT_PASSWORD_WRONG when the current password does not match.</summary>
    public Task<Result> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken);

    public Task ResetPasswordAsync(string userId, string newPassword, CancellationToken cancellationToken);

    public Task UpdateNamesAsync(string userId, string firstName, string lastName, CancellationToken cancellationToken);

    public Task SetRoleAsync(string userId, string role, CancellationToken cancellationToken);

    public Task SetStatusAsync(string userId, UserStatus status, string? reason, string? actorUserId, CancellationToken cancellationToken);

    public Task SetMustChangePasswordAsync(string userId, bool mustChangePassword, CancellationToken cancellationToken);

    /// <summary>Rotates ActiveSessionId (docs/03 section 5.4) and returns the new session id.</summary>
    public Task<Guid> StartSessionAsync(string userId, string? userAgent, CancellationToken cancellationToken);

    /// <summary>Clears the active session and regenerates the security stamp.</summary>
    public Task EndSessionAsync(string userId, CancellationToken cancellationToken);

    public Task DeleteAsync(string userId, CancellationToken cancellationToken);
}
