namespace Reaparr.Application;

public record RefreshPlexPhotoLibraryCommand(
    InsertMediaMetaDataCommandResponse LibraryMetadata,
    bool ForceMediaRefresh = false
) : ICommand<Result<PlexLibrary>>;

public class RefreshPlexPhotoLibraryCommandValidator : AbstractValidator<RefreshPlexPhotoLibraryCommand>
{
    public RefreshPlexPhotoLibraryCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.LibraryMetadata).NotNull();
        RuleFor(x => x.LibraryMetadata.PlexLibrary).NotNull();
        RuleFor(x => x.LibraryMetadata.PlexLibrary.Id).GreaterThan(0);
        RuleFor(x => x.LibraryMetadata.PlexLibrary.Type).Equal(PlexMediaType.PhotoAlbum);
        RuleFor(x => x.LibraryMetadata.PlexLibrary.PlexServerId).GreaterThan(0);
    }
}

public class RefreshPlexPhotoLibraryCommandHandler : ICommandHandler<RefreshPlexPhotoLibraryCommand, Result<PlexLibrary>>
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

    public async Task<Result<PlexLibrary>> ExecuteAsync(RefreshPlexPhotoLibraryCommand command, CancellationToken ct)
    {
        var library = command.LibraryMetadata.PlexLibrary;
        await _librarySyncProgressStore.StartAsync(library.Id, PlexMediaType.PhotoAlbum);

        var syncResult = await Result.Try(() =>
            _commandExecutor.Send(new SyncPlexPhotosCommand(command.LibraryMetadata, command.ForceMediaRefresh), ct)
        );
        if (syncResult.IsCancelled)
            return syncResult.ToResult().LogWarning();
        if (syncResult.IsFailed)
        {
            await _librarySyncProgressStore.UpdateErrorAsync(library.Id, syncResult.ToResult(), ct);
            return syncResult.ToResult().LogError();
        }

        // The catalog transaction has committed; postcommit progress and maintenance are best effort.
        var optimizationResult = await Result.Try(() =>
            _commandExecutor.Send(
                new ScheduleOptimizeDatabaseJobCommand { ChangedItemCount = syncResult.Value.ChangedItemCount },
                ct
            )
        );
        optimizationResult.LogIfFailed();

        var albumProgressResult = await Result.Try(async Task () =>
            await _librarySyncProgressStore.UpdateItemAsync(
                library.Id,
                new LibraryProgressItem
                {
                    MediaType = PlexMediaType.PhotoAlbum,
                    Received = library.PhotoAlbums.Count,
                    Total = library.PhotoAlbums.Count,
                    TimeRemaining = TimeSpan.Zero,
                },
                ct
            )
        );
        albumProgressResult.LogIfFailed();

        var imageProgressResult = await Result.Try(async Task () =>
            await _librarySyncProgressStore.UpdateItemAsync(
                library.Id,
                new LibraryProgressItem
                {
                    MediaType = PlexMediaType.PhotoImage,
                    Received = library.PhotoMediaData.Count,
                    Total = library.PhotoMediaData.Count,
                    TimeRemaining = TimeSpan.Zero,
                },
                ct
            )
        );
        imageProgressResult.LogIfFailed();

        var rebuildResult = await Result.Try(() =>
            _commandExecutor.Send(new QueueMediaOverviewRebuildCommand(PlexMediaType.PhotoAlbum), ct)
        );
        rebuildResult.LogIfFailed();

        _log.Here().Information("Successfully refreshed library {PlexLibraryName} with id: {PlexLibraryId}", library.Title, library.Id);

        var plexLibraryDb = await _dbContext.PlexLibraries.FirstOrDefaultAsync(
            x => x.Id == library.Id,
            CancellationToken.None
        );
        return plexLibraryDb is null
            ? ResultExtensions.EntityNotFound(nameof(PlexLibrary), library.Id)
            : Result.Ok(plexLibraryDb);
    }
}
