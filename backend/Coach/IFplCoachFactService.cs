using Backend.Coach.Models;

namespace Backend.Coach;

public interface IFplCoachFactService
{
    PlayerAvailabilityResult GetPlayerAvailability(FplCoachContext context, int playerId);

    Task<string> GetUpcomingFixturesAsync(
        FplCoachContext context,
        string playerName,
        int gameweeks,
        CancellationToken cancellationToken);

    Task<string> GetTransferOptionsAsync(
        FplCoachContext context,
        string playerName,
        int limit,
        CancellationToken cancellationToken);
}