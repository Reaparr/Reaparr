namespace Reaparr.Application;

public sealed record MetadataSyncJobUpdateDTO
{
    public required int ServerId { get; init; }
}

public sealed record MetadataSyncJobPayload(int ServerId);

/// <summary>
/// Syncs detailed metadata for a specific Plex server.
/// This job is registered but is not scheduled automatically.
/// </summary>
public class MetadataSyncJob : IJob
{
    private readonly ILogger _log;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IReaparrDbContextFactory _dbContextFactory;
    private readonly ITelemetry _telemetry;

    public MetadataSyncJob(
        ILogger log,
        ICommandExecutor commandExecutor,
        IReaparrDbContextFactory dbContextFactory,
        ITelemetry telemetry
    )
    {
        _log = log.ForContext<MetadataSyncJob>();
        _commandExecutor = commandExecutor;
        _dbContextFactory = dbContextFactory;
        _telemetry = telemetry;
    }

    protected JobTypes JobType => JobTypes.MetadataSyncJob;

    protected List<RefreshDataType> RefreshDataTypes => [RefreshDataType.PlexLibrary];

    public static JobKey GetJobKey(int serverId) =>
        new($"{nameof(JobTypes.MetadataSyncJob)}_{serverId}", nameof(JobTypes.MetadataSyncJob));

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        using var telemetryScope = _telemetry.BeginJob(TelemetryActivity.MetadataSync, context.GetTelemetryContext());

        var payloadResult = context.GetRequiredPayload<MetadataSyncJobPayload>();
        if (payloadResult.IsFailed)
        {
            context.SetResult(JobStatus.Failed, payloadResult);
            payloadResult.LogError();
            return;
        }

        var serverId = payloadResult.Value.ServerId;
        using var dbContext = await _dbContextFactory.CreateAsync();
        var serverName = await dbContext.GetPlexServerNameById(serverId);
        if (!await dbContext.IsServerOnline(serverId))
        {
            _log.Here()
                .Warning("Server {ServerName} ({ServerId}) is offline, skipping metadata sync", serverName, serverId);
            context.SetResult(JobStatus.Completed);
            return;
        }

        var processedCount = 0;
        var movieResult = await _commandExecutor.Send(
            new ProcessMovieMetadataCommand(serverId, serverName),
            cancellationToken
        );
        if (movieResult.IsCancelledOrFailed)
        {
            context.SetResult(movieResult.IsCancelled ? JobStatus.Cancelled : JobStatus.Failed, movieResult);
            movieResult.LogIfFailed();
            _telemetry.MetadataSyncFinished(serverId, processedCount, movieResult);
            _telemetry.CaptureErrors(movieResult, TelemetryActivity.MetadataSync);
            return;
        }

        processedCount += movieResult.Value;

        var episodeResult = await _commandExecutor.Send(
            new ProcessEpisodeMetadataCommand(serverId, serverName),
            cancellationToken
        );
        if (episodeResult.IsCancelledOrFailed)
        {
            context.SetResult(episodeResult.IsCancelled ? JobStatus.Cancelled : JobStatus.Failed, episodeResult);
            episodeResult.LogIfFailed();
            _telemetry.MetadataSyncFinished(serverId, processedCount, episodeResult);
            _telemetry.CaptureErrors(episodeResult, TelemetryActivity.MetadataSync);
            return;
        }

        processedCount += episodeResult.Value;
        context.SetResult(JobStatus.Completed);
        _telemetry.MetadataSyncFinished(serverId, processedCount, Result.Ok());
    }
}
