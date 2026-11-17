using LukeHagar.PlexAPI.SDK;

namespace Reaparr.PlexApi;

/// <summary>
/// Creates configured clients for communicating with Plex servers.
/// </summary>
public interface IPlexApiClientFactory
{
    /// <summary>
    /// Creates a Plex API client authenticated with the supplied token.
    /// </summary>
    /// <param name="authToken">The Plex authentication token.</param>
    /// <param name="options">The options used to configure the client.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A configured Plex API client.</returns>
    IPlexAPI CreateClient(
        string authToken,
        PlexApiClientOptions options,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Creates a Plex API client without an authentication token.
    /// </summary>
    /// <param name="options">The options used to configure the client.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A configured Plex API client.</returns>
    IPlexAPI CreateClient(PlexApiClientOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a Plex API client for TV-related operations.
    /// </summary>
    /// <param name="authToken">The optional Plex authentication token.</param>
    /// <param name="options">The optional options used to configure the client.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A configured Plex API client.</returns>
    IPlexAPI CreateTvClient(
        string authToken = "",
        PlexApiClientOptions? options = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Creates a Plex API client authenticated with the token stored for the specified server.
    /// </summary>
    /// <param name="plexServerId">The identifier of the Plex server.</param>
    /// <param name="timeout">The timeout for the client.</param>
    /// <param name="retryCount">The number of times to retry the request.</param>
    /// <param name="retryProgressAction">The action to invoke when a retry is made.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A task that represents the asynchronous client creation operation.</returns>
    Task<Result<IPlexAPI>> CreateClientAsync(
        int plexServerId,
        int timeout = 10,
        int retryCount = 1,
        Action<HttpRequestRetryProgress>? retryProgressAction = null,
        CancellationToken cancellationToken = default
    );
}
