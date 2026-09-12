using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Mya.Application.Common.Results;

namespace Mya.Api.Http;

/// <summary>
/// Every failure on the wire is RFC 7807 with a stable <c>code</c> extension (CLAUDE.md conventions).
/// Used by the result mapper, the validation filter, auth events, the rate limiter and the
/// exception handler, so the shape is identical wherever the failure originates.
/// </summary>
public static class ApiProblems
{
    public const string ContentType = "application/problem+json";
    private const string TypeBase = "https://mya.app/errors/";

    public static ProblemDetails Create(HttpContext httpContext, int status, string code, string title, string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(code);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Type = TypeFor(code),
            Instance = httpContext.Request.Path,
        };
        Decorate(problem, httpContext, code);
        return problem;
    }

    public static ValidationProblemDetails Validation(HttpContext httpContext, IDictionary<string, string[]> errors)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed",
            Type = TypeFor(ErrorCodes.ValidationFailed),
            Instance = httpContext.Request.Path,
        };
        Decorate(problem, httpContext, ErrorCodes.ValidationFailed);
        return problem;
    }

    /// <summary>Writes a problem response directly, for places that have no IActionResult (middleware, auth events).</summary>
    public static async Task WriteAsync(HttpContext httpContext, int status, string code, string title, string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        httpContext.Response.StatusCode = status;
        var problem = Create(httpContext, status, code, title, detail);

        var service = httpContext.RequestServices.GetService<IProblemDetailsService>();
        var written = service is not null
            && await service.TryWriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problem });

        if (!written)
        {
            httpContext.Response.ContentType = ContentType;
            await httpContext.Response.WriteAsJsonAsync(problem, httpContext.RequestAborted);
        }
    }

    private static void Decorate(ProblemDetails problem, HttpContext httpContext, string code)
    {
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;
    }

    private static string TypeFor(string code) =>
        TypeBase + code.ToLowerInvariant().Replace('_', '-');
}
