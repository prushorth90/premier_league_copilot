namespace Backend.ExternalClients;

public interface IFplApiClient
{
    Task<FplBootstrapDto> GetBootstrapStaticAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<FplFixtureDto>> GetFixturesAsync(CancellationToken cancellationToken);

    Task<FplManagerDto> GetManagerAsync(int managerId, CancellationToken cancellationToken);

    Task<FplSquadPicksDto> GetManagerPicksAsync(
        int managerId,
        int gameweek,
        CancellationToken cancellationToken);

    Task<FplPlayerSummaryDto> GetPlayerSummaryAsync(int playerId, CancellationToken cancellationToken);
}