namespace Reaparr.PlexApi.Contracts;

public record PlexApiClientOptions
{
    public required string ConnectionUrl { get; set; }

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int Timeout { get; init; } = 10;

    public int RetryCount { get; init; } = 1;

    public Action<HttpRequestRetryProgress>? RetryProgressAction { get; init; }

    public CancellationToken CancellationToken { get; init; }
}
