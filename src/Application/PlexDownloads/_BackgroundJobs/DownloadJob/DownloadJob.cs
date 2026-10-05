using Autofac.Features.Indexed;

namespace Reaparr.Application;

public sealed record DownloadJobPayload(DownloadTaskKey DownloadTaskKey);

[DisallowConcurrentExecution]
public class DownloadJob : IJob
{
    private readonly ILogger _log;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IReaparrDbContext _dbContext;
    private readonly IDownloadTaskUpdateDispatcher _downloadTaskUpdateDispatcher;
    private readonly IEventPublisher _eventPublisher;
    private readonly IIndex<PlexDownloadClientType, IPlexDownloadClient> _plexDownloadClientFactory;

    public DownloadJob(
        ILogger log,
        ICommandExecutor commandExecutor,
        IReaparrDbContext dbContext,
        IDownloadTaskUpdateDispatcher downloadTaskUpdateDispatcher,
        IEventPublisher eventPublisher,
        IIndex<PlexDownloadClientType, IPlexDownloadClient> plexDownloadClientFactory
    )
    {
        _log = log.ForContext<DownloadJob>();
        _commandExecutor = commandExecutor;
        _dbContext = dbContext;
        _downloadTaskUpdateDispatcher = downloadTaskUpdateDispatcher;
        _eventPublisher = eventPublisher;
        _plexDownloadClientFactory = plexDownloadClientFactory;
    }

    public static JobKey GetJobKey(Guid id) => new($"{nameof(DownloadJob)}_{id}", nameof(DownloadJob));

    public async Task Execute(IJobExecutionContext context)
    {
        var payloadResult = context.GetRequiredPayload<DownloadJobPayload>();
        if (payloadResult.IsFailed)
        {
            context.SetResult(JobStatus.Failed, payloadResult);
            payloadResult.LogError();
            return;
        }

        var downloadTaskKey = payloadResult.Value.DownloadTaskKey;
        var token = context.CancellationToken;

        // Preserve handled outcomes for Quartz listeners while containing unexpected exceptions.
        // https://www.quartz-scheduler.net/documentation/best-practices.html#throwing-exceptions
        var executionResult = await Result.Try(async Task<Result> () =>
        {
            _log.Here()
                .Debug(
                    "Executing job: {DownloadJobName} for {DownloadTaskIdName} with id: {DownloadTaskId}",
                    nameof(DownloadJob),
                    nameof(downloadTaskKey),
                    downloadTaskKey
                );

            // Create the multiple download worker tasks which will split up the work
            var downloadTask = await _dbContext.GetDownloadTaskFileAsync(downloadTaskKey, token);
            if (downloadTask is null)
            {
                return ResultExtensions.EntityNotFound(nameof(DownloadTaskFileBase), downloadTaskKey.Id).LogError();
            }

            if (!downloadTask.IsDownloadable || !CanStartDownload(downloadTask.DownloadStatus))
            {
                return Result
                    .Fail(
                        "DownloadTask {DownloadTaskId} is not authorized to start from status {DownloadStatus}",
                        downloadTaskKey,
                        downloadTask.DownloadStatus
                    )
                    .LogWarning();
            }

            var result = await SetDownloadAndDestination(downloadTask, token);
            if (result.IsCancelled)
            {
                result.LogWarning();
                return result.ToResult();
            }

            if (result.IsFailed)
            {
                result.LogError();
                return result.ToResult();
            }

            downloadTask = result.Value;

            var clientTypeResult = await _commandExecutor.Send(
                new DeterminePlexDownloadClientCommand(
                    downloadTask.PlexServerId,
                    downloadTask.ToKey(),
                    $"/library/metadata/{downloadTask.PlexApiRatingKey}"
                ),
                token
            );
            if (clientTypeResult.IsCancelled)
            {
                clientTypeResult.LogWarning();
                return clientTypeResult.ToResult();
            }

            if (clientTypeResult.IsFailed)
            {
                await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
                    downloadTask.ToKey(),
                    DownloadStatus.DownloadClientError,
                    clientTypeResult.ToResult(),
                    token
                );
                await _eventPublisher.PublishAsync(new SendNotificationResult(clientTypeResult.ToResult()), token);
                return clientTypeResult.ToResult();
            }

            var clientType = clientTypeResult.Value;
            _log.Here()
                .Information(
                    "Creating {ClientType} download client for {DownloadTaskFullTitle}",
                    clientType,
                    downloadTask.FullTitle
                );

            var currentStatus = await _dbContext.GetDownloadTaskStatusAsync(downloadTaskKey, token);
            if (!CanStartDownload(currentStatus))
            {
                return Result
                    .Fail(
                        "DownloadTask {DownloadTaskId} is not authorized to start from status {DownloadStatus}",
                        downloadTaskKey,
                        currentStatus
                    )
                    .LogWarning();
            }

            await using var plexDownloadClient = _plexDownloadClientFactory[clientType];

            var startResult = await plexDownloadClient.Start(downloadTask.ToKey(), token);

            if (startResult.IsCancelled)
            {
                _log.Here()
                    .Information(
                        "{DownloadJobName} with {DownloadTaskIdName}: {DownloadTaskId} has been requested to be stopped",
                        nameof(DownloadJob),
                        nameof(downloadTaskKey),
                        downloadTaskKey
                    );
                await plexDownloadClient.StopAsync();

                await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
                    downloadTaskKey,
                    DownloadStatus.Paused,
                    CancellationToken.None
                );
            }
            else if (startResult.IsFailed)
            {
                var failedStatus =
                    startResult.HasPlex401UnauthorizedError() ? DownloadStatus.AuthError
                    : startResult.Has404NotFoundError() ? DownloadStatus.SourceUnavailable
                    : startResult.IsServerUnreachable() ? DownloadStatus.ServerUnreachable
                    : startResult.HasStorageError() ? DownloadStatus.StorageError
                    : DownloadStatus.DownloadClientError;

                var persistedStatus = await _dbContext.GetDownloadTaskStatusAsync(
                    downloadTask.ToKey(),
                    CancellationToken.None
                );
                if (persistedStatus != failedStatus)
                {
                    await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
                        downloadTask.ToKey(),
                        failedStatus,
                        startResult,
                        CancellationToken.None
                    );
                }

                await _eventPublisher.PublishAsync(new SendNotificationResult(startResult), token);
            }

            return startResult;
        });

        if (executionResult.IsCancelled)
        {
            context.SetResult(JobStatus.Cancelled, executionResult);
            executionResult.LogWarning();
        }
        else if (executionResult.IsFailed)
        {
            context.SetResult(JobStatus.Failed, executionResult);
            executionResult.LogError();
        }

        _log.Here()
            .Debug(
                "Exiting job: {DownloadJobName} for {DownloadTaskName} with id: {DownloadTaskId}",
                nameof(DownloadJob),
                nameof(DownloadTaskGeneric),
                downloadTaskKey
            );
    }

    private static bool CanStartDownload(DownloadStatus status) =>
        status
            is DownloadStatus.Queued
                or DownloadStatus.AutoPaused
                or DownloadStatus.Restarting
                or DownloadStatus.Error
                or DownloadStatus.ServerUnreachable
                or DownloadStatus.AuthError
                or DownloadStatus.StorageError
                or DownloadStatus.SourceUnavailable
                or DownloadStatus.DownloadClientError
                or DownloadStatus.IntegrityError;

    private async Task<Result<DownloadTaskFileBase>> SetDownloadAndDestination(
        DownloadTaskFileBase downloadTask,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(downloadTask.DirectoryMeta.DownloadRootPath))
        {
            var integration = (
                downloadTask.SonarrIntegrationId,
                downloadTask.RadarrIntegrationId
            ).ToIntegrationIdentity();
            var downloadFolder = await _dbContext.GetDownloadFolder(integration);
            downloadTask.DirectoryMeta.DownloadRootPath = downloadFolder.DirectoryPath;
        }

        // A custom destination folder can have been set during creation
        if (string.IsNullOrEmpty(downloadTask.DirectoryMeta.DestinationRootPath))
        {
            FolderPath? destinationFolder = null;
            if (downloadTask.DestinationFolderPathId is not null && downloadTask.DestinationFolderPathId > 0)
            {
                destinationFolder = await _dbContext.FolderPaths.GetAsync(
                    (int)downloadTask.DestinationFolderPathId,
                    cancellationToken
                );
            }

            destinationFolder ??= await _dbContext.GetDestinationFolder(downloadTask.PlexLibraryId);

            if (destinationFolder is null)
                return ResultExtensions.EntityNotFound(nameof(PlexLibrary), downloadTask.PlexLibraryId).LogError();

            downloadTask.DirectoryMeta.DestinationRootPath = destinationFolder.DirectoryPath;
        }

        switch (downloadTask.DownloadTaskType)
        {
            case DownloadTaskType.MovieData:
                await _dbContext
                    .DownloadTaskMovieFile.Where(x => x.Id == downloadTask.Id)
                    .ExecuteUpdateAsync(
                        p => p.SetProperty(x => x.DirectoryMeta, downloadTask.DirectoryMeta),
                        cancellationToken
                    );
                break;
            case DownloadTaskType.EpisodeData:
                await _dbContext
                    .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == downloadTask.Id)
                    .ExecuteUpdateAsync(
                        p => p.SetProperty(x => x.DirectoryMeta, downloadTask.DirectoryMeta),
                        cancellationToken
                    );
                break;
            case DownloadTaskType.PhotoData:
            case DownloadTaskType.PhotoPart:
                await _dbContext
                    .DownloadTaskPhotoImageFiles.Where(x => x.Id == downloadTask.Id)
                    .ExecuteUpdateAsync(
                        p => p.SetProperty(x => x.DirectoryMeta, downloadTask.DirectoryMeta),
                        cancellationToken
                    );
                break;
            case DownloadTaskType.MusicTrackData:
            case DownloadTaskType.MusicTrackPart:
                await _dbContext
                    .DownloadTaskMusicTrackFiles.Where(x => x.Id == downloadTask.Id)
                    .ExecuteUpdateAsync(
                        p => p.SetProperty(x => x.DirectoryMeta, downloadTask.DirectoryMeta),
                        cancellationToken
                    );
                break;
            case DownloadTaskType.OtherVideoData:
            case DownloadTaskType.OtherVideoPart:
                await _dbContext
                    .DownloadTaskOtherVideoFiles.Where(x => x.Id == downloadTask.Id)
                    .ExecuteUpdateAsync(
                        p => p.SetProperty(x => x.DirectoryMeta, downloadTask.DirectoryMeta),
                        cancellationToken
                    );
                break;
            default:
                return Result.Fail(
                    "DownloadTaskType {DownloadTaskType} is not supported",
                    downloadTask.DownloadTaskType
                );
        }

        return Result.Ok(downloadTask);
    }
}
