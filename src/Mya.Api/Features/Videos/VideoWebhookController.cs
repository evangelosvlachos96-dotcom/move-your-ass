using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mya.Api.Extensions;
using Mya.Application.Features.Videos;
using Mya.Application.Common.Results;
namespace Mya.Api.Features.Videos;
[ApiController, AllowAnonymous, Route("api/webhooks/video-ready")]
public sealed class VideoWebhookController(VideoWebhookRequest handler) : ControllerBase
{
    [HttpPost, RequestSizeLimit(16384)]
    public async Task<IActionResult> Handle(CancellationToken ct) =>
        (await handler.HandleAsync(Request, ct)).ToActionResult(HttpContext, NoContent);
}
public sealed class VideoWebhookRequest(VideoProcessingHandler handler)
{
    public async Task<Result> HandleAsync(HttpRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, ct);
        return await handler.WebhookAsync(buffer.ToArray(), request.Headers["X-BunnyStream-Signature"].ToString(),
            request.Headers["X-BunnyStream-Signature-Version"].ToString(), request.Headers["X-BunnyStream-Signature-Algorithm"].ToString(), ct);
    }
}
