using System.Security.Cryptography;

namespace Mya.Application.Common.Security;

/// <summary>
/// Generates a temporary password that satisfies the Identity policy (10+, upper, lower, digit)
/// from an alphabet without look-alike characters, since the admin reads it out to the client.
/// </summary>
public static class TemporaryPassword
{
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string All = Upper + Lower + Digits;
    private const int Length = 12;

    public static string Generate()
    {
        var chars = new char[Length];
        chars[0] = Pick(Upper);
        chars[1] = Pick(Lower);
        chars[2] = Pick(Digits);
        for (var i = 3; i < Length; i++)
        {
            chars[i] = Pick(All);
        }

        // Fisher-Yates so the guaranteed classes are not always in the first three positions.
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }

    private static char Pick(string alphabet) => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
}
