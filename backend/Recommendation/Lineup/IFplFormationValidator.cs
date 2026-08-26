namespace Backend.Recommendation.Lineup;

public interface IFplFormationValidator
{
    bool IsValid(IEnumerable<string> positions);
}