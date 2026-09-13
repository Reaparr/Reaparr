namespace Reaparr.Application;

public record DeletePlexAccountByIdRequest(int PlexAccountId);

public class DeletePlexAccountByIdRequestValidator : Validator<DeletePlexAccountByIdRequest>
{
    public DeletePlexAccountByIdRequestValidator()
    {
        RuleFor(x => x.PlexAccountId).GreaterThan(0);
    }
}

public class DeletePlexAccountByIdEndpoint : Endpoint<DeletePlexAccountByIdRequest, BaseResultDTO>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly INotificationHubService _notificationHubService;
    private readonly IMediaOverviewReadStore _mediaOverviewReadStore;

    public DeletePlexAccountByIdEndpoint(
        ILogger log,
        IReaparrDbContext dbContext,
        INotificationHubService notificationHubService,
        IMediaOverviewReadStore mediaOverviewReadStore
    )
    {
        _log = log.ForContext<DeletePlexAccountByIdEndpoint>();
        _dbContext = dbContext;
        _notificationHubService = notificationHubService;
        _mediaOverviewReadStore = mediaOverviewReadStore;
    }

    public override void Configure()
    {
        Delete(ApiRoutes.PlexAccountController + "/{PlexAccountId}");

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status404NotFound, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(DeletePlexAccountByIdRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);
        var deletedPlexAccountsCount = await _dbContext
            .PlexAccounts.Where(x => x.Id == req.PlexAccountId)
            .ExecuteDeleteAsync(ct);

        if (deletedPlexAccountsCount == 0)
        {
            await Send.FluentResult(
                Result
                    .Fail(
                        "Could not find {PlexAccountName} with id {ReqPlexAccountId} to delete",
                        nameof(PlexAccount),
                        req.PlexAccountId
                    )
                    .LogError(),
                ct
            );
            return;
        }

        var affectedLibraryIds = await _dbContext
            .PlexAccountLibraries.Where(x => x.PlexAccountId == req.PlexAccountId)
            .Select(x => x.PlexLibraryId)
            .ToListAsync(ct);
        var affectedServerIds = await _dbContext
            .PlexAccountServers.Where(x => x.PlexAccountId == req.PlexAccountId)
            .Select(x => x.PlexServerId)
            .ToListAsync(ct);

        await _dbContext.PlexAccountServers.Where(x => x.PlexAccountId == req.PlexAccountId).ExecuteDeleteAsync(ct);
        await _dbContext.PlexAccountLibraries.Where(x => x.PlexAccountId == req.PlexAccountId).ExecuteDeleteAsync(ct);

        // Clean up orphaned PlexServers and PlexLibraries
        var accessibleServerIds = await _dbContext.PlexAccountServers.Select(y => y.PlexServerId).ToListAsync(ct);
        var accessibleLibraryIds = await _dbContext.PlexAccountLibraries.Select(y => y.PlexLibraryId).ToListAsync(ct);

        var deletedServersCount = await _dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .Where(x => !accessibleServerIds.Contains(x.Id))
            .ExecuteDeleteAsync(ct);

        var deletedLibrariesCount = await _dbContext
            .PlexLibraries.Where(x => !accessibleLibraryIds.Contains(x.Id))
            .ExecuteDeleteAsync(ct);

        if (affectedServerIds.Count > 0)
        {
            var serverLibraryIds = await _dbContext
                .PlexLibraries.IgnoreQueryFilters()
                .Where(x => affectedServerIds.Contains(x.PlexServerId))
                .Select(x => x.Id)
                .ToListAsync(ct);
            affectedLibraryIds.AddRange(serverLibraryIds);
        }

        _log.Here()
            .Debug(
                "Deleted {PlexAccount} with Id: {CommandId} from the database, and cleaned up {DeletedServersCount} PlexServers and {DeletedLibrariesCount} PlexLibraries",
                nameof(PlexAccount),
                req.PlexAccountId,
                deletedServersCount,
                deletedLibrariesCount
            );

        await _notificationHubService.SendRefreshNotificationAsync([
            RefreshDataType.PlexAccount,
            RefreshDataType.PlexServer,
            RefreshDataType.PlexServerConnection,
            RefreshDataType.PlexLibrary,
        ]);

        var rebuildResult = await _mediaOverviewReadStore.RebuildAsync(ct);
        rebuildResult.LogIfFailed();

        await Send.FluentResult(rebuildResult, ct);
    }
}
