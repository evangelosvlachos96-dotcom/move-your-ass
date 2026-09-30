namespace Mya.Api.Authorization;

public static class Policies
{
    public const string AdminOnly = "AdminOnly";
}

public static class RateLimitPolicies
{
    /// <summary>5 attempts per (email, IP) per 15 minutes on the credential endpoints.</summary>
    public const string AuthPerEmail = "auth-per-email";

    /// <summary>
    /// Outbound actions a signed-in user can repeat: the contact form and the admin test email.
    /// Keyed by account rather than address, so a household behind one IP is not one allowance.
    /// </summary>
    public const string PerUserWrite = "per-user-write";
}

/// <summary>
/// Marks the few endpoints a user may call while MustChangePassword is set:
/// /auth/me, /auth/change-password and /auth/logout (docs/04-roadmap.md).
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class AllowWhilePasswordChangeRequiredAttribute : Attribute
{
}
