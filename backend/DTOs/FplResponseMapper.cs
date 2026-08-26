using Backend.Models;

namespace Backend.DTOs;

public static class FplResponseMapper
{
    public static FplTeamResponse ToResponse(this Manager manager) => new(
        manager.Id,
        $"{manager.FirstName} {manager.LastName}".Trim(),
        manager.TeamName,
        manager.StartedGameweek,
        manager.CurrentGameweek,
        manager.OverallPoints,
        manager.OverallRank,
        manager.GameweekPoints,
        manager.GameweekRank,
        ToMillions(manager.Bank),
        ToMillions(manager.TeamValue));

    public static FplPlayerResponse ToResponse(
        this Player player,
        IReadOnlyDictionary<int, Team> teams,
        IReadOnlyDictionary<int, PlayerPosition> positions) => new(
        player.Id,
        player.Code,
        player.FirstName,
        player.LastName,
        player.DisplayName,
        player.TeamId,
        teams.GetValueOrDefault(player.TeamId)?.Name ?? "Unknown team",
        player.PositionId,
        positions.GetValueOrDefault(player.PositionId)?.ShortName ?? "Unknown",
        ToMillions(player.Price),
        player.TotalPoints,
        player.GameweekPoints,
        player.Status,
        player.News,
        player.ChanceOfPlayingNextRound);

    public static FplFixtureResponse ToResponse(
        this Fixture fixture,
        IReadOnlyDictionary<int, Team> teams) => new(
        fixture.Id,
        fixture.Code,
        fixture.Gameweek,
        fixture.Kickoff,
        fixture.Finished,
        fixture.Started,
        fixture.HomeTeamId,
        teams.GetValueOrDefault(fixture.HomeTeamId)?.Name ?? "Unknown team",
        fixture.AwayTeamId,
        teams.GetValueOrDefault(fixture.AwayTeamId)?.Name ?? "Unknown team",
        fixture.HomeScore,
        fixture.AwayScore,
        fixture.HomeDifficulty,
        fixture.AwayDifficulty);

    public static FplSquadResponse ToResponse(
        this Squad squad,
        Manager manager,
        BootstrapData bootstrapData)
    {
        var players = bootstrapData.Players.ToDictionary(player => player.Id);
        var teams = bootstrapData.Teams.ToDictionary(team => team.Id);
        var positions = bootstrapData.PlayerPositions.ToDictionary(position => position.Id);

        return new FplSquadResponse(
            manager.Id,
            manager.TeamName,
            squad.Summary.Gameweek,
            squad.ActiveChip,
            new FplSquadSummaryResponse(
                squad.Summary.Points,
                squad.Summary.TotalPoints,
                squad.Summary.OverallRank,
                ToMillions(squad.Summary.Bank),
                ToMillions(squad.Summary.TeamValue),
                squad.Summary.Transfers,
                squad.Summary.TransferCost,
                squad.Summary.BenchPoints),
            squad.Picks.Select(pick =>
            {
                players.TryGetValue(pick.PlayerId, out var player);
                var teamName = player is null
                    ? "Unknown team"
                    : teams.GetValueOrDefault(player.TeamId)?.Name ?? "Unknown team";
                var positionName = player is null
                    ? "Unknown"
                    : positions.GetValueOrDefault(player.PositionId)?.ShortName ?? "Unknown";

                return new FplSquadPickResponse(
                    pick.PlayerId,
                    player?.DisplayName ?? "Unknown player",
                    teamName,
                    positionName,
                    ToMillions(player?.Price ?? 0),
                    pick.Position,
                    pick.Multiplier,
                    pick.IsCaptain,
                    pick.IsViceCaptain);
            }).ToArray());
    }

    private static decimal ToMillions(int tenths) => tenths / 10m;
}