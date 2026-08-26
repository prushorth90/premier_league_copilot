using Backend.DTOs;
using Backend.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/profiles")]
[Produces("application/json")]
public sealed class ProfilesController(IProfileRepository profileRepository) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<LocalProfileResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LocalProfileResponse>>> ListAsync(CancellationToken cancellationToken)
    {
        var profiles = await profileRepository.ListAsync(cancellationToken);
        return Ok(profiles.Select(LocalProfileResponse.From).ToArray());
    }

    [HttpGet("{profileId:guid}")]
    [ProducesResponseType<LocalProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LocalProfileResponse>> GetAsync(Guid profileId, CancellationToken cancellationToken)
    {
        if (profileId == Guid.Empty)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid profile ID.");
        }

        var profile = await profileRepository.GetAsync(profileId, cancellationToken);
        return profile is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Profile not found.")
            : Ok(LocalProfileResponse.From(profile));
    }

    [HttpPost]
    [ProducesResponseType<LocalProfileResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LocalProfileResponse>> CreateAsync(
        CreateProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length > 120 || request.SelectedFplTeamId <= 0)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid profile.", detail: "Display name is required and team ID, when provided, must be positive.");
        }

        var profile = await profileRepository.CreateAsync(request.DisplayName, request.SelectedFplTeamId, cancellationToken);
        return Created($"/api/profiles/{profile.Id}", LocalProfileResponse.From(profile));
    }

    [HttpPut("{profileId:guid}/team")]
    [ProducesResponseType<LocalProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LocalProfileResponse>> UpdateTeamAsync(
        Guid profileId,
        UpdateSelectedTeamRequest request,
        CancellationToken cancellationToken)
    {
        if (profileId == Guid.Empty || request.SelectedFplTeamId <= 0)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid FPL team ID.");
        }

        var profile = await profileRepository.UpdateSelectedTeamAsync(profileId, request.SelectedFplTeamId, cancellationToken);
        return profile is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Profile not found.")
            : Ok(LocalProfileResponse.From(profile));
    }

    [HttpGet("{profileId:guid}/settings/{key}")]
    [ProducesResponseType<ApplicationSettingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApplicationSettingResponse>> GetSettingAsync(
        Guid profileId,
        string key,
        CancellationToken cancellationToken)
    {
        if (profileId == Guid.Empty || !IsValidSettingKey(key))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid profile ID or setting key.");
        }

        var value = await profileRepository.GetSettingAsync(profileId, key, cancellationToken);
        return value is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Setting not found.")
            : Ok(new ApplicationSettingResponse(key, value.Value));
    }

    [HttpPut("{profileId:guid}/settings/{key}")]
    [ProducesResponseType<ApplicationSettingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApplicationSettingResponse>> SetSettingAsync(
        Guid profileId,
        string key,
        SetApplicationSettingRequest request,
        CancellationToken cancellationToken)
    {
        if (profileId == Guid.Empty || !IsValidSettingKey(key) || request.Value.ValueKind == System.Text.Json.JsonValueKind.Undefined)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid setting key.");
        }

        var updated = await profileRepository.SetSettingAsync(profileId, key, request.Value, cancellationToken);
        return updated
            ? Ok(new ApplicationSettingResponse(key, request.Value))
            : Problem(statusCode: StatusCodes.Status404NotFound, title: "Profile not found.");
    }

    private static bool IsValidSettingKey(string key) =>
        !string.IsNullOrWhiteSpace(key) &&
        key.Length <= 100 &&
        key.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');
}