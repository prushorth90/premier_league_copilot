namespace Backend.Coach;

public interface ICoachProgressSink
{
    ValueTask ReportAsync(CoachProgressUpdate update, CancellationToken cancellationToken);
}

public sealed record CoachProgressUpdate(string Code, string Message);