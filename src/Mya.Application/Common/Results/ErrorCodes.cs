namespace Mya.Application.Common.Results;

/// <summary>
/// Stable wire codes. The business codes are exactly the list in docs/04-roadmap.md
/// "Error codes"; the transport codes cover framework-level failures so that every failure on
/// the wire is a ProblemDetails with a code.
/// </summary>
public static class ErrorCodes
{
    // --- business (docs/04-roadmap.md) ---
    public const string AccountPending = "ACCOUNT_PENDING";
    public const string AccountDeclined = "ACCOUNT_DECLINED";
    public const string AccountSuspended = "ACCOUNT_SUSPENDED";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string MustChangePassword = "MUST_CHANGE_PASSWORD";
    public const string CurrentPasswordWrong = "CURRENT_PASSWORD_WRONG";
    public const string EmailAlreadyExists = "EMAIL_ALREADY_EXISTS";
    public const string UserNotFound = "USER_NOT_FOUND";
    public const string UserNotPending = "USER_NOT_PENDING";
    public const string CannotDeleteSelf = "CANNOT_DELETE_SELF";
    public const string CannotModifySelf = "CANNOT_MODIFY_SELF";
    public const string CannotDeleteLastAdmin = "CANNOT_DELETE_LAST_ADMIN";
    public const string SessionSuperseded = "SESSION_SUPERSEDED";

    // --- transport (emitted by the host, not by handlers) ---
    public const string Unauthenticated = "UNAUTHENTICATED";
    public const string Forbidden = "FORBIDDEN";
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string RateLimited = "RATE_LIMITED";
    public const string InternalError = "INTERNAL_ERROR";
}
