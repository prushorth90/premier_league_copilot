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
    [ProducesResponseType<HealthResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthResponse>> GetAsync(CancellationToken cancellationToken)
    {
        var healthStatus = await healthStatusService.GetStatusAsync(cancellationToken);

        logger.LogDebug(
            "Health check completed with status {HealthStatus}",
            healthStatus.Status);

        var response = new HealthResponse(healthStatus.Status, healthStatus.Timestamp, healthStatus.Dependencies);
        return healthStatus.Status == "Unhealthy"
            ? StatusCode(StatusCodes.Status503ServiceUnavailable, response)
            : Ok(response);
    }
}