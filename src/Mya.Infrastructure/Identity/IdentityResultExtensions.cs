using Microsoft.AspNetCore.Identity;

namespace Mya.Infrastructure.Identity;

internal static class IdentityResultExtensions
{
    /// <summary>For operations that cannot legitimately fail once the handler has validated its inputs.</summary>
    public static void ThrowIfFailed(this IdentityResult result, string action)
    {
        if (result.Succeeded)
        {
            return;
        }

        throw new InvalidOperationException($"Identity failed to {action}: {result.Describe()}");
    }

    /// <summary>
    /// True when the update went through, false when it lost a race, and an exception for
    /// anything else.
    ///
    /// Identity guards every user row with a concurrency stamp. Two admins acting on the same
    /// registration at the same moment — one approving, one declining — is a race the stamp is
    /// there to catch, so the loser is an ordinary outcome to report, not a bug to throw over.
    /// Every other Identity failure still throws: those really do mean the code asked for
    /// something impossible.
    /// </summary>
    public static bool SucceededOrLostRace(this IdentityResult result, string action)
    {
        if (result.Succeeded)
        {
            return true;
        }

        if (result.Errors.Any(e => e.Code == ConcurrencyFailureCode))
        {
            return false;
        }

        throw new InvalidOperationException($"Identity failed to {action}: {result.Describe()}");
    }

    /// <summary>The code IdentityErrorDescriber gives a stamp mismatch. Not worth a magic string.</summary>
    private const string ConcurrencyFailureCode = "ConcurrencyFailure";

    public static string Describe(this IdentityResult result) =>
        string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
}
