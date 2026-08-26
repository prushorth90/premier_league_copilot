namespace Backend.Recommendation.Lineup;

public sealed class FplFormationValidator : IFplFormationValidator
{
    public bool IsValid(IEnumerable<string> positions)
    {
        var counts = positions
            .GroupBy(position => position)
            .ToDictionary(group => group.Key, group => group.Count());

        return counts.Values.Sum() == 11
            && Count("GKP") == 1
            && Count("DEF") is >= 3 and <= 5
            && Count("MID") is >= 2 and <= 5
            && Count("FWD") is >= 1 and <= 3
            && counts.Keys.All(position => position is "GKP" or "DEF" or "MID" or "FWD");

        int Count(string position) => counts.GetValueOrDefault(position);
    }
}