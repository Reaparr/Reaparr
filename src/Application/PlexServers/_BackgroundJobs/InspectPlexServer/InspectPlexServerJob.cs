namespace Reaparr.Application;

public sealed record InspectPlexServerJobPayload(IReadOnlyCollection<int> PlexServerIds);

/// <summary>
/// Executed on a new Plex Account to check all connections and refresh libraries.
/// </summary>
public class InspectPlexServerJob : IJob
{
    private readonly IReaparrDbContextFactory _dbContextFactory;
    private readonly INotificationHubService _notificationHubService;
    private readonly ILogger _log;
    private readonly ICommandExecutor _commandExecutor;
    private readonly ITelemetry _telemetry;

    public static JobKey GetJobKey(int plexServerId) =>
        new($"{nameof(InspectPlexServerJob)}_{plexServerId}", nameof(InspectPlexServerJob));

    public InspectPlexServerJob(
        ILogger log,
        ICommandExecutor commandExecutor,
        IReaparrDbContextFactory dbContextFactory,
        INotificationHubService notificationHubService,
        ITelemetry telemetry
    )
    {
        _log = log.ForContext<InspectPlexServerJob>();
        _commandExecutor = commandExecutor;
        _dbContextFactory = dbContextFactory;
        _notificationHubService = notificationHubService;
        _telemetry = telemetry;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        using var telemetryScope = _telemetry.BeginJob(
            TelemetryActivity.ServerInspection,
            context.GetTelemetryContext()
        );

        var payloadResult = context.GetRequiredPayload<InspectPlexServerJobPayload>();
        if (payloadResult.IsCancelledOrFailed)
        {
            context.SetResult(payloadResult.IsCancelled ? JobStatus.Cancelled : JobStatus.Failed, payloadResult);
            payloadResult.LogIfFailed();
            _telemetry.CaptureErrors(payloadResult, TelemetryActivity.ServerInspection);
            return;
        }

        var cancellationToken = context.CancellationToken;
        var plexServerIds = payloadResult.Value.PlexServerIds;

        _log.Here()
            .Debug(
                "Executing job: {InspectPlexServerJobName} for {Count} servers",
                nameof(InspectPlexServerJob),
                plexServerIds.Count
            );

        var executionResult = await Result.Try(
            async Task<Result> () =>
            {
                var serverTasks = plexServerIds.Select(plexServerId =>
                    InspectPlexServer(plexServerId, cancellationToken)
                );
                var results = await Task.WhenAll(serverTasks);
                var terminalResults = results.Where(x => x.IsCancelledOrFailed).ToList();

                foreach (var result in terminalResults)
                {
                    result.LogIfFailed();
                    _telemetry.CaptureErrors(result, TelemetryActivity.ServerInspection);
                }

                if (terminalResults.Count == 0)
                {
                    _log.Here().Information("Successfully finished the inspection of {Count}", plexServerIds.Count);
                }
                else
                {
                    var terminalResult = terminalResults.FirstOrDefault(x => x.IsCancelled) ?? terminalResults.First();
                    context.SetResult(
                        terminalResult.IsCancelled ? JobStatus.Cancelled : JobStatus.Failed,
                        terminalResult.Errors.FirstOrDefault()?.Message
                    );
                }

                return Result.Ok();
            },
            exception =>
            {
                if (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
                    return new Error(exception.Message).CausedBy(exception);

                return new ExceptionalError(exception);
            }
        );

        if (executionResult.IsCancelledOrFailed)
        {
            context.SetResult(executionResult.IsCancelled ? JobStatus.Cancelled : JobStatus.Failed, executionResult);
            executionResult.LogIfFailed();
            _telemetry.CaptureErrors(executionResult, TelemetryActivity.ServerInspection);
        }
    }

    private async Task<Result> InspectPlexServer(int plexServerId, CancellationToken cancellationToken)
    {
        _telemetry.ServerInspectionStarted(plexServerId);

        var result = await Result.Try(
            async Task<Result> () =>
            {
                // Check all Plex Server Connections
                var checkResult = await _commandExecutor.Send(
                    new CheckAllConnectionsStatusByPlexServerCommand(plexServerId, Timeout: 5),
                    cancellationToken
                );

                if (checkResult.IsCancelledOrFailed)
                    return checkResult.ToResult();

                using var dbContext = await _dbContextFactory.CreateAsync();
                return await RefreshAndSyncLibraries(dbContext, plexServerId, cancellationToken);
            },
            exception =>
            {
                if (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
                    return new Error(exception.Message).CausedBy(exception);

                return new ExceptionalError(exception);
            }
        );

        _telemetry.ServerInspectionFinished(plexServerId, result);
        return result;
    }

    private async Task<Result> RefreshAndSyncLibraries(
        IReaparrDbContext dbContext,
        int plexServerId,
        CancellationToken cancellationToken
    )
    {
        // Refresh accessible libraries
        var accountsResult = await dbContext.GetPlexAccountsWithAccessAsync(plexServerId, cancellationToken);
        if (accountsResult.IsCancelledOrFailed)
            return accountsResult.ToResult();

        var plexAccountId = accountsResult.Value.First().Id;
        var refreshResult = await _commandExecutor.Send(
            new RefreshLibraryAccessCommand(plexAccountId, plexServerId),
            cancellationToken
        );
        if (refreshResult.IsCancelledOrFailed)
            return refreshResult.ToResult();

        // Notify front-end
        await _notificationHubService.SendRefreshNotificationAsync([
            RefreshDataType.PlexAccount,
            RefreshDataType.PlexLibrary,
        ]);

        var libraryIds = await dbContext
            .PlexLibraries.Where(x => x.PlexServerId == plexServerId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        // Sync library media
        return await _commandExecutor.Send(new QueueLibrarySyncJobCommand(libraryIds), cancellationToken);
    }
}
