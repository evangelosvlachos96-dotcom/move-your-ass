namespace Mya.Api.Authorization;

public static class Policies
{
    public const string AdminOnly = "AdminOnly";
}

public static class RateLimitPolicies
{
    /// <summary>5 attempts per email per 15 minutes on login and register (docs/03 section 4.2).</summary>
    public const string AuthPerEmail = "auth-per-email";
}

/// <summary>
/// Marks the few endpoints a user may call while MustChangePassword is set:
/// /auth/me, /auth/change-password and /auth/logout (docs/04-roadmap.md).
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class AllowWhilePasswordChangeRequiredAttribute : Attribute
{
}
