using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mya.Api.Authorization;
using Mya.Api.Extensions;
using Mya.Application.Features.Videos;
namespace Mya.Api.Features.Videos;
[ApiController, Authorize(Policy = Policies.AdminOnly), Route("api/admin/tags")]
public sealed class TagsController(VideoAdminHandler admin, VideoQueryHandler query) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => (await query.TagsAsync(true, ct)).ToActionResult(HttpContext, Ok);
    [HttpPost]
    public async Task<IActionResult> Create(TagInput input, CancellationToken ct) => (await admin.AddTagAsync(input, ct)).ToActionResult(HttpContext, Ok);
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) => (await admin.DeleteTagAsync(id, ct)).ToActionResult(HttpContext);
}
