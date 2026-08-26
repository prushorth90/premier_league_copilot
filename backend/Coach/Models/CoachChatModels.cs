namespace Backend.Coach.Models;

public sealed record CoachChatRequest(int TeamId, string Message);

public sealed record CoachChatResponse(
    string Message,
    int TeamId,
    DateTimeOffset RespondedAt,
    bool IsMocked);