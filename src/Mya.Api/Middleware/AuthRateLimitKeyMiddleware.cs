using System.Text.Json;

namespace Mya.Api.Middleware;

/// <summary>
/// Extracts the email from the JSON body of POST /api/auth/login and /api/auth/register so the
/// rate limiter can partition per email (docs/03 section 4.2). Falls back to the client IP when
/// the body has no usable email. Buffers the body so model binding can read it again.
/// </summary>
public sealed class AuthRateLimitKeyMiddleware(RequestDelegate next)
{
    private const string ItemKey = "Mya.RateLimitPartition";
    private const string EmailProperty = "email";
    private const int MaxBufferedBodyBytes = 16 * 1024;

    private static readonly PathString[] Paths = ["/api/auth/login", "/api/auth/register"];

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (HttpMethods.IsPost(context.Request.Method)
            && Paths.Any(p => context.Request.Path.Equals(p, StringComparison.OrdinalIgnoreCase))
            && context.Request.HasJsonContentType())
        {
            var email = await TryReadEmailAsync(context.Request, context.RequestAborted);
            if (email is not null)
            {
                context.Items[ItemKey] = $"{context.Request.Path.Value!.ToLowerInvariant()}|email:{email}";
            }
        }

        await next(context);
    }

    /// <summary>Partition key for the auth policy: per email when known, per IP otherwise.</summary>
    public static string PartitionKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Items.TryGetValue(ItemKey, out var key) && key is string partition)
        {
            return partition;
        }

        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"{context.Request.Path.Value?.ToLowerInvariant()}|ip:{ip}";
    }

    private static async Task<string?> TryReadEmailAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength is > MaxBufferedBodyBytes)
        {
            return null;
        }

        request.EnableBuffering(MaxBufferedBodyBytes);
        try
        {
            using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Name.Equals(EmailProperty, StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.String)
                {
                    var email = property.Value.GetString()?.Trim().ToLowerInvariant();
                    return string.IsNullOrEmpty(email) ? null : email;
                }
            }

            return null;
        }
        catch (JsonException)
        {
            // Malformed JSON: model binding will reject it with a proper 400.
            return null;
        }
        finally
        {
            request.Body.Position = 0;
        }
    }
}
