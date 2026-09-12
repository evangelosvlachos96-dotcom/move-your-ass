using Microsoft.AspNetCore.Mvc;

namespace Mya.Api.Features.Health;

/// <summary>
/// Liveness only. Must never touch the database so keep-alive pings stay cheap (docs/04 section 1).
/// </summary>
[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok" });
}
