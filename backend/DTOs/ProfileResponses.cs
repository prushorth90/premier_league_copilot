using System.Text.Json;
using Backend.Persistence.Entities;

namespace Backend.DTOs;

public sealed record CreateProfileRequest(string DisplayName, int? SelectedFplTeamId);

public sealed record UpdateSelectedTeamRequest(int? SelectedFplTeamId);

public sealed record SetApplicationSettingRequest(JsonElement Value);

public sealed record LocalProfileResponse(
    Guid Id,
    string DisplayName,
    int? SelectedFplTeamId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static LocalProfileResponse From(LocalProfile profile) => new(
        profile.Id,
        profile.DisplayName,
        profile.SelectedFplTeamId,
        profile.CreatedAt,
        profile.UpdatedAt);
}

public sealed record ApplicationSettingResponse(string Key, JsonElement Value);