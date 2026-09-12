using Mya.Domain.Enums;

namespace Mya.Application.Abstractions.Identity;

/// <summary>
/// Read model of a user as the application layer sees it. The Identity entity itself lives in
/// Infrastructure because it derives from an ASP.NET type.
/// </summary>
public sealed record UserAccount(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    UserStatus Status,
    bool MustChangePassword,
    Guid? ActiveSessionId,
    string SecurityStamp,
    DateTime CreatedAtUtc,
    DateTime? ApprovedAtUtc,
    DateTime? SuspendedAtUtc,
    string? SuspensionReason);

public sealed record NewUserAccount(
    string Email,
    string FirstName,
    string LastName,
    string Role,
    UserStatus Status,
    bool MustChangePassword);

public sealed record UserListFilter(UserStatus? Status, string? Search, int Page, int PageSize);

public enum PasswordCheckOutcome
{
    Success,
    Invalid,
    LockedOut,
}
