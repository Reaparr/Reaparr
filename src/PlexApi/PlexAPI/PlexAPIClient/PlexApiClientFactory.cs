using LukeHagar.PlexAPI.SDK;
using LukeHagar.PlexAPI.SDK.Utils.Retries;

namespace Reaparr.PlexApi;

public class PlexApiClientFactory : IPlexApiClientFactory
{
    private static readonly RetryConfig DisabledSdkRetry = new(
        RetryConfig.RetryStrategy.NONE,
        new BackoffStrategy(1, 1, 1, 1, 0)
    );

    private readonly Func<PlexApiClientOptions?, IPlexApiClient> _clientFactory;
    private readonly IReaparrDbContextFactory _dbContextFactory;

    public PlexApiClientFactory(
        Func<PlexApiClientOptions?, IPlexApiClient> clientFactory,
        IReaparrDbContextFactory dbContextFactory
    )
    {
        _clientFactory = clientFactory;
        _dbContextFactory = dbContextFactory;
    }

    public IPlexAPI CreateClient(
        string authToken,
        PlexApiClientOptions options,
        CancellationToken cancellationToken = default
    )
    {
        var resolvedOptions = options with
        {
            CancellationToken = cancellationToken.CanBeCanceled ? cancellationToken : options.CancellationToken,
        };
        return new PlexAPI(
            client: _clientFactory(resolvedOptions),
            serverUrl: resolvedOptions.ConnectionUrl,
            token: authToken,
            retryConfig: DisabledSdkRetry
        );
    }

    public IPlexAPI CreateClient(PlexApiClientOptions options, CancellationToken cancellationToken = default) =>
        CreateClient(string.Empty, options, cancellationToken);

    public IPlexAPI CreateTvClient(
        string authToken = "",
        PlexApiClientOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        options ??= new PlexApiClientOptions { ConnectionUrl = "https://plex.tv/api/v2" };
        return CreateClient(authToken, options, cancellationToken);
    }

    public async Task<Result<IPlexAPI>> CreateClientAsync(
        int plexServerId,
        int timeout = 10,
        int retryCount = 1,
        Action<HttpRequestRetryProgress>? retryProgressAction = null,
        CancellationToken cancellationToken = default
    )
    {
        using var dbContext = await _dbContextFactory.CreateAsync();

        var authTokenResult = await dbContext.GetPlexServerTokenAsync(plexServerId, cancellationToken);
        if (authTokenResult.IsCancelledOrFailed)
            return authTokenResult.ToResult().LogIfFailed();

        var connectionResult = await dbContext.ChoosePlexServerConnection(plexServerId, cancellationToken);
        if (connectionResult.IsCancelledOrFailed)
            return connectionResult.ToResult().LogIfFailed();

        var client = CreateClient(
            authTokenResult.Value,
            new PlexApiClientOptions
            {
                ConnectionUrl = connectionResult.Value.Url,
                Timeout = timeout,
                RetryCount = retryCount,
                RetryProgressAction = retryProgressAction,
            },
            cancellationToken
        );

        return Result.Ok(client);
    }
}
