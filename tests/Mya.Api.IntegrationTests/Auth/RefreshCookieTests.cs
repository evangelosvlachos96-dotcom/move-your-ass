using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Mya.Api.Features.Auth;
using Shouldly;

namespace Mya.Api.IntegrationTests.Auth;

/// <summary>
/// The refresh cookie is the whole session. These pin the attributes it goes out with, because a
/// silent change to any of them is either a security regression or a browser-specific outage
/// that only shows up in one engine.
/// </summary>
public sealed class RefreshCookieTests
{
    /// <summary>The one property IsDevelopment() reads; everything else stays at its default.</summary>
    private sealed class StubEnvironment(string environment) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environment;
        public string ApplicationName { get; set; } = "Mya.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private const string Token = "a-refresh-token";

    private static HttpContext Context(string environment, bool https)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new StubEnvironment(environment));

        return new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            Request = { Scheme = https ? "https" : "http", Host = new HostString("localhost") },
        };
    }

    private static string SetCookie(HttpContext context)
    {
        RefreshCookie.Set(context.Response, Token, DateTime.UtcNow.AddDays(7));
        return context.Response.Headers.SetCookie.ToString();
    }

    [Theory]
    [InlineData("Production", false)]
    [InlineData("Production", true)]
    [InlineData("Staging", false)]
    public void Outside_development_the_cookie_is_always_secure(string environment, bool https)
    {
        // Even over plain http: behind Render's load balancer the app speaks http, and a
        // forwarded-headers mistake must not be able to put the session token in the clear.
        SetCookie(Context(environment, https)).ShouldContain("secure", Case.Insensitive);
    }

    [Fact]
    public void In_development_over_http_the_cookie_is_not_secure()
    {
        // Chromium exempts http://localhost from the Secure rule; WebKit does not, and silently
        // never sends the cookie back. Development is served over http, so mirroring the scheme
        // is what keeps Safari and the WebKit end-to-end projects usable.
        SetCookie(Context("Development", https: false)).ShouldNotContain("secure", Case.Insensitive);
    }

    [Fact]
    public void In_development_over_https_the_cookie_is_secure()
    {
        SetCookie(Context("Development", https: true)).ShouldContain("secure", Case.Insensitive);
    }

    [Fact]
    public void The_cookie_is_http_only_same_site_strict_and_scoped_to_the_auth_endpoints()
    {
        var header = SetCookie(Context("Production", https: true));

        header.ShouldStartWith("rt=");
        header.ShouldContain("httponly", Case.Insensitive);
        header.ShouldContain("samesite=strict", Case.Insensitive);
        header.ShouldContain("path=/api/auth", Case.Insensitive);
    }

    [Fact]
    public void Clearing_the_cookie_expires_it_with_the_same_attributes()
    {
        var context = Context("Production", https: true);
        RefreshCookie.Clear(context.Response);
        var header = context.Response.Headers.SetCookie.ToString();

        // The attributes have to match the ones it was set with, or the browser keeps the old
        // cookie alongside the deletion and the session outlives the sign-out.
        header.ShouldContain("path=/api/auth", Case.Insensitive);
        header.ShouldContain("samesite=strict", Case.Insensitive);
        header.ShouldContain("expires=", Case.Insensitive);
    }
}
