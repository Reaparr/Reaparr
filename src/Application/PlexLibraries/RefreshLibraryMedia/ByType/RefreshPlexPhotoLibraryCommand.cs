namespace Reaparr.Application;

public record RefreshPlexPhotoLibraryCommand(
    InsertMediaMetaDataCommandResponse LibraryMetadata,
    bool ForceMediaRefresh = false
) : ICommand<Result<PlexLibrary>>;

public class RefreshPlexPhotoLibraryCommandValidator : AbstractValidator<RefreshPlexPhotoLibraryCommand>
{
    public RefreshPlexPhotoLibraryCommandValidator()
    {
        RuleFor(x => x.LibraryMetadata).NotNull();
        RuleFor(x => x.LibraryMetadata.PlexLibrary).NotNull();
        RuleFor(x => x.LibraryMetadata.PlexLibraryId).GreaterThan(0);
        RuleFor(x => x.LibraryMetadata.PlexLibrary.Type).Equal(PlexMediaType.Photos);
    }
}

public class RefreshPlexPhotoLibraryCommandHandler
    : ICommandHandler<RefreshPlexPhotoLibraryCommand, Result<PlexLibrary>>
{
    private readonly ILogger _log;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IReaparrDbContext _dbContext;
    private readonly ILibrarySyncProgressStore _librarySyncProgressStore;

    public RefreshPlexPhotoLibraryCommandHandler(
        ILogger log,
        ICommandExecutor commandExecutor,
        IReaparrDbContext dbContext,
        ILibrarySyncProgressStore librarySyncProgressStore
    )
    {
        _log = log.ForContext<RefreshPlexPhotoLibraryCommandHandler>();
        _commandExecutor = commandExecutor;
        _dbContext = dbContext;
        _librarySyncProgressStore = librarySyncProgressStore;
    }

    public async Task<Result<PlexLibrary>> ExecuteAsync(
        RefreshPlexPhotoLibraryCommand command,
        CancellationToken cancellationToken
    )
    {
        var plexLibrary = command.LibraryMetadata.PlexLibrary;
        var plexLibraryId = command.LibraryMetadata.PlexLibraryId;
        var retrievalResult = await Result.Try(() =>
            _commandExecutor.Send(new GetAllMediaByTypeFromPlexApiCommand(plexLibrary, PlexMediaType.Photos), cancellationToken)
        );
        if (retrievalResult.IsCancelled)
            return retrievalResult.ToResult().LogWarning();
        if (retrievalResult.IsFailed)
        {
            await _librarySyncProgressStore.UpdateErrorAsync(
                plexLibraryId,
                retrievalResult.ToResult(),
                cancellationToken
            );
            return retrievalResult.ToResult().LogError();
        }

        var albumsByKey = plexLibrary.PhotoAlbums.ToDictionary(x => x.PlexApiRatingKey);
        var photos = new List<PlexPhoto>(retrievalResult.Value.Count);
        var clipCount = 0;
        var sortIndices = new Dictionary<int, int>();
        foreach (var source in retrievalResult.Value.OrderByNatural(x => x.SortTitle))
        {
            if (source.Type is not (PlexMediaType.Photos or PlexMediaType.OtherVideos or PlexMediaType.Movie)
                || !int.TryParse(source.ParentRatingKey, out var parentKey)
                || !albumsByKey.TryGetValue(parentKey, out var album))
            {
                var error = Result.Fail("Plex photo does not belong to a retrieved photo album");
                await _librarySyncProgressStore.UpdateErrorAsync(plexLibraryId, error, cancellationToken);
                return error.LogError();
            }

            sortIndices.TryGetValue(parentKey, out var sortIndex);
            source.SortIndex = sortIndices[parentKey] = sortIndex + 1;
            photos.Add(source.ToPlexPhoto(album, plexLibrary));
            if (source.Type is PlexMediaType.OtherVideos or PlexMediaType.Movie)
                clipCount++;
        }

        BuildPhotoTree(plexLibrary, photos);
        command.LibraryMetadata.PhotoClipCount = clipCount;

        var syncResult = await Result.Try(() =>
            _commandExecutor.Send(
                new SyncPlexPhotosCommand(command.LibraryMetadata, command.ForceMediaRefresh),
                cancellationToken
            )
        );

        if (syncResult.IsCancelled)
            return syncResult.ToResult().LogWarning();

        if (syncResult.IsFailed)
        {
            await _librarySyncProgressStore.UpdateErrorAsync(plexLibraryId, syncResult.ToResult(), cancellationToken);
            return syncResult.ToResult().LogError();
        }

        var optimizationResult = await _commandExecutor.Send(
            new ScheduleOptimizeDatabaseJobCommand { ChangedItemCount = syncResult.Value.ChangedItemCount },
            cancellationToken
        );
        optimizationResult.LogIfFailed();

        await _librarySyncProgressStore.UpdateItemAsync(
            plexLibraryId,
            new LibraryProgressItem
            {
                MediaType = PlexMediaType.PhotoAlbum,
                Received = plexLibrary.PhotoAlbums.Count,
                Total = plexLibrary.PhotoAlbums.Count,
                TimeRemaining = TimeSpan.Zero,
            },
            cancellationToken
        );
        await _librarySyncProgressStore.UpdateItemAsync(
            plexLibraryId,
            new LibraryProgressItem
            {
                MediaType = PlexMediaType.Photos,
                Received = plexLibrary.Photos.Count,
                Total = plexLibrary.Photos.Count,
                TimeRemaining = TimeSpan.Zero,
            },
            cancellationToken
        );

        _log.Here()
            .Information(
                "Successfully refreshed library {PlexLibraryName} with id: {PlexLibraryId}",
                plexLibrary.Title,
                plexLibraryId
            );

        var plexLibraryDb = await _dbContext.PlexLibraries.GetAsync(plexLibraryId, cancellationToken);
        return plexLibraryDb is null
            ? ResultExtensions.EntityNotFound(nameof(PlexLibrary), plexLibraryId)
            : Result.Ok(plexLibraryDb);
    }

    private static void BuildPhotoTree(PlexLibrary library, List<PlexPhoto> photos)
    {
        library.Photos.Clear();
        foreach (var album in library.PhotoAlbums)
        {
            album.Photos.Clear();
            album.MediaSize = 0;
        }

        foreach (var photo in photos)
        {
            var album = photo.PlexPhotoAlbum!;
            album.MediaSize += photo.MediaSize;
            album.Photos.Add(photo);
            library.Photos.Add(photo);
        }
    }
}
