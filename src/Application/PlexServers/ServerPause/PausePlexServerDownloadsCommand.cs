namespace Reaparr.Application;

public record PausePlexServerDownloadsCommand(int PlexServerId) : ICommand<Result>;

public class PausePlexServerDownloadsCommandValidator : AbstractValidator<PausePlexServerDownloadsCommand>
{
    public PausePlexServerDownloadsCommandValidator()
    {
        RuleFor(x => x.PlexServerId).GreaterThan(0);
    }
}

public class PausePlexServerDownloadsCommandHandler : ICommandHandler<PausePlexServerDownloadsCommand, Result>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IDownloadTaskScheduler _downloadTaskScheduler;
    private readonly ITelemetry _telemetry;

    public PausePlexServerDownloadsCommandHandler(
        ILogger log,
        IReaparrDbContext dbContext,
        ICommandExecutor commandExecutor,
        IDownloadTaskScheduler downloadTaskScheduler,
        ITelemetry telemetry
    )
    {
        _log = log.ForContext<PausePlexServerDownloadsCommandHandler>();
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
        _downloadTaskScheduler = downloadTaskScheduler;
        _telemetry = telemetry;
    }

    public async Task<Result> ExecuteAsync(PausePlexServerDownloadsCommand command, CancellationToken cancellationToken)
    {
        var plexServer = await _dbContext.PlexServers.GetAsync(command.PlexServerId, cancellationToken);
        if (plexServer is null)
            return ResultExtensions.EntityNotFound(nameof(PlexServer), command.PlexServerId).LogError();

        _log.Here()
            .Information(
                "Pausing PlexServer {PlexServerName} with id: {PlexServerId}",
                plexServer.Name,
                command.PlexServerId
            );

        await _dbContext
            .PlexServers.Where(x => x.Id == command.PlexServerId)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.IsDownloadsPausedByUser, true), cancellationToken);

        _telemetry.ServerDownloadControlProcessed(command.PlexServerId, TelemetryAction.Pause, true);
        var downloadingKeys = await _downloadTaskScheduler.GetCurrentlyDownloadingKeysByServer(command.PlexServerId);
        foreach (var downloadKey in downloadingKeys)
        {
            await _commandExecutor.Send(new PauseDownloadTaskCommand(downloadKey.Id), cancellationToken);
        }

        _log.Here()
            .Information(
                "PlexServer {PlexServerName} with id: {PlexServerId} has been paused",
                plexServer.Name,
                command.PlexServerId
            );

        return Result.Ok();
    }
}
