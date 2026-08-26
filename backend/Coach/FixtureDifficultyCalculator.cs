namespace Backend.Coach;

public static class FixtureDifficultyCalculator
{
    public static FixtureDifficultySummary Calculate(IEnumerable<int> difficulties)
    {
        var values = difficulties.ToArray();
        if (values.Length == 0)
        {
            return new(null, null, "Unknown");
        }

        if (values.Any(value => value is < 1 or > 5))
        {
            throw new ArgumentOutOfRangeException(nameof(difficulties), "FPL fixture difficulty must be between 1 and 5.");
        }

        var averageDifficulty = Round(values.Average(value => (decimal)value));
        var aggregateScore = Round(6m - averageDifficulty);
        var rating = averageDifficulty switch
        {
            <= 2.5m => "Favorable",
            <= 3.5m => "Mixed",
            _ => "Difficult"
        };
        return new(averageDifficulty, aggregateScore, rating);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

public sealed record FixtureDifficultySummary(
    decimal? AverageDifficulty,
    decimal? AggregateScore,
    string ScheduleRating);