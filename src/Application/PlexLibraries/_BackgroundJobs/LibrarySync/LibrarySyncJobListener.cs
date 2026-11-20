namespace Reaparr.Application;

public sealed class LibrarySyncJobListener : IJobListener
{
    private readonly ILogger _log;
    private readonly IReaparrDbContextFactory _dbContextFactory;
    private readonly ICommandExecutor _commandExecutor;

    public LibrarySyncJobListener(
        ILogger log,
        IReaparrDbContextFactory dbContextFactory,
        ICommandExecutor commandExecutor
    )
    {
        _log = log.ForContext<LibrarySyncJobListener>();
        _dbContextFactory = dbContextFactory;
        _commandExecutor = commandExecutor;
    }

    public string Name => nameof(LibrarySyncJobListener);

    public ValueTask JobExecutionVetoed(IJobExecutionContext context, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;

    public ValueTask JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;

    public async ValueTask JobWasExecuted(
        IJobExecutionContext context,
        JobExecutionException? jobException,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var payload = context.MergedJobDataMap.GetPayload<LibrarySyncJobPayload>();
            if (payload is null)
            {
                _log.Here()
                    .Error("Library sync job {JobKey} has no valid job data map parameters", context.JobDetail.Key);
                return;
            }

            var serverId = payload.ServerId;
            var libraryId = payload.LibraryId;
            var outcome = BackgroundJobTerminalOutcome.From(context, jobException);
            using (var queueTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                var queueResult = await _commandExecutor.Send(
                    new CheckQueuedPlexLibraryToSyncCommand(),
                    queueTokenSource.Token
                );
                queueResult.LogIfFailed();
            }

            using var dbContext = await _dbContextFactory.CreateAsync();

            var queueStatus = await dbContext
                .LibrarySyncJobQueues.Where(x => x.PlexServerId == serverId && x.PlexLibraryId == libraryId)
                .Select(x => x.Status)
                .FirstOrDefaultAsync(cancellationToken);

            if (outcome.Status == JobStatus.Completed && queueStatus == LibrarySyncJobStatus.Completed)
            {
                var comparisonQueueResult = await _commandExecutor.Send(
                    new ScheduleAffectedLibraryComparisonJobsCommand(libraryId),
                    cancellationToken
                );
                comparisonQueueResult.LogIfFailed();
            }
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _log.Here().Error(exception, "Library sync completion listener failed for {JobKey}", context.JobDetail.Key);
        }
    }
}
