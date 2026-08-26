using Backend.Models;

namespace Backend.Services;

public interface IFplDataService
{
    Task<BootstrapData> GetBootstrapDataAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Fixture>> GetFixturesAsync(CancellationToken cancellationToken);

    Task<Manager> GetManagerAsync(int managerId, CancellationToken cancellationToken);

    Task<Squad> GetManagerPicksAsync(int managerId, int gameweek, CancellationToken cancellationToken);

    Task<PlayerHistory> GetPlayerHistoryAsync(int playerId, CancellationToken cancellationToken);
}