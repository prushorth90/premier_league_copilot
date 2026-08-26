using System.Text.Json;
using Backend.Persistence.Entities;

namespace Backend.Persistence;

public interface IProfileRepository
{
    Task<IReadOnlyList<LocalProfile>> ListAsync(CancellationToken cancellationToken);

    Task<LocalProfile?> GetAsync(Guid profileId, CancellationToken cancellationToken);

    Task<LocalProfile> CreateAsync(string displayName, int? selectedFplTeamId, CancellationToken cancellationToken);

    Task<LocalProfile?> UpdateSelectedTeamAsync(Guid profileId, int? selectedFplTeamId, CancellationToken cancellationToken);

    Task<JsonElement?> GetSettingAsync(Guid profileId, string key, CancellationToken cancellationToken);

    Task<bool> SetSettingAsync(Guid profileId, string key, JsonElement value, CancellationToken cancellationToken);
}