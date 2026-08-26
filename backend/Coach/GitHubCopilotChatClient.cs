using Backend.Configuration;
using GitHub.Copilot;
using Microsoft.Extensions.Options;

namespace Backend.Coach;

public sealed class GitHubCopilotChatClient : ICopilotChatClient, IAsyncDisposable
{
    private readonly CopilotOptions options;
    private readonly ILogger<GitHubCopilotChatClient> logger;
    private readonly CopilotClient client;

    public GitHubCopilotChatClient(
        IOptions<CopilotOptions> options,
        ILogger<GitHubCopilotChatClient> logger)
    {
        this.options = options.Value;
        this.logger = logger;
        client = new CopilotClient(CreateClientOptions(this.options));
    }

    public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.RequestTimeoutSeconds));

        try
        {
            await client.StartAsync(timeout.Token);
            await using var session = await client.CreateSessionAsync(new SessionConfig
            {
                Model = options.Model,
                AvailableTools = [],
                ExcludedTools = new ToolSet()
                    .AddBuiltIn("*")
                    .AddMcp("*")
                    .AddCustom("*")
            }, timeout.Token);
            var response = await session.SendAndWaitAsync(
                prompt,
                TimeSpan.FromSeconds(options.RequestTimeoutSeconds),
                timeout.Token);
            var content = response?.Data.Content?.Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new CopilotServiceException("GitHub Copilot returned an empty response.");
            }

            return content;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("GitHub Copilot request timed out after {TimeoutSeconds}s", options.RequestTimeoutSeconds);
            throw new CopilotServiceException("GitHub Copilot timed out.");
        }
        catch (CopilotServiceException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "GitHub Copilot SDK request failed");
            throw new CopilotServiceException("GitHub Copilot is temporarily unavailable.", exception);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await client.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private static CopilotClientOptions CreateClientOptions(CopilotOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.RuntimeUrl))
        {
            return new CopilotClientOptions
            {
                Mode = CopilotClientMode.Empty,
                BaseDirectory = options.BaseDirectory,
                Connection = RuntimeConnection.ForUri(options.RuntimeUrl, options.RuntimeConnectionToken)
            };
        }

        return new CopilotClientOptions
        {
            Mode = CopilotClientMode.Empty,
            BaseDirectory = options.BaseDirectory,
            GitHubToken = options.GitHubToken,
            UseLoggedInUser = string.IsNullOrWhiteSpace(options.GitHubToken)
        };
    }
}