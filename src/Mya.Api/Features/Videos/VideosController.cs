using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mya.Api.Extensions;
using Mya.Application.Features.Videos;
namespace Mya.Api.Features.Videos;
[ApiController, Authorize, Route("api/videos")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class VideosController(VideoQueryHandler query) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] VideoQuery input, CancellationToken ct) =>
        (await query.ListAsync(input, false, ct)).ToActionResult(HttpContext, Ok);
    [HttpGet("filters")]
    public async Task<IActionResult> Filters(CancellationToken ct) =>
        (await query.TagsAsync(false, ct)).ToActionResult(HttpContext, Ok);
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct) =>
        (await query.DetailAsync(id, false, ct)).ToActionResult(HttpContext, Ok);
    [HttpGet("{id:guid}/playback")]
    public async Task<IActionResult> Playback(Guid id, CancellationToken ct) =>
        (await query.PlaybackAsync(id, false, ct)).ToActionResult(HttpContext, Ok);
}
