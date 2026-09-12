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

    public static string Describe(this IdentityResult result) =>
        string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
}
