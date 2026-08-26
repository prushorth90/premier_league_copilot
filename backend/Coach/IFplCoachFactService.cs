using Backend.Coach.Models;

namespace Backend.Coach;

public interface IFplCoachFactService
{
    PlayerAvailabilityResult GetPlayerAvailability(FplCoachContext context, int playerId);

    Task<PlayerFixtureWindowResult> GetUpcomingFixturesAsync(
        FplCoachContext context,
        int playerId,
        int gameweeks,
        CancellationToken cancellationToken);

    Task<PlayerReplacementResult> GetTransferCandidatesAsync(
        FplCoachContext context,
        int playerId,
        int limit,
        CancellationToken cancellationToken);
}