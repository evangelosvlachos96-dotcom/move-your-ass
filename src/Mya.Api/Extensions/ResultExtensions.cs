using Microsoft.AspNetCore.Mvc;
using Mya.Api.Http;
using Mya.Application.Common.Results;

namespace Mya.Api.Extensions;

/// <summary>The "map the result" third of a thin controller action.</summary>
public static class ResultExtensions
{
    public static IActionResult ToActionResult(this Result result, HttpContext httpContext, Func<IActionResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);

        return result.IsSuccess ? onSuccess() : Problem(httpContext, result.Error!);
    }

    public static IActionResult ToActionResult<T>(this Result<T> result, HttpContext httpContext, Func<T, IActionResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);

        return result.IsSuccess ? onSuccess(result.Value) : Problem(httpContext, result.Error!);
    }

    private static ObjectResult Problem(HttpContext httpContext, Error error)
    {
        var status = error.Status switch
        {
            ResultStatus.Invalid => StatusCodes.Status400BadRequest,
            ResultStatus.Unauthorized => StatusCodes.Status401Unauthorized,
            ResultStatus.Forbidden => StatusCodes.Status403Forbidden,
            ResultStatus.NotFound => StatusCodes.Status404NotFound,
            ResultStatus.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };

        return new ObjectResult(ApiProblems.Create(httpContext, status, error.Code, error.Title, error.Detail))
        {
            StatusCode = status,
            ContentTypes = { ApiProblems.ContentType },
        };
    }
}
