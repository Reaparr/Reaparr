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
        RuleFor(x => x.LibraryMetadata.PlexLibrary.Type).Equal(PlexMediaType.PhotoAlbum);
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
            _commandExecutor.Send(
                new GetLibraryMediaFromPlexApiCommand(plexLibrary, PlexMediaType.PhotoImage),
                cancellationToken
            )
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

        var photos = retrievalResult.Value.Library.PhotoImages;
        var treeResult = BuildPhotoTree(plexLibrary, photos);
        if (treeResult.IsFailed)
        {
            await _librarySyncProgressStore.UpdateErrorAsync(plexLibraryId, treeResult, cancellationToken);
            return treeResult.LogError();
        }
        command.LibraryMetadata.PhotoClipCount = retrievalResult.Value.PhotoClipCount;

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
                MediaType = PlexMediaType.PhotoImage,
                Received = plexLibrary.PhotoImages.Count,
                Total = plexLibrary.PhotoImages.Count,
                TimeRemaining = TimeSpan.Zero,
            },
            cancellationToken
        );
        var rebuildResult = await _commandExecutor.Send(new QueueMediaOverviewRebuildCommand(), cancellationToken);
        rebuildResult.LogIfFailed();

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

    private static Result BuildPhotoTree(PlexLibrary library, ICollection<PlexPhotoImage> photos)
    {
        var albums = library.PhotoAlbums.ToDictionary(x => x.PlexApiRatingKey);
        var missingPhoto = photos.FirstOrDefault(x => !albums.ContainsKey(x.ParentKey));
        if (missingPhoto is not null)
            return Result.Fail($"Photo {missingPhoto.PlexApiRatingKey} has an unknown album {missingPhoto.ParentKey}");

        foreach (var album in library.PhotoAlbums)
        {
            album.Photos.Clear();
            album.ChildCount = 0;
            album.Duration = 0;
            album.MediaSize = 0;
            album.Quality = VideoQuality.Unknown;
        }

        foreach (var photo in photos)
        {
            var album = albums[photo.ParentKey];
            photo.PlexPhotoAlbum = album;
            album.Photos.Add(photo);
        }

        foreach (var album in library.PhotoAlbums)
        {
            album.ChildCount = album.Photos.Count;
            album.Duration = album.Photos.Sum(x => x.Duration);
            album.MediaSize = album.Photos.Sum(x => x.MediaSize);
            album.Quality = album.Photos.Count == 0 ? VideoQuality.Unknown : album.Photos.Max(x => x.Quality);
        }

        return Result.Ok();
    }
}
