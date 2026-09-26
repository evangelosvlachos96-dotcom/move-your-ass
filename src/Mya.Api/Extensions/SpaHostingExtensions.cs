using System.Text.RegularExpressions;
using Microsoft.Net.Http.Headers;
using Mya.Api.Http;
using Mya.Application.Common.Results;

namespace Mya.Api.Extensions;

/// <summary>
/// Serves the Angular build from wwwroot so the SPA and the API share one origin (ADR-017).
/// Same origin keeps the SameSite=Strict refresh cookie working without a custom domain and
/// removes CORS from production. In Development wwwroot is empty and the Angular dev server is
/// used instead; nothing here changes that.
/// </summary>
public static partial class SpaHostingExtensions
{
    private const string IndexFile = "index.html";

    /// <summary>Static assets. Registered before request logging so asset hits do not flood the logs.</summary>
    public static WebApplication UseSpaStaticFiles(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseStaticFiles(CreateOptions());
        return app;
    }

    /// <summary>
    /// Unknown /api routes get a 404 ProblemDetails; every other unmatched, extension-less path
    /// gets index.html so client-side routes survive a refresh or an emailed deep link.
    /// </summary>
    public static WebApplication MapSpaFallback(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapFallback("/api/{**path}", context => ApiProblems.WriteAsync(
            context,
            StatusCodes.Status404NotFound,
            ErrorCodes.NotFound,
            "Not found"));

        app.MapFallbackToFile(IndexFile, CreateOptions());
        return app;
    }

    private static StaticFileOptions CreateOptions() => new()
    {
        OnPrepareResponse = context => context.Context.Response.Headers[HeaderNames.CacheControl] =
            CacheControlFor(context.File.Name),
    };

    /// <summary>
    /// index.html must always be revalidated or clients keep an old build; Angular's hashed
    /// bundles (main-AbC_12-z.js) never change content and can be cached for a year.
    /// </summary>
    private static string CacheControlFor(string fileName)
    {
        if (string.Equals(fileName, IndexFile, StringComparison.OrdinalIgnoreCase))
        {
            return "no-cache";
        }

        return HashedBundle().IsMatch(fileName)
            ? "public, max-age=31536000, immutable"
            : "public, max-age=3600";
    }

    [GeneratedRegex(@"-[A-Za-z0-9_-]{8}\.(js|css)$")]
    private static partial Regex HashedBundle();
}
