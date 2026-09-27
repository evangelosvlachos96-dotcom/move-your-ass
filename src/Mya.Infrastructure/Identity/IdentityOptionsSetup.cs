using Microsoft.AspNetCore.Identity;

namespace Mya.Infrastructure.Identity;

/// <summary>Password and lockout policy from the docs/03 section 7 checklist.</summary>
public static class IdentityOptionsSetup
{
    private const int MinPasswordLength = 10;

    public static void Configure(IdentityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.User.RequireUniqueEmail = true;

        options.Password.RequiredLength = MinPasswordLength;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = false;

        // Account lockout is deliberately OFF. Counting failures per account and locking it makes
        // the account the thing an attacker can damage: anyone who knows the trainer's email
        // could lock her out of her own platform for fifteen minutes at a time, from anywhere,
        // without ever having to guess a password. That is a denial-of-service handed to the
        // internet in exchange for protection that throttling already provides.
        //
        // Online guessing is instead throttled per (email, IP) with a progressive delay, plus a
        // looser per-email backstop for distributed attempts — see AddAuthRateLimiting in
        // Mya.Api. Throttling slows the attacker's own connection rather than disabling the
        // victim's account, and the password policy below is what makes guessing impractical.
        options.Lockout.AllowedForNewUsers = false;

        // No client email-confirmation step in this product: registration notifies the admin,
        // who approves. Accounts are created with EmailConfirmed = true.
        options.SignIn.RequireConfirmedEmail = false;
    }
}
