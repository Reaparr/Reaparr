namespace Reaparr.PlexApi;

public class GetAllMediaByTypeFromPlexApiCommandValidator : Validator<GetAllMediaByTypeFromPlexApiCommand>
{
    public GetAllMediaByTypeFromPlexApiCommandValidator()
    {
        RuleFor(x => x.BatchSize).GreaterThan(0);
        RuleFor(x => x.PlexLibrary).NotNull();
    }
}

public class GetAllMediaByTypeFromPlexApiCommandHandler
    : ICommandHandler<GetAllMediaByTypeFromPlexApiCommand, Result<List<LibraryMediaItemDTO>>>
{
    private readonly ILogger _log;
    private readonly ILibrarySyncProgressStore _librarySyncProgressStore;
    private readonly IPlexApiClientFactory _plexApiClientFactory;

    public GetAllMediaByTypeFromPlexApiCommandHandler(
        ILogger log,
        ILibrarySyncProgressStore librarySyncProgressStore,
        IPlexApiClientFactory plexApiClientFactory
    )
    {
        _log = log.ForContext<GetAllMediaByTypeFromPlexApiCommandHandler>();
        _librarySyncProgressStore = librarySyncProgressStore;
        _plexApiClientFactory = plexApiClientFactory;
    }

    public async Task<Result<List<LibraryMediaItemDTO>>> ExecuteAsync(
        GetAllMediaByTypeFromPlexApiCommand command,
        CancellationToken ct
    )
    {
        var plexLibrary = command.PlexLibrary;
        var mediaType = command.MediaType;
        var batchSize = command.BatchSize;

        var clientResult = await _plexApiClientFactory.CreateClientAsync(plexLibrary.PlexServerId, 30, 3);
        if (clientResult.IsFailed)
            return clientResult.ToResult().LogIfFailed();

        var client = clientResult.Value;

        var mediaList = new List<LibraryMediaItemDTO>();
        var libraryKey = long.Parse(plexLibrary.Key);
        // Get the total size of the library
        var totalSizeResult = await client.GetLibraryMediaTotalCountAsync(libraryKey, mediaType, ct);

        if (totalSizeResult.IsFailed)
            return totalSizeResult.ToResult().LogIfFailed();

        var totalSize = totalSizeResult.Value;
        if (totalSize == 0)
        {
            _log.Here()
                .Warning("The library with name: {PlexLibraryName} contains no media to retrieve", plexLibrary.Name);
            await SendProgress(plexLibrary.Id, mediaType, DateTime.UtcNow, 0, 0, ct);
            return Result.Ok(mediaList);
        }

        // Retrieve the media for this library
        var startTime = DateTime.UtcNow; // Start time for estimation
        for (var index = 0; index < totalSize; index += batchSize)
        {
            var mediaPageResult = await client.GetMetadataForLibraryAsync(
                libraryKey,
                mediaType,
                index,
                Math.Min(batchSize, totalSize - index),
                ct
            );
            if (mediaPageResult.IsFailed)
                return mediaPageResult.ToResult().LogIfFailed();

            var mediaPage = mediaPageResult.Value;
            if (mediaPage.Total.HasValue)
                totalSize = mediaPage.Total.Value;
            mediaList.AddRange(mediaPage.Items);
            await SendProgress(plexLibrary.Id, mediaType, startTime, mediaList.Count, totalSize, ct);

            if (ct.IsCancellationRequested)
            {
                return ResultExtensions.TaskIsCancelled(nameof(GetAllMediaByTypeFromPlexApiCommand)).LogWarning();
            }
        }

        _log.Here()
            .Information(
                "Finished getting {MediaCount} media items from library with name {PlexLibraryName}  ",
                mediaList.Count,
                plexLibrary.Name
            );

        return Result.Ok(mediaList);
    }

    private async Task SendProgress(
        int plexLibraryId,
        PlexMediaType plexMediaType,
        DateTime startTime,
        int index,
        int totalSize,
        CancellationToken cancellationToken
    )
    {
        var remainingTime = TimeSpan.Zero;
        if (totalSize > 0 && index > 0)
        {
            // Estimate remaining time
            var elapsedTime = DateTime.UtcNow - startTime;
            var progress = (double)index / totalSize;
            var estimatedTotalTime = elapsedTime.TotalSeconds / progress;
            remainingTime = TimeSpan.FromSeconds(estimatedTotalTime - elapsedTime.TotalSeconds);
        }

        // Report progress
        await _librarySyncProgressStore.UpdateItemAsync(
            plexLibraryId,
            new LibraryProgressItem
            {
                MediaType = plexMediaType,
                Received = Math.Clamp(index, 0, totalSize),
                Total = totalSize,
                TimeRemaining = remainingTime,
            }
        );
    }
}
