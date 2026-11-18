using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Olvanta.Application;

namespace Olvanta.Api.Controllers;

[ApiController]
[Route("api/v1/system")]
public sealed class SystemController(HealthCheckService health) : ControllerBase
{
    [HttpGet("status", Name = "GetSystemStatus")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [Produces("application/json")]
    [ProducesResponseType<SystemStatus>(StatusCodes.Status200OK)]
    [ProducesResponseType<SystemStatus>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SystemStatus>> GetStatus(CancellationToken cancellationToken)
    {
        var report = await health.CheckHealthAsync(check => check.Name == "database", cancellationToken);
        return report.Status == HealthStatus.Healthy
            ? Ok(new SystemStatus("ready", "connected"))
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new SystemStatus("degraded", "unavailable"));
    }
}
