using Microsoft.AspNetCore.Identity;

namespace Mya.Infrastructure.Identity;

/// <summary>Password and lockout policy from the docs/03 section 7 checklist.</summary>
public static class IdentityOptionsSetup
{
    private const int MinPasswordLength = 10;
    private const int MaxFailedAccessAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public static void Configure(IdentityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.User.RequireUniqueEmail = true;

        options.Password.RequiredLength = MinPasswordLength;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = false;

        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = MaxFailedAccessAttempts;
        options.Lockout.DefaultLockoutTimeSpan = LockoutDuration;

        // No client email-confirmation step in this product: registration notifies the admin,
        // who approves. Accounts are created with EmailConfirmed = true.
        options.SignIn.RequireConfirmedEmail = false;
    }
}
