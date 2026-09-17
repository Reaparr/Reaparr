namespace Reaparr.Application;

public record RefreshPlexMovieLibraryCommand(
    InsertMediaMetaDataCommandResponse LibraryMetadata,
    bool ForceMediaRefresh = false
) : ICommand<Result<PlexLibrary>>;

public class RefreshPlexMovieLibraryCommandValidator : AbstractValidator<RefreshPlexMovieLibraryCommand>
{
    public RefreshPlexMovieLibraryCommandValidator()
    {
        RuleFor(x => x.LibraryMetadata).NotNull();
        RuleFor(x => x.LibraryMetadata.PlexLibrary).NotNull();
        RuleFor(x => x.LibraryMetadata.PlexLibraryId).GreaterThan(0);
    }
}

public class RefreshPlexMovieLibraryCommandHandler
    : ICommandHandler<RefreshPlexMovieLibraryCommand, Result<PlexLibrary>>
{
    private readonly ILogger _log;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IReaparrDbContext _dbContext;
    private readonly ILibrarySyncProgressStore _librarySyncProgressStore;

    public RefreshPlexMovieLibraryCommandHandler(
        ILogger log,
        IReaparrDbContext dbContext,
        ILibrarySyncProgressStore librarySyncProgressStore,
        ICommandExecutor commandExecutor
    )
    {
        _log = log.ForContext<RefreshPlexMovieLibraryCommandHandler>();
        _commandExecutor = commandExecutor;
        _dbContext = dbContext;
        _librarySyncProgressStore = librarySyncProgressStore;
    }

    public async Task<Result<PlexLibrary>> ExecuteAsync(
        RefreshPlexMovieLibraryCommand command,
        CancellationToken cancellationToken
    )
    {
        var plexLibrary = command.LibraryMetadata.PlexLibrary;
        var plexLibraryId = command.LibraryMetadata.PlexLibraryId;
        var movieCount = plexLibrary.Movies.Count;

        var syncResult = await _commandExecutor.Send(
            new SyncPlexMoviesCommand(command.LibraryMetadata, command.ForceMediaRefresh),
            cancellationToken
        );

        if (syncResult.IsCancelled)
            return syncResult.ToResult();

        if (syncResult.IsFailed)
        {
            // Report movies as not yet synced on failure
            await _librarySyncProgressStore.UpdateItemAsync(
                plexLibraryId,
                new LibraryProgressItem
                {
                    MediaType = PlexMediaType.Movie,
                    Received = 0,
                    Total = movieCount,
                    TimeRemaining = TimeSpan.Zero,
                },
                cancellationToken
            );

            return syncResult.ToResult().LogError();
        }

        var optimizationResult = await _commandExecutor.Send(
            new ScheduleOptimizeDatabaseJobCommand
            {
                ChangedItemCount =
                    syncResult.Value.CreatedMovies + syncResult.Value.UpdatedMovies + syncResult.Value.DeletedMovies,
            },
            cancellationToken
        );
        optimizationResult.LogIfFailed();

        _log.Here()
            .Information(
                "Successfully refreshed library {PlexLibraryName} with id: {PlexLibraryId}",
                plexLibrary.Title,
                plexLibrary.Id
            );

        // Report movies as successfully synced
        await _librarySyncProgressStore.UpdateItemAsync(
            plexLibraryId,
            new LibraryProgressItem
            {
                MediaType = PlexMediaType.Movie,
                Received = movieCount,
                Total = movieCount,
                TimeRemaining = TimeSpan.Zero,
            },
            cancellationToken
        );

        var rebuildResult = await _commandExecutor.Send(new QueueMediaOverviewRebuildCommand(), cancellationToken);
        rebuildResult.LogIfFailed();

        // Refresh the PlexLibrary from the database to ensure we have the latest data
        var plexLibraryDb = await _dbContext.PlexLibraries.GetAsync(plexLibraryId, cancellationToken);
        return plexLibraryDb is null
            ? ResultExtensions.EntityNotFound(nameof(PlexLibrary), plexLibraryId)
            : Result.Ok(plexLibraryDb);
    }
}
