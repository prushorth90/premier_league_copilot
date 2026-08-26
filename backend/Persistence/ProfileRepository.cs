using System.Text.Json;
using Backend.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Persistence;

public sealed class ProfileRepository(
    ApplicationDbContext dbContext,
    TimeProvider timeProvider) : IProfileRepository
{
    public async Task<IReadOnlyList<LocalProfile>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.Profiles
            .AsNoTracking()
            .OrderBy(profile => profile.DisplayName)
            .ThenBy(profile => profile.Id)
            .ToArrayAsync(cancellationToken);

    public Task<LocalProfile?> GetAsync(Guid profileId, CancellationToken cancellationToken) =>
        dbContext.Profiles.AsNoTracking().SingleOrDefaultAsync(profile => profile.Id == profileId, cancellationToken);

    public async Task<LocalProfile> CreateAsync(
        string displayName,
        int? selectedFplTeamId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var profile = new LocalProfile
        {
            Id = Guid.NewGuid(),
            DisplayName = displayName.Trim(),
            SelectedFplTeamId = selectedFplTeamId,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.Profiles.Add(profile);
        await dbContext.SaveChangesAsync(cancellationToken);
        return profile;
    }

    public async Task<LocalProfile?> UpdateSelectedTeamAsync(
        Guid profileId,
        int? selectedFplTeamId,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.Profiles.SingleOrDefaultAsync(item => item.Id == profileId, cancellationToken);
        if (profile is null)
        {
            return null;
        }

        profile.SelectedFplTeamId = selectedFplTeamId;
        profile.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return profile;
    }

    public async Task<JsonElement?> GetSettingAsync(Guid profileId, string key, CancellationToken cancellationToken)
    {
        var valueJson = await dbContext.ApplicationSettings
            .AsNoTracking()
            .Where(setting => setting.ProfileId == profileId && setting.Key == key)
            .Select(setting => setting.ValueJson)
            .SingleOrDefaultAsync(cancellationToken);
        return valueJson is null ? null : JsonSerializer.Deserialize<JsonElement>(valueJson);
    }

    public async Task<bool> SetSettingAsync(
        Guid profileId,
        string key,
        JsonElement value,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Profiles.AnyAsync(profile => profile.Id == profileId, cancellationToken))
        {
            return false;
        }

        var setting = await dbContext.ApplicationSettings
            .SingleOrDefaultAsync(item => item.ProfileId == profileId && item.Key == key, cancellationToken);
        if (setting is null)
        {
            setting = new ApplicationSetting { ProfileId = profileId, Key = key, ValueJson = value.GetRawText() };
            dbContext.ApplicationSettings.Add(setting);
        }
        else
        {
            setting.ValueJson = value.GetRawText();
        }

        setting.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}