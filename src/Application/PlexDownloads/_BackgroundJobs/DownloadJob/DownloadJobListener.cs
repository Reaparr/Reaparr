namespace Reaparr.Application;

public sealed class DownloadJobListener : IJobListener
{
    private readonly ILogger _log;
    private readonly IReaparrDbContextFactory _dbContextFactory;
    private readonly IEventPublisher _eventPublisher;
    private readonly IMoveDownloadFileQueue _moveDownloadFileQueue;

    public DownloadJobListener(
        ILogger log,
        IReaparrDbContextFactory dbContextFactory,
        IEventPublisher eventPublisher,
        IMoveDownloadFileQueue moveDownloadFileQueue
    )
    {
        _log = log.ForContext<DownloadJobListener>();
        _dbContextFactory = dbContextFactory;
        _eventPublisher = eventPublisher;
        _moveDownloadFileQueue = moveDownloadFileQueue;
    }

    public string Name => nameof(DownloadJobListener);

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
            var payload = context.MergedJobDataMap.GetPayload<DownloadJobPayload>()?.DownloadTaskKey;
            if (payload is null)
            {
                _log.Here().Error("Download job {JobKey} has no valid payload", context.JobDetail.Key);
                return;
            }

            var outcome = BackgroundJobTerminalOutcome.From(context, jobException);
            using var dbContext = await _dbContextFactory.CreateAsync();
            var status = await dbContext.GetDownloadTaskStatusAsync(payload, cancellationToken);

            if (outcome.Status == JobStatus.Completed && status == DownloadStatus.DownloadFinished)
            {
                _log.Here()
                    .Debug("DownloadTask with id {DownloadTaskId} finished; checking the move queue", payload.Id);
                var moveQueueResult = await _moveDownloadFileQueue.CheckMoveDownloadFileJobQueue(cancellationToken);
                moveQueueResult.LogIfFailed();
            }

            _log.Here()
                .Debug(
                    "DownloadTask with id {DownloadTaskId} ended with job outcome {JobStatus} and download status {DownloadStatus}; checking the download queue",
                    payload.Id,
                    outcome.Status,
                    status
                );
            await _eventPublisher.PublishAsync(new CheckDownloadQueueEvent(payload.PlexServerId), cancellationToken);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _log.Here().Error(exception, "Download completion listener failed for {JobKey}", context.JobDetail.Key);
        }
    }
}
