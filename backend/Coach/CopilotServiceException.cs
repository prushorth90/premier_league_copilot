namespace Backend.Coach;

public sealed class CopilotServiceException(string message, Exception? innerException = null)
    : Exception(message, innerException);