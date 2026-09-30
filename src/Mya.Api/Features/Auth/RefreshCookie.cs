using Microsoft.Extensions.Hosting;

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
        response.Cookies.Append(Name, token, Options(response.HttpContext, expiresUtc));

    public static void Clear(HttpResponse response) =>
        response.Cookies.Delete(Name, Options(response.HttpContext, expires: null));

    private static CookieOptions Options(HttpContext context, DateTime? expires) => new()
    {
        HttpOnly = true,
        Secure = IsSecure(context),
        SameSite = SameSiteMode.Strict,
        Path = CookiePath,
        Expires = expires,
        IsEssential = true,
    };

    /// <summary>
    /// Always <c>true</c> outside Development, whatever the request looks like — a proxy header
    /// mistake must never be able to put this cookie on the wire in the clear.
    ///
    /// In Development it mirrors the scheme instead, because the local app is served over plain
    /// http. Chromium exempts <c>http://localhost</c> from the Secure rule and sends the cookie
    /// anyway; WebKit does not, so a Secure cookie on http is stored and then never sent back,
    /// and every session silently dies on the next request — in Safari and in the WebKit
    /// end-to-end projects only. Production is https, so this changes nothing there.
    /// </summary>
    private static bool IsSecure(HttpContext context) =>
        !context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment()
        || context.Request.IsHttps;
}
