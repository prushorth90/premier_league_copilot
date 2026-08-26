namespace Backend.Coach;

public interface ICopilotChatClient
{
    Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken);
}