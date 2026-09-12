using Microsoft.AspNetCore.Identity;
using Mya.Domain.Enums;

namespace Mya.Infrastructure.Identity;

/// <summary>
/// The Identity user plus the platform columns (docs/04-roadmap.md "Schema changes"). Lives in
/// Infrastructure because IdentityUser is an ASP.NET type and Domain references nothing.
/// </summary>
public sealed class AppUser : IdentityUser
{
    public string FirstName { get; set; } = default!;

    public string LastName { get; set; } = default!;

    public UserStatus Status { get; set; } = UserStatus.PendingApproval;

    /// <summary>Set for admin-created accounts and after an admin password reset; cleared by change-password.</summary>
    public bool MustChangePassword { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    public string? ApprovedByUserId { get; set; }

    public DateTime? SuspendedAtUtc { get; set; }

    public string? SuspensionReason { get; set; }

    public Guid? ActiveSessionId { get; set; }

    public DateTime? ActiveSessionStartedAtUtc { get; set; }

    public string? ActiveSessionUserAgent { get; set; }
}
