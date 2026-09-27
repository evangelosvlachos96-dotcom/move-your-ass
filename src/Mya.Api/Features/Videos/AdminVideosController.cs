using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mya.Api.Authorization;
using Mya.Api.Extensions;
using Mya.Application.Features.Videos;
namespace Mya.Api.Features.Videos;
[ApiController, Authorize(Policy = Policies.AdminOnly), Route("api/admin/videos")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminVideosController(VideoAdminHandler admin, VideoQueryHandler query, VideoProcessingHandler processing) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] VideoQuery input, CancellationToken ct) =>
        (await query.ListAsync(input, true, ct)).ToActionResult(HttpContext, Ok);
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct) =>
        (await query.SummaryAsync(ct)).ToActionResult(HttpContext, Ok);
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct) =>
        (await query.DetailAsync(id, true, ct)).ToActionResult(HttpContext, Ok);
    [HttpGet("{id:guid}/playback")]
    public async Task<IActionResult> Playback(Guid id, CancellationToken ct) =>
        (await query.PlaybackAsync(id, true, ct)).ToActionResult(HttpContext, Ok);
    [HttpPost]
    public async Task<IActionResult> Create(VideoCreateInput input, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct) =>
        (await admin.CreateAsync(input, key ?? string.Empty, ct)).ToActionResult(HttpContext, v => Created($"/api/admin/videos/{v.Id}", v));
    [HttpPost("{id:guid}/upload")]
    public async Task<IActionResult> Upload(Guid id, UploadRequest input, CancellationToken ct) =>
        (await admin.UploadAsync(id, input, ct)).ToActionResult(HttpContext, Ok);
    [HttpPost("{id:guid}/upload/complete")]
    public async Task<IActionResult> Complete(Guid id, CompleteUploadInput input, CancellationToken ct) =>
        (await admin.CompleteAsync(id, input, ct)).ToActionResult(HttpContext, NoContent);
    [HttpPost("{id:guid}/upload/abort")]
    public async Task<IActionResult> Abort(Guid id, CancellationToken ct) =>
        (await admin.AbortAsync(id, ct)).ToActionResult(HttpContext, NoContent);
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, VideoInput input, CancellationToken ct) =>
        (await admin.UpdateAsync(id, input, ct)).ToActionResult(HttpContext, NoContent);
    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, RevisionInput input, CancellationToken ct) =>
        (await admin.PublishAsync(id, input, true, ct)).ToActionResult(HttpContext, NoContent);
    [HttpPost("{id:guid}/unpublish")]
    public async Task<IActionResult> Unpublish(Guid id, RevisionInput input, CancellationToken ct) =>
        (await admin.PublishAsync(id, input, false, ct)).ToActionResult(HttpContext, NoContent);
    [HttpPost("{id:guid}/refresh")]
    public async Task<IActionResult> Refresh(Guid id, CancellationToken ct) =>
        (await processing.RefreshAsync(id, ct)).ToActionResult(HttpContext, NoContent);
    [HttpPost("reorder")]
    public async Task<IActionResult> Reorder(VideoOrder[] input, CancellationToken ct) =>
        (await admin.ReorderAsync(input, ct)).ToActionResult(HttpContext, NoContent);
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        (await admin.DeleteAsync(id, ct)).ToActionResult(HttpContext, NoContent);
}
