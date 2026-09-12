namespace Mya.Api.Features.Auth;

/// <summary>
/// The refresh token travels only in this cookie: HttpOnly, Secure, SameSite=Strict, scoped to
/// /api/auth (docs/03 section 2). Curl and browsers on https send it; an XSS cannot read it.
/// </summary>
internal static class RefreshCookie
{
    public const string Name = "rt";
    private const string CookiePath = "/api/auth";

    public static void Set(HttpResponse response, string token, DateTime expiresUtc) =>
        response.Cookies.Append(Name, token, Options(expiresUtc));

    public static void Clear(HttpResponse response) =>
        response.Cookies.Delete(Name, Options(expires: null));

    private static CookieOptions Options(DateTime? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = CookiePath,
        Expires = expires,
        IsEssential = true,
    };
}
