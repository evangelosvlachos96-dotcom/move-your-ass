namespace Mya.Application.Common.Results;

/// <summary>The one place a code is paired with its HTTP semantics. Handlers return these.</summary>
public static class Errors
{
    public static readonly Error AccountPending =
        new(ErrorCodes.AccountPending, "Account pending approval", ResultStatus.Forbidden);

    public static readonly Error AccountDeclined =
        new(ErrorCodes.AccountDeclined, "Registration was declined", ResultStatus.Forbidden);

    public static readonly Error AccountSuspended =
        new(ErrorCodes.AccountSuspended, "Account suspended", ResultStatus.Forbidden);

    public static readonly Error InvalidCredentials =
        new(ErrorCodes.InvalidCredentials, "Invalid credentials", ResultStatus.Unauthorized);

    public static readonly Error MustChangePassword =
        new(ErrorCodes.MustChangePassword, "Password change required", ResultStatus.Forbidden);

    public static readonly Error CurrentPasswordWrong =
        new(ErrorCodes.CurrentPasswordWrong, "Current password is wrong", ResultStatus.Invalid);

    public static readonly Error EmailAlreadyExists =
        new(ErrorCodes.EmailAlreadyExists, "Email already registered", ResultStatus.Conflict);

    public static readonly Error UserNotFound =
        new(ErrorCodes.UserNotFound, "User not found", ResultStatus.NotFound);

    public static readonly Error UserNotPending =
        new(ErrorCodes.UserNotPending, "User is not pending approval", ResultStatus.Conflict);

    public static readonly Error CannotDeleteSelf =
        new(ErrorCodes.CannotDeleteSelf, "You cannot delete your own account", ResultStatus.Conflict);

    public static readonly Error CannotModifySelf =
        new(ErrorCodes.CannotModifySelf, "You cannot suspend your own account", ResultStatus.Conflict);

    /// <summary>docs/03 section 4.3: the refresh token belongs to a session a newer login replaced.</summary>
    public static readonly Error SessionSuperseded =
        new(ErrorCodes.SessionSuperseded, "Signed out because this account logged in on another device", ResultStatus.Unauthorized);

    public static readonly Error CannotDeleteLastAdmin =
        new(ErrorCodes.CannotDeleteLastAdmin, "The last remaining Admin cannot be removed", ResultStatus.Conflict);

    public static readonly Error ValidationFailed =
        new(ErrorCodes.ValidationFailed, "Validation failed", ResultStatus.Invalid);

    /// <summary>Login and refresh refuse any non-active account with the code for its status.</summary>
    public static Error ForInactiveStatus(Domain.Enums.UserStatus status) => status switch
    {
        Domain.Enums.UserStatus.PendingApproval => AccountPending,
        Domain.Enums.UserStatus.Declined => AccountDeclined,
        Domain.Enums.UserStatus.Suspended => AccountSuspended,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Status is not an inactive status."),
    };
}
