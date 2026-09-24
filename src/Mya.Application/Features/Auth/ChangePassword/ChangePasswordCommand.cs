namespace Mya.Application.Features.Auth.ChangePassword;

/// <summary>
/// <paramref name="CurrentPassword"/> may be omitted only for a forced change (the handler checks
/// <c>MustChangePassword</c> in the database, never a flag in the body). A voluntary change from
/// the user menu always requires it: that is what protects an unattended session.
/// </summary>
public sealed record ChangePasswordCommand(string? CurrentPassword, string NewPassword);
