using LukeHagar.PlexAPI.SDK;
using LukeHagar.PlexAPI.SDK.Models.Components;
using LukeHagar.PlexAPI.SDK.Models.Requests;

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
    private readonly IReaparrDbContext _dbContext;
    private readonly ILibrarySyncProgressStore _librarySyncProgressStore;
    private readonly IPlexApiClientFactory _plexApiClientFactory;

    public GetAllMediaByTypeFromPlexApiCommandHandler(
        ILogger log,
        IReaparrDbContext dbContext,
        ILibrarySyncProgressStore librarySyncProgressStore,
        IPlexApiClientFactory plexApiClientFactory
    )
    {
        _log = log.ForContext<GetAllMediaByTypeFromPlexApiCommandHandler>();
        _dbContext = dbContext;
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
        var requestMediaType = mediaType.ToPlexMetadataTypeId();

        var tokenResult = await _dbContext.GetPlexServerTokenAsync(plexLibrary.PlexServerId, ct);
        if (tokenResult.IsFailed)
            return tokenResult.ToResult().LogIfFailed();

        var plexServerConnectionResult = await _dbContext.ChoosePlexServerConnection(plexLibrary.PlexServerId, ct);

        if (plexServerConnectionResult.IsFailed)
            return plexServerConnectionResult.ToResult().LogIfFailed();

        var plexServerConnection = plexServerConnectionResult.Value;

        var client = _plexApiClientFactory.CreateClient(
            tokenResult.Value,
            new PlexApiClientOptions
            {
                ConnectionUrl = plexServerConnection.Url,
                Timeout = 30,
                RetryCount = 3,
            }
        );

        var mediaList = new List<LibraryMediaItemDTO>();
        var libraryKey = long.Parse(plexLibrary.Key);

        // Get the total size of the library
        var totalSizeResult = await GetLibraryMediaTotalCount(client, libraryKey, requestMediaType, ct);

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
            var mediaListResult = await GetMetadataForLibraryAsync(
                client,
                libraryKey,
                requestMediaType,
                index,
                Math.Min(batchSize, totalSize - index),
                ct
            );
            if (mediaListResult.IsFailed)
                return mediaListResult.ToResult().LogIfFailed();

            var rawMediaList = mediaListResult.Value;
            mediaList.AddRange(rawMediaList);
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
            },
            cancellationToken
        );
    }

    /// <summary>
    /// Gets the total count of the media in the library.
    /// </summary>
    private static async Task<Result<int>> GetLibraryMediaTotalCount(
        IPlexAPI client,
        long libraryKey,
        int mediaType,
        CancellationToken cancellationToken
    )
    {
        var response = await client
            .Content.ListContentAsync(
                new ListContentRequest
                {
                    MediaType = mediaType,
                    XPlexContainerStart = 0,
                    XPlexContainerSize = 0,
                    SectionId = libraryKey,
                    Sort = "titleSort:asc",
                }
            )
            .ToResponse(cancellationToken);
        if (response.IsFailed)
            return response.ToResult().LogIfFailed();

        var rawValue = response.Value.MediaContainerWithMetadata?.MediaContainer?.TotalSize ?? 0;
        var safeValue = (int)Math.Max(0, Math.Min(rawValue, int.MaxValue));
        return Result.Ok(safeValue);
    }

    /// <summary>
    /// Gets one page of metadata for the requested media type in this Plex library.
    /// <remarks>URL: {{SERVER_URL}}/library/sections/{{LIBRARY_KEY}}/all?X-Plex-Token={{SERVER_TOKEN}}</remarks>
    /// </summary>
    private static async Task<Result<List<LibraryMediaItemDTO>>> GetMetadataForLibraryAsync(
        IPlexAPI client,
        long libraryKey,
        int mediaType,
        int startIndex,
        int pageSize,
        CancellationToken cancellationToken
    )
    {
        var response = await client
            .Content.ListContentAsync(
                new ListContentRequest
                {
                    MediaType = mediaType,
                    XPlexContainerStart = startIndex,
                    XPlexContainerSize = pageSize,
                    SectionId = libraryKey,
                    IncludeGuids = BoolInt.True,
                    IncludeMeta = BoolInt.True,
                    Sort = "titleSort:asc",
                }
            )
            .ToResponse(cancellationToken);
        if (response.IsFailed)
            return response.ToResult().LogIfFailed();

        var metadata = response.Value.MediaContainerWithMetadata?.MediaContainer?.Metadata ?? [];
        return Result.Ok(metadata.Select(x => x.ToMediaItemDTO()).ToList());
    }
}
