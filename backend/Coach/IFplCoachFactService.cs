using Backend.Coach.Models;

namespace Backend.Coach;

public interface IFplCoachFactService
{
    string GetPlayerAvailability(FplCoachContext context, string playerName);

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