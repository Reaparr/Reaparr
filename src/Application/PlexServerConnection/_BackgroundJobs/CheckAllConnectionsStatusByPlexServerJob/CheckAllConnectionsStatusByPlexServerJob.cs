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
        await ExecuteCore(context);
    }

    private async Task ExecuteCore(IJobExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;
        var plexServers = await _dbContext
            .PlexServers.Include(x => x.PlexServerConnections)
            .ToListAsync(cancellationToken);

        if (!plexServers.Any())
        {
            return;
        }

        // Send start job status update
        var payload = new CheckAllConnectionStatusUpdateDTO
        {
            PlexServersWithConnectionIds = plexServers.ToDictionary(
                x => x.Id,
                x => x.PlexServerConnections.Select(y => y.Id).ToList()
            ),
        };

        var result = await Result.Try(async Task () =>
        {
            var startedUpdate = context.SetCronJobPayload(JobStatus.Started, payload);
            await _progressHubService.SendJobStatusUpdateAsync(startedUpdate);

            var connectionResults = await Task.WhenAll(
                plexServers.Select(async plexServer =>
                    await Result.Try(() =>
                        _commandExecutor.Send(
                            new CheckAllConnectionsStatusByPlexServerCommand(
                                plexServer.Id,
                                EmitTelemetry: false
                            ),
                            cancellationToken
                        )
                    )
                )
            );

            var cancelledResults = connectionResults.Where(x => x.IsCancelled).ToList();
            foreach (var cancelledResult in cancelledResults)
                cancelledResult.LogWarning();

            var failedResults = connectionResults.Where(x => x.IsFailed && !x.IsCancelled).ToList();
            foreach (var failedResult in failedResults)
            {
                _telemetry.CaptureErrors(failedResult, TelemetryActivity.ScheduledConnectionCheck);
                failedResult.LogError();
            }

            if (cancelledResults.Count > 0 || failedResults.Count > 0)
            {
                context.SetCronJobPayload(cancelledResults.Count > 0 ? JobStatus.Cancelled : JobStatus.Failed, payload);
                return;
            }

            _log.Here()
                .Debug(
                    "{JobName} for servers with ids: {PlexServerIds} completed",
                    nameof(CheckAllConnectionsStatusByPlexServerJob),
                    plexServers.Select(x => x.Id).ToList()
                );
        });

        if (result.IsCancelled)
        {
            context.SetCronJobPayload(JobStatus.Cancelled, payload);
            result.LogWarning();
            return;
        }

        if (result.IsFailed)
        {
            context.SetCronJobPayload(JobStatus.Failed, payload);
            _telemetry.CaptureErrors(result, TelemetryActivity.ScheduledConnectionCheck);
            result.LogError();
        }
    }
}
