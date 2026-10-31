using System.Reactive.Subjects;
using System.Threading.Channels;

namespace Reaparr.Application;

public record MoveDownloadFileFromFileTaskCommand(
    DownloadTaskKey Key,
    Subject<IDownloadFileTransferProgress>? MoveDownloadFileProgress = null
) : ICommand<Result>;

public class MoveDownloadFileFromFileTaskCommandValidator : AbstractValidator<MoveDownloadFileFromFileTaskCommand>
{
    public MoveDownloadFileFromFileTaskCommandValidator()
    {
        RuleFor(x => x.Key).NotNull().DependentRules(() => RuleFor(x => x.Key.Id).NotEqual(Guid.Empty));
    }
}

public class MoveDownloadFileFromFileTaskCommandHandler : ICommandHandler<MoveDownloadFileFromFileTaskCommand, Result>
{
    private readonly ILogger _log;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IEventPublisher _eventPublisher;
    private readonly IDownloadTaskUpdateDispatcher _downloadTaskUpdateDispatcher;
    private readonly IReaparrDbContext _dbContext;

    private readonly IReaparrDbContextFactory _dbContextFactory;
    private readonly IFile _file;
    private readonly IDirectory _directory;
    private readonly IPath _path;
    private readonly IDownloadManagerSettings _downloadManagerSettings;

    public MoveDownloadFileFromFileTaskCommandHandler(
        ILogger log,
        ICommandExecutor commandExecutor,
        IEventPublisher eventPublisher,
        IDownloadTaskUpdateDispatcher downloadTaskUpdateDispatcher,
        IReaparrDbContext dbContext,
        IReaparrDbContextFactory dbContextFactory,
        IFile file,
        IDirectory directory,
        IPath path,
        IDownloadManagerSettings downloadManagerSettings
    )
    {
        _log = log.ForContext<MoveDownloadFileFromFileTaskCommandHandler>();
        _commandExecutor = commandExecutor;
        _eventPublisher = eventPublisher;
        _downloadTaskUpdateDispatcher = downloadTaskUpdateDispatcher;
        _dbContext = dbContext;
        _dbContextFactory = dbContextFactory;
        _file = file;
        _directory = directory;
        _path = path;
        _downloadManagerSettings = downloadManagerSettings;
    }

    public async Task<Result> ExecuteAsync(
        MoveDownloadFileFromFileTaskCommand command,
        CancellationToken cancellationToken
    )
    {
        var key = command.Key;
        var moveDownloadFileProgress = command.MoveDownloadFileProgress;

        var downloadTask = await _dbContext.GetDownloadTaskFileAsync(command.Key, cancellationToken);
        if (downloadTask == null)
            return ResultExtensions.EntityNotFound(nameof(DownloadTaskGeneric), command.Key.Id).LogError();

        // Resolve paths
        var downloadFilePath = downloadTask.DownloadFilePath;
        var destinationPath = downloadTask.DestinationFilePath;

        _log.Here().Debug("Starting file move process for {DownloadFilePath}", downloadFilePath);

        if (string.IsNullOrWhiteSpace(downloadFilePath) || !_file.Exists(downloadFilePath))
        {
            _log.Here()
                .Debug(
                    "Source file not found at expected path for {DownloadTaskId}, checking fallback locations",
                    key.Id
                );

            string? movedInDownloadsPath = null;
            if (!string.IsNullOrWhiteSpace(downloadFilePath))
                movedInDownloadsPath = downloadFilePath.RemoveReapTempSuffix();

            var destinationExists = !string.IsNullOrWhiteSpace(destinationPath) && _file.Exists(destinationPath);
            var movedInDownloadsExists =
                !string.IsNullOrWhiteSpace(movedInDownloadsPath) && _file.Exists(movedInDownloadsPath);

            if (movedInDownloadsExists && ShouldKeepInDownloads(downloadTask))
            {
                var movedInDownloadsSizeResult = GetFileLength(movedInDownloadsPath!);
                if (movedInDownloadsSizeResult.IsFailed)
                    return await ErrorDownloadTask(key, movedInDownloadsSizeResult.ToResult());

                if (movedInDownloadsSizeResult.Value != downloadTask.DataTotal)
                {
                    var result = Result.Fail(
                        "Renamed download file has an unexpected size. Expected {ExpectedBytes} bytes but found {ActualBytes} bytes at '{Path}'",
                        downloadTask.DataTotal,
                        movedInDownloadsSizeResult.Value,
                        movedInDownloadsPath!
                    );
                    return await ErrorDownloadTask(key, result);
                }

                _log.Here()
                    .Warning(
                        "Source file was missing for {DownloadTaskId}, but renamed file exists in downloads and keep-in-downloads is set. Treating move as finished",
                        key.Id
                    );

                downloadTask.CurrentFileTransferBytesOffset = downloadTask.DataTotal;
                downloadTask.FileDataTransferred = downloadTask.DataTotal;
                downloadTask.TimeRemaining = 0;
                await _dbContext.UpdateDownloadFileTransferProgress(
                    key,
                    downloadTask.ToFileTransferProgress(),
                    CancellationToken.None
                );
                moveDownloadFileProgress?.OnNext(downloadTask.ToFileTransferProgress());

                await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
                    key,
                    DownloadStatus.MoveFinished,
                    CancellationToken.None
                );
                _log.Here()
                    .Debug(
                        "Move marked finished via renamed downloads file (keep-in-downloads) for {DownloadTaskId}",
                        key.Id
                    );
                return Result.Ok();
            }

            if (movedInDownloadsExists)
            {
                // The .reaptemp rename already happened but the file was not moved to the destination yet.
                // Resume the move using the renamed file as the source.
                _log.Here()
                    .Warning(
                        "Source .reaptemp file was missing for {DownloadTaskId} but renamed file exists in downloads. Resuming move to destination",
                        key.Id
                    );
                downloadFilePath = movedInDownloadsPath!;
            }
            else if (destinationExists)
            {
                var destinationSizeResult = GetFileLength(destinationPath!);
                if (destinationSizeResult.IsFailed)
                    return await ErrorDownloadTask(key, destinationSizeResult.ToResult());

                if (destinationSizeResult.Value != downloadTask.DataTotal)
                {
                    var result = Result.Fail(
                        "Existing destination file has an unexpected size. Expected {ExpectedBytes} bytes but found {ActualBytes} bytes at '{Path}'",
                        downloadTask.DataTotal,
                        destinationSizeResult.Value,
                        destinationPath
                    );
                    return await ErrorDownloadTask(key, result);
                }

                _log.Here()
                    .Warning(
                        "Source file was missing for {DownloadTaskId}, but a completed file already exists at destination. Treating move as finished",
                        key.Id
                    );

                downloadTask.CurrentFileTransferBytesOffset = downloadTask.DataTotal;
                downloadTask.FileDataTransferred = downloadTask.DataTotal;
                downloadTask.TimeRemaining = 0;
                await _dbContext.UpdateDownloadFileTransferProgress(
                    key,
                    downloadTask.ToFileTransferProgress(),
                    CancellationToken.None
                );
                moveDownloadFileProgress?.OnNext(downloadTask.ToFileTransferProgress());

                await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
                    key,
                    DownloadStatus.MoveFinished,
                    CancellationToken.None
                );
                _log.Here().Debug("Move marked finished via existing destination file for {DownloadTaskId}", key.Id);
                return Result.Ok();
            }
            else
            {
                var result = Result
                    .Fail("Source file does not exist and cannot be moved: {DownloadFilePath}", downloadFilePath)
                    .LogError();
                return await ErrorDownloadTask(key, result);
            }
        }

        try
        {
            if (downloadFilePath.RemoveReapTempSuffix() == destinationPath)
            {
                // Just rename it to remove .reapTemp suffix if present
                _log.Here()
                    .Debug(
                        "Source and destination resolve to the same path for {DownloadTaskId} — renaming in-place to strip .reaptemp suffix",
                        key.Id
                    );

                if (_file.Exists(destinationPath))
                {
                    _log.Here()
                        .Debug("Deleting pre-existing destination file before rename for {DownloadTaskId}", key.Id);
                    Result.Try(() => _file.Delete(destinationPath)).LogIfFailed();
                }

                _file.Move(downloadFilePath, destinationPath);

                downloadTask.CurrentFileTransferBytesOffset = downloadTask.DataTotal;
                downloadTask.FileDataTransferred = downloadTask.DataTotal;
                downloadTask.TimeRemaining = 0;
                await _dbContext.UpdateDownloadFileTransferProgress(
                    key,
                    downloadTask.ToFileTransferProgress(),
                    CancellationToken.None
                );
                moveDownloadFileProgress?.OnNext(downloadTask.ToFileTransferProgress());

                // The rename completed; persist its terminal state even when the caller has cancelled.
                await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
                    key,
                    DownloadStatus.MoveFinished,
                    CancellationToken.None
                );
                _log.Here().Debug("In-place rename succeeded for {DownloadTaskId}", key.Id);
                return Result.Ok();
            }

            // Determine if we should keep the file in the downloads directory
            var destinationDirectoryPath = _path.GetDirectoryName(destinationPath);
            var keepInDownloads = ShouldKeepInDownloads(downloadTask);

            _log.Here()
                .Debug(
                    "Move strategy for {DownloadTaskId}: KeepInDownloads={KeepInDownloads}, Source={SourcePath}, Destination={DestinationPath}",
                    key.Id,
                    keepInDownloads,
                    downloadFilePath,
                    destinationPath
                );

            if (keepInDownloads)
            {
                // Rename it to remove .reapTemp suffix if present
                var targetInDownloads = downloadFilePath.RemoveReapTempSuffix();
                _log.Here()
                    .Debug(
                        "Renaming to remove .reaptemp suffix in downloads folder for {DownloadTaskId}: {TargetPath}",
                        key.Id,
                        targetInDownloads
                    );

                if (_file.Exists(targetInDownloads))
                {
                    _log.Here()
                        .Debug("Deleting pre-existing target in downloads before rename for {DownloadTaskId}", key.Id);
                    Result.Try(() => _file.Delete(targetInDownloads)).LogIfFailed();
                }

                _file.Move(downloadFilePath, targetInDownloads);

                downloadTask.CurrentFileTransferBytesOffset = downloadTask.DataTotal;
                downloadTask.FileDataTransferred = downloadTask.DataTotal;
                downloadTask.TimeRemaining = 0;
                await _dbContext.UpdateDownloadFileTransferProgress(
                    key,
                    downloadTask.ToFileTransferProgress(),
                    CancellationToken.None
                );
                moveDownloadFileProgress?.OnNext(downloadTask.ToFileTransferProgress());

                // The rename completed; persist its terminal state even when the caller has cancelled.
                await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
                    key,
                    DownloadStatus.MoveFinished,
                    CancellationToken.None
                );
                _log.Here()
                    .Debug(
                        "Keep-in-downloads rename succeeded for {DownloadTaskId}: {TargetPath}",
                        key.Id,
                        targetInDownloads
                    );
                return Result.Ok();
            }

            // Ensure destination directory exists
            var directoryPathResult = Result.Try(() => _path.GetDirectoryName(destinationPath));
            if (directoryPathResult.IsFailed)
                return await ErrorDownloadTask(key, directoryPathResult.ToResult());

            if (string.IsNullOrEmpty(directoryPathResult.Value))
                return await ErrorDownloadTask(key, Result.Fail("Could not determine the directory name of path"));

            // Ensure the destination directory exists only when we are actually moving
            _log.Here()
                .Debug(
                    "Ensuring destination directory exists for {DownloadTaskId}: {DestinationDirectory}",
                    key.Id,
                    destinationDirectoryPath
                );
            var createDirectoryResult = Result
                .Try((() => _directory.CreateDirectory(destinationDirectoryPath!)))
                .ToResult();
            if (createDirectoryResult.IsFailed)
                return await ErrorDownloadTask(key, createDirectoryResult);

            var isResumingMove =
                downloadTask.DownloadStatus is DownloadStatus.MovePaused or DownloadStatus.AutoMovePaused;

            // Fresh moves intentionally overwrite any stale destination. Paused moves may only resume
            // when their durable offset is confirmed by the on-disk destination length.
            var destinationAlreadyExists = _file.Exists(destinationPath);
            if (isResumingMove)
            {
                if (destinationAlreadyExists)
                {
                    var destinationSizeResult = GetFileLength(destinationPath!);
                    if (destinationSizeResult.IsFailed)
                        return await ErrorDownloadTask(key, destinationSizeResult.ToResult());

                    if (destinationSizeResult.Value != downloadTask.CurrentFileTransferBytesOffset)
                    {
                        var result = Result.Fail(
                            "Paused move cannot resume because destination length {ActualBytes} does not match stored offset {ExpectedBytes} at '{Path}'",
                            destinationSizeResult.Value,
                            downloadTask.CurrentFileTransferBytesOffset,
                            destinationPath
                        );
                        return await ErrorDownloadTask(key, result);
                    }
                }
                else if (downloadTask.CurrentFileTransferBytesOffset != 0)
                {
                    var result = Result.Fail(
                        "Paused move cannot resume because destination '{Path}' is missing for stored offset {ExpectedBytes}",
                        destinationPath,
                        downloadTask.CurrentFileTransferBytesOffset
                    );
                    return await ErrorDownloadTask(key, result);
                }
            }
            else if (destinationAlreadyExists)
            {
                _log.Here()
                    .Debug(
                        "Destination file already exists for {DownloadTaskId}, deleting to start fresh: {DestinationPath}",
                        key.Id,
                        destinationPath
                    );
                var deleteExistingResult = Result.Try(() => _file.Delete(destinationPath));
                if (deleteExistingResult.IsFailed)
                    return await ErrorDownloadTask(key, deleteExistingResult);

                // Reset offset to start fresh since we removed existing target
                downloadTask.CurrentFileTransferBytesOffset = 0;
                downloadTask.FileDataTransferred = 0;
            }

            // Update status before moving
            await _dbContext.CreateDownloadClientLog(
                key,
                NotificationLevel.Information,
                DownloadStatus.Moving,
                $"Preparing to move download file from '{downloadFilePath}' to '{destinationPath}'"
            );
            await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(key, DownloadStatus.Moving, cancellationToken);

            _log.Here()
                .Debug(
                    "Starting move for {DownloadTaskId}: {SourcePath} -> {DestinationPath} (offset: {Offset} bytes)",
                    key.Id,
                    downloadFilePath,
                    destinationPath,
                    downloadTask.CurrentFileTransferBytesOffset
                );

            var moveResult = await MoveWithResumeAsync(
                downloadTask,
                downloadFilePath,
                destinationPath,
                key,
                cancellationToken
            );
            if (moveResult.IsCancelled)
            {
                _log.Here().Warning("Move was cancelled for file task {FileTaskId}", key.Id);

                // The move has already stopped; persist its resumable state after the cancellation boundary.
                await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
                    key,
                    DownloadStatus.MovePaused,
                    CancellationToken.None
                );

                await _dbContext.UpdateDownloadFileTransferProgress(
                    key,
                    downloadTask.ToFileTransferProgress(),
                    CancellationToken.None
                );
                moveDownloadFileProgress?.OnNext(downloadTask.ToFileTransferProgress());

                return moveResult.LogWarning();
            }

            if (moveResult.IsFailed)
                return await ErrorDownloadTask(key, moveResult);

            _log.Here().Debug("Resumable move completed successfully for {DownloadTaskId}", key.Id);

            // Instant finish on rename
            downloadTask.CurrentFileTransferBytesOffset = downloadTask.DataTotal;
            downloadTask.FileDataTransferred = downloadTask.DataTotal;
            downloadTask.TimeRemaining = 0;
            await _dbContext.UpdateDownloadFileTransferProgress(
                key,
                downloadTask.ToFileTransferProgress(),
                CancellationToken.None
            );
            moveDownloadFileProgress?.OnNext(downloadTask.ToFileTransferProgress());

            // The move completed; persist its terminal state even when the caller has cancelled.
            await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
                key,
                DownloadStatus.MoveFinished,
                CancellationToken.None
            );
            _log.Here().Debug("Move finished for {DownloadTaskId}: {DestinationPath}", key.Id, destinationPath);
        }
        catch (OperationCanceledException)
        {
            _log.Here().Warning("The file move operation was cancelled for file task {FileTaskId}", key.Id);

            // The move has already stopped; persist its resumable state after the cancellation boundary.
            await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
                key,
                DownloadStatus.MovePaused,
                CancellationToken.None
            );
            downloadTask.TimeRemaining = 0;
            await _dbContext.UpdateDownloadFileTransferProgress(
                key,
                downloadTask.ToFileTransferProgress(),
                CancellationToken.None
            );
            moveDownloadFileProgress?.OnNext(downloadTask.ToFileTransferProgress());

            return ResultExtensions.TaskIsCancelled(nameof(MoveDownloadFileFromFileTaskCommandHandler)).LogWarning();
        }
        catch (Exception ex)
        {
            _log.Here().Error(ex, "An error occurred while moving file for file task {FileTaskId}", key.Id);
            return await ErrorDownloadTask(key, Result.Fail(new ExceptionalError(ex)));
        }
        finally
        {
            moveDownloadFileProgress?.OnCompleted();
        }

        return Result.Ok();
    }

    private async Task<Result> MoveWithResumeAsync(
        DownloadTaskFileBase downloadTask,
        string sourcePath,
        string targetPath,
        DownloadTaskKey key,
        CancellationToken cancellationToken
    )
    {
        var progressChannel = Channel.CreateBounded<IDownloadFileTransferProgress>(
            new BoundedChannelOptions(1)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.DropOldest,
            }
        );

        var consumerTask = Task.Run(
            async () =>
            {
                await foreach (var dto in progressChannel.Reader.ReadAllAsync(cancellationToken))
                {
                    try
                    {
                        using var contextProgress = await _dbContextFactory.CreateAsync();
                        await contextProgress.UpdateDownloadFileTransferProgress(
                            key,
                            dto,
                            cancellationToken: cancellationToken
                        );
                        _downloadTaskUpdateDispatcher.NotifyFileTransferProgress(key);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _log.Here().Error(ex, "Error while processing file transfer progress for {FileTaskId}", key.Id);
                    }
                }
            },
            CancellationToken.None
        );

        var stopwatch = Stopwatch.StartNew();

        void SendProgress(MoveFileTransferProgressDTO progress)
        {
            downloadTask.CurrentFileTransferBytesOffset = progress.Transferred;
            downloadTask.FileDataTransferred = progress.Transferred;
            downloadTask.FileTransferSpeed = progress.FileTransferSpeed;
            downloadTask.TimeRemaining = DataFormat.GetTimeRemaining(
                Math.Max(0, downloadTask.DataTotal - progress.Transferred),
                progress.FileTransferSpeed
            );
            var rawPercent =
                downloadTask.DataTotal > 0 ? (decimal)progress.Transferred / downloadTask.DataTotal * 100m : 0m;
            downloadTask.Percentage = Math.Clamp(rawPercent, 0m, 100m);

            if (stopwatch.ElapsedMilliseconds > 1000)
            {
                // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
                _log.Here().Debug(downloadTask.ToString());
                var fileTransferProgress = downloadTask.ToFileTransferProgress();
                progressChannel.Writer.TryWrite(fileTransferProgress);
                stopwatch.Restart();
            }
        }

        try
        {
            var result = await _commandExecutor.Send(
                new MoveFileWithResumeCommand
                {
                    SourcePath = sourcePath,
                    TargetPath = targetPath,
                    Progress = SendProgress,
                    CurrentOffset = downloadTask.CurrentFileTransferBytesOffset,
                    DataTotal = downloadTask.DataTotal,
                },
                cancellationToken
            );

            return result;
        }
        finally
        {
            progressChannel.Writer.TryComplete();

            try
            {
                await consumerTask;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Ignore cancellation during progress channel completion.
            }
        }
    }

    private async Task<Result> ErrorDownloadTask(DownloadTaskKey key, Result result)
    {
        await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(key, DownloadStatus.MoveError, result);

        await _eventPublisher.PublishAsync(new SendNotificationResult(result), CancellationToken.None);

        return result.LogError();
    }

    private Result<long> GetFileLength(string path) => Result.Try(() => _file.FileSystem.FileInfo.New(path).Length);

    private bool ShouldKeepInDownloads(DownloadTaskFileBase downloadTask) =>
        downloadTask.DirectoryMeta.KeepCompletedInDownloadFolder
        || _downloadManagerSettings.KeepCompletedInDownloadFolder
        || downloadTask.DownloadFilePath.RemoveReapTempSuffix() == downloadTask.DestinationFilePath
        || string.IsNullOrWhiteSpace(downloadTask.DestinationFilePath);
}
