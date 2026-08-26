using Backend.Coach.Models;
using GitHub.Copilot;

namespace Backend.Coach;

public interface IFplCoachSessionFactory
{
    SessionConfig Create(
        FplCoachContext context,
        string model,
        CancellationToken cancellationToken);
}