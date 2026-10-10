using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Mya.Api.Http;

/// <summary>Receipt for successful commands that do not return a resource.</summary>
public static class ApiSuccess
{
    public static OkObjectResult Create(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new OkObjectResult(new { success = true, traceId = Activity.Current?.Id ?? context.TraceIdentifier });
    }
}
