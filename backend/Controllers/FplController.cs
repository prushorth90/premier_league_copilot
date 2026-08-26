using Backend.DTOs;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/fpl")]
[Produces("application/json")]
public sealed class FplController(
    IFplDataService fplDataService,
    ILogger<FplController> logger) : ControllerBase
{
    [HttpGet("team/{teamId}", Name = "GetFplTeam")]
    [ProducesResponseType<FplTeamResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<FplTeamResponse>> GetTeamAsync(
        int teamId,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateTeamId(teamId);
        if (validationError is not null)
        {
            return validationError;
        }

        logger.LogInformation("Retrieving FPL team {TeamId}", teamId);
        var manager = await fplDataService.GetManagerAsync(teamId, cancellationToken);
        return Ok(manager.ToResponse());
    }

    [HttpGet("team/{teamId}/squad", Name = "GetFplTeamSquad")]
    [ProducesResponseType<FplSquadResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<FplSquadResponse>> GetTeamSquadAsync(
        int teamId,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateTeamId(teamId);
        if (validationError is not null)
        {
            return validationError;
        }

        logger.LogInformation("Retrieving current squad for FPL team {TeamId}", teamId);
        var manager = await fplDataService.GetManagerAsync(teamId, cancellationToken);
        if (manager.CurrentGameweek <= 0)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Squad data is not available.",
                detail: "The FPL team does not have a current gameweek squad.");
        }

        var squadTask = fplDataService.GetManagerPicksAsync(
            teamId,
            manager.CurrentGameweek,
            cancellationToken);
        var bootstrapTask = fplDataService.GetBootstrapDataAsync(cancellationToken);
        await Task.WhenAll(squadTask, bootstrapTask);

        var squad = await squadTask;
        var bootstrapData = await bootstrapTask;
        return Ok(squad.ToResponse(manager, bootstrapData));
    }

    [HttpGet("players", Name = "GetFplPlayers")]
    [ProducesResponseType<IReadOnlyList<FplPlayerResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<IReadOnlyList<FplPlayerResponse>>> GetPlayersAsync(
        CancellationToken cancellationToken)
    {
        var bootstrapData = await fplDataService.GetBootstrapDataAsync(cancellationToken);
        var teams = bootstrapData.Teams.ToDictionary(team => team.Id);
        var positions = bootstrapData.PlayerPositions.ToDictionary(position => position.Id);

        return Ok(bootstrapData.Players
            .Select(player => player.ToResponse(teams, positions))
            .ToArray());
    }

    [HttpGet("fixtures", Name = "GetFplFixtures")]
    [ProducesResponseType<IReadOnlyList<FplFixtureResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<IReadOnlyList<FplFixtureResponse>>> GetFixturesAsync(
        CancellationToken cancellationToken)
    {
        var fixturesTask = fplDataService.GetFixturesAsync(cancellationToken);
        var bootstrapTask = fplDataService.GetBootstrapDataAsync(cancellationToken);
        await Task.WhenAll(fixturesTask, bootstrapTask);

        var fixtures = await fixturesTask;
        var bootstrapData = await bootstrapTask;
        var teams = bootstrapData.Teams.ToDictionary(team => team.Id);
        return Ok(fixtures
            .Select(fixture => fixture.ToResponse(teams))
            .ToArray());
    }

    private ObjectResult? ValidateTeamId(int teamId) => teamId > 0
        ? null
        : Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid FPL team ID.",
            detail: "The team ID must be a positive integer.");
}