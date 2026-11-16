namespace Reaparr.Application;

/// <summary>
/// This job will check the status of all connections for a given Plex Server and runs periodically.
/// </summary>
[DisallowConcurrentExecution]
public class CheckAllConnectionsStatusByPlexServerJob : IJob
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IProgressHubService _progressHubService;
    private readonly ITelemetry _telemetry;

    public static JobKey GetJobKey() =>
        new(nameof(CheckAllConnectionsStatusByPlexServerJob), nameof(CheckAllConnectionsStatusByPlexServerJob));

    public CheckAllConnectionsStatusByPlexServerJob(
        ILogger log,
        IReaparrDbContext dbContext,
        ICommandExecutor commandExecutor,
        IProgressHubService progressHubService,
        ITelemetry telemetry
    )
    {
        _log = log.ForContext<CheckAllConnectionsStatusByPlexServerJob>();
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
        _progressHubService = progressHubService;
        _telemetry = telemetry;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        using var telemetryScope = _telemetry.BeginJob(
            TelemetryActivity.ScheduledConnectionCheck,
            context.GetTelemetryContext()
        );

        var cancellationToken = context.CancellationToken;
        var payload = new CheckAllConnectionStatusUpdateDTO { PlexServersWithConnectionIds = [] };
        var result = await Result.Try(
            async Task<Result> () =>
            {
                var plexServers = await _dbContext
                    .PlexServers.Include(x => x.PlexServerConnections)
                    .ToListAsync(cancellationToken);

                if (!plexServers.Any())
                    return Result.Ok();

                payload = new CheckAllConnectionStatusUpdateDTO
                {
                    PlexServersWithConnectionIds = plexServers.ToDictionary(
                        x => x.Id,
                        x => x.PlexServerConnections.Select(y => y.Id).ToList()
                    ),
                };

                // Send start job status update
                var startedUpdate = context.SetCronJobPayload(JobStatus.Started, payload);
                await _progressHubService.SendJobStatusUpdateAsync(startedUpdate);

                var connectionResults = await Task.WhenAll(
                    plexServers.Select(plexServer =>
                        _commandExecutor.Send(
                            new CheckAllConnectionsStatusByPlexServerCommand(plexServer.Id),
                            cancellationToken
                        )
                    )
                );

                var terminalResults = connectionResults.Where(x => x.IsCancelledOrFailed).ToList();
                foreach (var terminalResult in terminalResults)
                {
                    terminalResult.LogIfFailed();
                    _telemetry.CaptureErrors(terminalResult, TelemetryActivity.ScheduledConnectionCheck);
                }

                if (terminalResults.Count > 0)
                {
                    context.SetCronJobPayload(
                        terminalResults.Any(x => x.IsCancelled) ? JobStatus.Cancelled : JobStatus.Failed,
                        payload
                    );
                    return Result.Ok();
                }

                _log.Here()
                    .Debug(
                        "{JobName} for servers with ids: {PlexServerIds} completed",
                        nameof(CheckAllConnectionsStatusByPlexServerJob),
                        plexServers.Select(x => x.Id).ToList()
                    );
                return Result.Ok();
            },
            exception =>
                exception is OperationCanceledException && !cancellationToken.IsCancellationRequested
                    ? new Error(exception.Message).CausedBy(exception)
                    : new ExceptionalError(exception)
        );

        if (result.IsCancelledOrFailed)
        {
            context.SetCronJobPayload(result.IsCancelled ? JobStatus.Cancelled : JobStatus.Failed, payload);
            result.LogIfFailed();
            _telemetry.CaptureErrors(result, TelemetryActivity.ScheduledConnectionCheck);
        }
    }
}
