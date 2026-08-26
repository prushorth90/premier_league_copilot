using Backend.DTOs;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController(
    IHealthStatusService healthStatusService,
    ILogger<HealthController> logger) : ControllerBase
{
    [HttpGet(Name = "GetHealth")]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<HealthResponse>> GetAsync(CancellationToken cancellationToken)
    {
        var healthStatus = await healthStatusService.GetStatusAsync(cancellationToken);

        logger.LogInformation(
            "Health check completed with status {HealthStatus}",
            healthStatus.Status);

        return Ok(new HealthResponse(healthStatus.Status, healthStatus.Timestamp));
    }
}