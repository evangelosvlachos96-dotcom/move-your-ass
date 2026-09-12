namespace Mya.Application.Common.Security;

/// <summary>Custom claim types carried by the access token (docs/03 section 2). Standard ones use JWT names.</summary>
public static class AuthClaims
{
    public const string Role = "role";
    public const string SessionId = "sid";
    public const string SecurityStamp = "stamp";
    public const string MustChangePassword = "must_change_password";
}
