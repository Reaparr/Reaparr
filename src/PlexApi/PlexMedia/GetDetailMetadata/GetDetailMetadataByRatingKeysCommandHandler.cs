using LukeHagar.PlexAPI.SDK.Models.Requests;

namespace Reaparr.PlexApi;

public class GetDetailMetadataByRatingKeysCommandHandler
    : ICommandHandler<GetDetailMetadataByRatingKeysCommand, Result<List<LibraryMediaItemDTO>>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly IPlexApiClientFactory _plexApiClientFactory;

    public GetDetailMetadataByRatingKeysCommandHandler(
        ILogger log,
        IReaparrDbContext dbContext,
        IPlexApiClientFactory plexApiClientFactory
    )
    {
        _log = log.ForContext<GetDetailMetadataByRatingKeysCommandHandler>();
        _dbContext = dbContext;
        _plexApiClientFactory = plexApiClientFactory;
    }

    public async Task<Result<List<LibraryMediaItemDTO>>> ExecuteAsync(
        GetDetailMetadataByRatingKeysCommand command,
        CancellationToken ct
    )
    {
        if (command.RatingKeys.Length == 0)
            return ResultExtensions.IsEmpty(nameof(command.RatingKeys)).LogError();

        // Get server token
        var tokenResult = await _dbContext.GetPlexServerTokenAsync(command.PlexServerId, ct);
        if (tokenResult.IsFailed)
            return tokenResult.ToResult();

        // Get server connection
        var connectionResult = await _dbContext.ChoosePlexServerConnection(command.PlexServerId, ct);
        if (connectionResult.IsFailed)
            return connectionResult.ToResult();

        var client = _plexApiClientFactory.CreateClient(
            tokenResult.Value,
            new PlexApiClientOptions
            {
                ConnectionUrl = connectionResult.Value.Url,
                Timeout = 30,
                RetryCount = 3,
            },
            ct
        );
        // Fetch detailed metadata
        var response = await client
            .Content.GetMetadataItemAsync(new GetMetadataItemRequest { Ids = command.RatingKeys.ToList() })
            .ToResponse(ct);

        if (response.IsCancelledOrFailed)
        {
            _log.Here()
                .Error(
                    "Failed to get metadata for rating keys: {RatingKeys}. Error: {Error}",
                    string.Join(",", command.RatingKeys),
                    response.Errors
                );
            return response.ToResult().LogError();
        }

        var metaDataListResult = Result.Try(() =>
            response.Value.MediaContainerWithMetadata?.MediaContainer?.Metadata ?? []
        );
        if (metaDataListResult.IsFailed)
            return metaDataListResult.LogError();

        return Result.Ok(metaDataListResult.Value.Select(x => x.ToMediaItemDTO()).ToList());
    }
}
