namespace Reaparr.Application;

public record SetLibraryEnabledRequest
{
    [RouteParam, BindFrom("PlexLibraryId")]
    public int PlexLibraryId { get; init; }

    public bool IsEnabled { get; init; } = true;
}

public class SetLibraryEnabledRequestValidator : Validator<SetLibraryEnabledRequest>
{
    public SetLibraryEnabledRequestValidator()
    {
        RuleFor(x => x.PlexLibraryId).GreaterThan(0);
    }
}

public class SetLibraryEnabledEndpoint : Endpoint<SetLibraryEnabledRequest, ResultDTO<PlexLibraryDTO>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;
    private readonly INotificationHubService _notificationHubService;

    public SetLibraryEnabledEndpoint(
        ILogger log,
        IReaparrDbContext dbContext,
        ICommandExecutor commandExecutor,
        INotificationHubService notificationHubService
    )
    {
        _log = log.ForContext<SetLibraryEnabledEndpoint>();
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
        _notificationHubService = notificationHubService;
    }

    public override void Configure()
    {
        Put(ApiRoutes.PlexLibraryController + "/{PlexLibraryId}/set-library-enabled");

        Description(x =>
            x.Accepts<SetLibraryEnabledRequest>()
                .Produces(StatusCodes.Status200OK, typeof(ResultDTO<PlexLibraryDTO>))
                .Produces(StatusCodes.Status404NotFound, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(SetLibraryEnabledRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);

        var plexLibrary = await _dbContext
            .PlexLibraries.IgnoreIsEnabledFilter()
            .FirstOrDefaultAsync(x => x.Id == req.PlexLibraryId, ct);

        if (plexLibrary is null)
        {
            await Send.FluentResult(ResultExtensions.EntityNotFound(nameof(PlexLibrary), req.PlexLibraryId), ct);
            return;
        }

        var operationResult = req.IsEnabled
            ? await EnableLibraryAsync(plexLibrary, ct)
            : await DisableLibraryAsync(plexLibrary, ct);
        if (operationResult.IsFailed)
        {
            operationResult.LogError();
            await Send.FluentResult(operationResult, ct);
            return;
        }

        // Re-fetch with ignore filter to return updated entity including IsEnabled
        var updatedLibrary = await _dbContext
            .PlexLibraries.AsNoTracking()
            .IgnoreIsEnabledFilter()
            .FirstOrDefaultAsync(x => x.Id == req.PlexLibraryId, ct);

        if (updatedLibrary is null)
        {
            await Send.FluentResult(ResultExtensions.EntityNotFound(nameof(PlexLibrary), req.PlexLibraryId), ct);
            return;
        }

        await Send.FluentResult(Result.Ok(updatedLibrary), x => x.ToDTO(), ct);
    }

    private async Task<Result> EnableLibraryAsync(PlexLibrary plexLibrary, CancellationToken ct)
    {
        _log.Here().Information("Enabling PlexLibrary {PlexLibraryId}", plexLibrary.Id);

        await _dbContext
            .PlexLibraries.IgnoreIsEnabledFilter()
            .Where(x => x.Id == plexLibrary.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, true), ct);

        // Queue a fresh sync
        var queueResult = await _commandExecutor.Send(new QueueLibrarySyncJobCommand([plexLibrary.Id]), ct);
        if (queueResult.IsFailed)
            return queueResult.LogError();

        var rebuildResult = await _commandExecutor.Send(new QueueMediaOverviewRebuildCommand(), ct);
        rebuildResult.LogIfFailed();

        // Notify frontend
        await _notificationHubService.SendRefreshNotificationAsync([
            RefreshDataType.PlexLibrary,
            RefreshDataType.PlexLibrarySyncStatus,
        ]);

        _log.Here().Information("PlexLibrary {PlexLibraryId} enabled and sync queued", plexLibrary.Id);
        return Result.Ok();
    }

    private async Task<Result> DisableLibraryAsync(PlexLibrary plexLibrary, CancellationToken ct)
    {
        _log.Here().Information("Disabling PlexLibrary {PlexLibraryId}", plexLibrary.Id);

        // Cancel queued/processing sync job for this library
        var cancelResult = await _commandExecutor.Send(new CancelLibrarySyncJobCommand(plexLibrary.Id), ct);
        if (cancelResult.IsFailed)
            return cancelResult.LogError();

        // Purge synced media and reset metadata atomically.
        var deleteResult = await _dbContext.ExecuteTransactionAsync(
            async (dbContext, txCt) =>
            {
                var deletedCount = plexLibrary.Type switch
                {
                    PlexMediaType.Movie => await dbContext
                        .PlexMovies.Where(x => x.PlexLibraryId == plexLibrary.Id)
                        .ExecuteDeleteAsync(txCt),
                    PlexMediaType.TvShow => await dbContext
                        .PlexTvShows.Where(x => x.PlexLibraryId == plexLibrary.Id)
                        .ExecuteDeleteAsync(txCt),
                    _ => throw new ArgumentOutOfRangeException(nameof(plexLibrary.Type), plexLibrary.Type, null),
                };

                await dbContext
                    .PlexLibraries.IgnoreIsEnabledFilter()
                    .Where(x => x.Id == plexLibrary.Id)
                    .ExecuteUpdateAsync(
                        x =>
                            x.SetProperty(y => y.IsEnabled, false)
                                .SetProperty(y => y.SyncedAt, (DateTime?)null)
                                .SetProperty(y => y.Outdated, false)
                                .SetProperty(y => y.MovieCount, 0)
                                .SetProperty(y => y.MovieMediaDataCount, 0)
                                .SetProperty(y => y.MediaSize, 0)
                                .SetProperty(y => y.TvShowCount, 0)
                                .SetProperty(y => y.SeasonCount, 0)
                                .SetProperty(y => y.EpisodeCount, 0)
                                .SetProperty(y => y.EpisodeMediaDataCount, 0),
                        txCt
                    );

                return deletedCount;
            },
            ct
        );
        if (deleteResult.IsFailed)
            return deleteResult.ToResult().LogError();

        _log.Here()
            .Information("Purged {Count} media items from library {PlexLibraryId}", deleteResult.Value, plexLibrary.Id);

        var rebuildResult = await _commandExecutor.Send(new QueueMediaOverviewRebuildCommand(), ct);
        rebuildResult.LogIfFailed();

        // Notify frontend
        await _notificationHubService.SendRefreshNotificationAsync([
            RefreshDataType.PlexLibrary,
            RefreshDataType.PlexLibrarySyncStatus,
        ]);

        _log.Here().Information("PlexLibrary {PlexLibraryId} disabled and media purged", plexLibrary.Id);
        return Result.Ok();
    }
}
