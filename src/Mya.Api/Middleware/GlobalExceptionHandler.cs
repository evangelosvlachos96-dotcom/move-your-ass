using Microsoft.AspNetCore.Diagnostics;
using Mya.Api.Http;
using Mya.Application.Common.Results;

namespace Mya.Api.Middleware;

/// <summary>
/// Last line: a generic 500 ProblemDetails with the trace id. The exception itself is logged by
/// the exception handler middleware with the correlation id; nothing about it reaches the client.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private const int ClientClosedRequest = 499;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            httpContext.Response.StatusCode = ClientClosedRequest;
            return true;
        }

        await ApiProblems.WriteAsync(
            httpContext,
            StatusCodes.Status500InternalServerError,
            ErrorCodes.InternalError,
            "An unexpected error occurred");

        return true;
    }
}
