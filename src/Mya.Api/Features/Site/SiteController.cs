using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mya.Api.Authorization;
using Mya.Api.Extensions;
using Mya.Application.Features.Site;
using Mya.Application.Features.Videos;

namespace Mya.Api.Features.Site;

/// <summary>
/// The "Ο γυμναστής σου" page. Reading needs an approved account like everything else behind the
/// shell; writing needs an admin.
/// </summary>
[ApiController, Authorize, Route("api/site")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SiteController(SiteContentHandler content, ContactHandler contact) : ControllerBase
{
    [HttpGet("about")]
    public async Task<IActionResult> About(CancellationToken ct) =>
        (await content.GetAsync(ct)).ToActionResult(HttpContext, Ok);

    /// <summary>
    /// Sending is throttled per account, not per IP: a household behind one address must not
    /// share an allowance, and the limit is about one person sending too much.
    /// </summary>
    [HttpPost("contact")]
    [EnableRateLimiting(RateLimitPolicies.PerUserWrite)]
    public async Task<IActionResult> Contact(ContactInput input, CancellationToken ct) =>
        (await contact.SendAsync(input, ct)).ToActionResult(HttpContext, NoContent);
}

[ApiController, Authorize(Policy = Policies.AdminOnly), Route("api/admin/site")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminSiteController(SiteContentHandler content) : ControllerBase
{
    [HttpPut("about")]
    public async Task<IActionResult> Update(AboutInput input, CancellationToken ct) =>
        (await content.UpdateAsync(input, ct)).ToActionResult(HttpContext, NoContent);

    [HttpPost("about/photo")]
    public async Task<IActionResult> PhotoUpload(CoverRequest input, CancellationToken ct) =>
        (await content.PhotoUploadAsync(input, ct)).ToActionResult(HttpContext, Ok);

    [HttpPut("about/photo")]
    public async Task<IActionResult> PhotoConfirm(PhotoConfirm input, CancellationToken ct) =>
        (await content.PhotoConfirmAsync(input, ct)).ToActionResult(HttpContext, NoContent);

    [HttpDelete("about/photo")]
    public async Task<IActionResult> PhotoRemove(CancellationToken ct) =>
        (await content.PhotoRemoveAsync(ct)).ToActionResult(HttpContext, NoContent);
}
