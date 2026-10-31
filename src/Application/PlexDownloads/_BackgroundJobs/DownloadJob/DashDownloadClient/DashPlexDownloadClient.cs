using System.Globalization;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Reaparr.Application;

/// <summary>
/// Download client implementation that uses dash-mpd-cli for downloading DASH (MPEG-DASH) streaming content.
/// Unlike <see cref="DirectPlexDownloadClient"/>, this implementation delegates the entire download process
/// to the dash-mpd-cli binary, which handles multi-threading, segment downloading, and muxing internally.
/// </summary>
public class DashPlexDownloadClient : IPlexDownloadClient
{
    private readonly IReaparrDbContext _dbContext;
    private readonly IDashMpdCliWrapper _dashWrapper;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IDownloadTaskUpdateDispatcher _downloadTaskUpdateDispatcher;
    private readonly IDownloadSpeedLimitProvider _speedLimits;
    private readonly INotificationHubService _notificationHubService;
    private readonly IFileInfoFactory _fileInfoFactory;

    private DownloadTaskKey? _downloadTaskKey;
    private string? _dashOutputFilePath;
    private readonly CompositeDisposable _subscriptions = new();
    private readonly Subject<Unit> _destroy = new();
    private TaskCompletionSource<Result>? _terminalWorkCompletion;
    private int _completionObserved;
    private int _stopRequested;
    private bool _disposed;
    private DownloadTaskProgress _lastProgressUpdate = new();

    public DashPlexDownloadClient(
        IReaparrDbContextFactory dbContextFactory,
        IDashMpdCliWrapper dashWrapper,
        ICommandExecutor commandExecutor,
        IDownloadTaskUpdateDispatcher downloadTaskUpdateDispatcher,
        IDownloadSpeedLimitProvider speedLimits,
        INotificationHubService notificationHubService,
        IFileInfoFactory fileInfoFactory
    )
    {
        _dashWrapper = dashWrapper;
        _commandExecutor = commandExecutor;
        _downloadTaskUpdateDispatcher = downloadTaskUpdateDispatcher;
        _speedLimits = speedLimits;
        _notificationHubService = notificationHubService;
        _fileInfoFactory = fileInfoFactory;
        _dbContext = dbContextFactory.Create();
    }

    public async Task<Result> Start(DownloadTaskKey downloadTaskKey, CancellationToken cancellationToken = default)
    {
        _downloadTaskKey = downloadTaskKey;

        var downloadTask = await _dbContext.GetDownloadTaskFileAsync(downloadTaskKey, cancellationToken);
        if (downloadTask is null)
        {
            return ResultExtensions
                .EntityNotFound(nameof(DownloadTaskGeneric), downloadTaskKey.ToString())
                .LogWarning();
        }

        // Get transcoding download url
        var downloadUrlResult = await _commandExecutor.Send(
            new GetTranscodeUrlCommand
            {
                DownloadTaskKey = downloadTaskKey,
                MetaDataPath = $"/library/metadata/{downloadTask.PlexApiRatingKey}",
            },
            cancellationToken
        );

        if (downloadUrlResult.IsCancelled)
        {
            var stoppedResult = await SetDownloadStatusAsync(DownloadStatus.Stopped, downloadUrlResult.ToResult());
            if (stoppedResult.IsFailed)
                return stoppedResult.LogIfFailed();

            return downloadUrlResult.ToResult();
        }

        if (downloadUrlResult.IsFailed)
        {
            var status = downloadUrlResult.ToResult().IsServerUnreachable()
                ? DownloadStatus.ServerUnreachable
                : DownloadStatus.SourceUnavailable;
            var statusResult = await SetDownloadStatusAsync(status, downloadUrlResult.ToResult());
            if (statusResult.IsFailed)
                return statusResult.LogIfFailed();

            return downloadUrlResult.LogError();
        }

        // Ensure the download directory exists and has enough disk space
        var ensureDirectoryResult = await _commandExecutor.Send(
            new EnsureDownloadDirectoryCommand(downloadTask.DownloadDirectory, downloadTask.DataTotal),
            cancellationToken
        );
        if (ensureDirectoryResult.IsCancelled)
            return ensureDirectoryResult;

        if (ensureDirectoryResult.IsFailed)
        {
            var storageErrorResult = await SetDownloadStatusAsync(DownloadStatus.StorageError, ensureDirectoryResult);
            if (storageErrorResult.IsFailed)
                return storageErrorResult.LogIfFailed();

            return ensureDirectoryResult;
        }

        var outputQuality = downloadUrlResult.Value.TranscodedQuality;
        var normalizedFileName = DashOutputFileNameCleaner.NormalizeForDashOutput(downloadTask.FileName, outputQuality);
        if (!string.Equals(downloadTask.FileName, normalizedFileName, StringComparison.Ordinal))
        {
            await PersistDashOutputFileName(downloadTask, normalizedFileName, cancellationToken);
            downloadTask.FileName = normalizedFileName;

            // Ensure the new filename is propagated to the front-end
            await _notificationHubService.SendRefreshNotificationAsync(RefreshDataType.DownloadTasks);
        }

        SetupDownloadListeners(downloadTaskKey);

        // Execute dash stream download
        var downloadingResult = await SetDownloadStatusAsync(DownloadStatus.Downloading);
        if (downloadingResult.IsFailed)
            return downloadingResult.LogIfFailed();

        var options = await CreateDashOptions(downloadTask, downloadUrlResult.Value.DownloadUrl);
        _dashOutputFilePath = options.Output;
        var dashStartTask = _dashWrapper.StartAsync(options);
        try
        {
            var startResult = await dashStartTask.WaitAsync(cancellationToken);
            var terminalResult = await AwaitTerminalWorkAsync();
            if (terminalResult.IsFailed)
            {
                terminalResult.LogError();
                if (startResult.IsSuccess)
                    return terminalResult;
            }

            return startResult;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var stopResult = await StopRunnerAsync();
            if (stopResult.IsCancelled)
                return stopResult;

            if (stopResult.IsFailed)
                return stopResult.LogError();

            return ResultExtensions.TaskIsCancelled(nameof(DashPlexDownloadClient));
        }
    }

    public async Task<Result> StopAsync()
    {
        var stopResult = await StopRunnerAsync();
        if (stopResult.IsCancelled)
            return stopResult;

        if (stopResult.IsFailed)
            return stopResult.LogIfFailed();

        if (_downloadTaskKey is null)
            return Result.Ok();

        var stoppedResult = await SetDownloadStatusAsync(DownloadStatus.Stopped);
        if (stoppedResult.IsFailed)
            return stoppedResult.LogIfFailed();

        return Result.Ok();
    }

    private async Task<Result> StopRunnerAsync()
    {
        Interlocked.Exchange(ref _stopRequested, 1);

        var stopResult = await _dashWrapper.StopAsync();
        if (stopResult.IsFailed && !stopResult.IsCancelled)
            return stopResult;

        return await AwaitTerminalWorkAsync();
    }

    private async Task<Result> AwaitTerminalWorkAsync()
    {
        var terminalWorkCompletion = _terminalWorkCompletion;
        if (Volatile.Read(ref _completionObserved) == 0 || terminalWorkCompletion is null)
            return Result.Ok();

        return await Result.Try(async Task<Result> () => await terminalWorkCompletion.Task);
    }

    private async Task<DashMpdCliOptions> CreateDashOptions(DownloadTaskFileBase downloadTask, string downloadUrl)
    {
        var serverMachineIdentifier = await _dbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);
        var speedLimitBytes = _speedLimits.GetEffectiveDownloadSpeedLimit(serverMachineIdentifier);

        return new DashMpdCliOptions
        {
            MpdUrl = downloadUrl,
            Output = downloadTask.DownloadFilePath.RemoveReapTempSuffix(),
            WorkingDirectory = downloadTask.DownloadDirectory,
            Quiet = false,
            Quality = "best",
            LimitRate = speedLimitBytes > 0 ? speedLimitBytes.ToString(CultureInfo.InvariantCulture) : null,
            EnvironmentVariables = new Dictionary<string, string>
            {
                ["TMPDIR"] = downloadTask.DownloadDirectory,
                ["TMP"] = downloadTask.DownloadDirectory,
            },
        };
    }

    private async Task PersistDashOutputFileName(
        DownloadTaskFileBase downloadTask,
        string normalizedFileName,
        CancellationToken cancellationToken
    )
    {
        switch (downloadTask.DownloadTaskType)
        {
            case DownloadTaskType.MovieData:
                await _dbContext
                    .DownloadTaskMovieFile.Where(x => x.Id == downloadTask.Id)
                    .ExecuteUpdateAsync(
                        patch =>
                            patch
                                .SetProperty(x => x.Title, normalizedFileName)
                                .SetProperty(x => x.FileName, normalizedFileName),
                        cancellationToken
                    );
                break;
            case DownloadTaskType.EpisodeData:
                await _dbContext
                    .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == downloadTask.Id)
                    .ExecuteUpdateAsync(
                        patch =>
                            patch
                                .SetProperty(x => x.Title, normalizedFileName)
                                .SetProperty(x => x.FileName, normalizedFileName),
                        cancellationToken
                    );
                break;
            case DownloadTaskType.MusicTrackData:
                await _dbContext
                    .DownloadTaskMusicTrackFiles.Where(x => x.Id == downloadTask.Id)
                    .ExecuteUpdateAsync(
                        patch =>
                            patch
                                .SetProperty(x => x.Title, normalizedFileName)
                                .SetProperty(x => x.FileName, normalizedFileName),
                        cancellationToken
                    );
                break;
            case DownloadTaskType.PhotoImage:
                await _dbContext
                    .DownloadTaskPhotoImageFiles.Where(x => x.Id == downloadTask.Id)
                    .ExecuteUpdateAsync(
                        patch =>
                            patch
                                .SetProperty(x => x.Title, normalizedFileName)
                                .SetProperty(x => x.FileName, normalizedFileName),
                        cancellationToken
                    );
                break;
            case DownloadTaskType.OtherVideoData:
                await _dbContext
                    .DownloadTaskOtherVideoFiles.Where(x => x.Id == downloadTask.Id)
                    .ExecuteUpdateAsync(
                        patch =>
                            patch
                                .SetProperty(x => x.Title, normalizedFileName)
                                .SetProperty(x => x.FileName, normalizedFileName),
                        cancellationToken
                    );
                break;
        }
    }

    private void SetupDownloadListeners(DownloadTaskKey key)
    {
        var terminalWorkCompletion = new TaskCompletionSource<Result>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        _terminalWorkCompletion = terminalWorkCompletion;
        Volatile.Write(ref _completionObserved, 0);
        Volatile.Write(ref _stopRequested, 0);

        _subscriptions.Add(
            _dashWrapper
                .Progress.TakeUntil(_destroy)
                .Subscribe(progress =>
                {
                    _lastProgressUpdate = new DownloadTaskProgress
                    {
                        DataTotal = progress.TotalBytes,
                        Percentage = Convert.ToDecimal(progress.Percent),
                        DataReceived = progress.DownloadedBytes,
                        DownloadSpeed = progress.DownloadSpeedInBytes,
                        TimeRemaining = progress.Eta,
                    };

                    _downloadTaskUpdateDispatcher.OnProgressUpdated(key, _lastProgressUpdate);
                })
        );

        _subscriptions.Add(
            _dashWrapper
                .DownloadCompleted.Take(1)
                .TakeUntil(_destroy)
                .Do(_ => Volatile.Write(ref _completionObserved, 1))
                .Select(completed => Observable.FromAsync(async _ => await HandleDownloadCompleted(key, completed)))
                .Concat()
                .Do(terminalResult => terminalWorkCompletion.TrySetResult(terminalResult))
                .Subscribe(
                    _ => { },
                    exception => terminalWorkCompletion.TrySetException(exception),
                    () => terminalWorkCompletion.TrySetResult(Result.Ok())
                )
        );
    }

    private async Task<Result> HandleDownloadCompleted(DownloadTaskKey key, DashDownloadCompletedEventArgs completed)
    {
        if (Volatile.Read(ref _stopRequested) != 0 || Volatile.Read(ref _disposed))
            return Result.Ok();

        if (completed.Cancelled)
        {
            var stoppedResult = await SetDownloadStatusAsync(DownloadStatus.Stopped, completed.Result);
            if (stoppedResult.IsFailed)
                return stoppedResult.LogError();

            return completed.Result;
        }

        if (!completed.IsSuccess)
        {
            var status =
                completed.Result.Has404NotFoundError() ? DownloadStatus.SourceUnavailable
                : completed.Result.IsServerUnreachable() ? DownloadStatus.ServerUnreachable
                : completed.Result.HasStorageError() ? DownloadStatus.StorageError
                : DownloadStatus.DownloadClientError;
            var statusResult = await SetDownloadStatusAsync(status, completed.Result);
            if (statusResult.IsFailed)
                return statusResult.LogError();

            return completed.Result;
        }

        var outputFileLengthResult = GetDashOutputFileLength();
        if (outputFileLengthResult.IsFailed)
        {
            var outputFailureResult = await SetDownloadStatusAsync(
                DownloadStatus.DownloadClientError,
                outputFileLengthResult.ToResult()
            );
            if (outputFailureResult.IsFailed)
                return outputFailureResult.LogError();

            return outputFileLengthResult.ToResult();
        }

        // dash-mpd-cli reports an estimated total. The completed file is authoritative.
        _lastProgressUpdate = new DownloadTaskProgress
        {
            DataTotal = outputFileLengthResult.Value,
            DataReceived = outputFileLengthResult.Value,
            Percentage = 100m,
            DownloadSpeed = 0,
            TimeRemaining = 0,
        };
        var finalProgressResult = await _downloadTaskUpdateDispatcher.PersistProgressAsync(
            key,
            _lastProgressUpdate,
            cancellationToken: CancellationToken.None
        );
        if (finalProgressResult.IsCancelledOrFailed)
        {
            var progressFailureResult = await SetDownloadStatusAsync(
                DownloadStatus.DownloadClientError,
                finalProgressResult
            );
            if (progressFailureResult.IsFailed)
                return progressFailureResult.LogError();

            return finalProgressResult;
        }

        var finishedResult = await SetDownloadStatusAsync(DownloadStatus.DownloadFinished);
        if (finishedResult.IsFailed)
            return finishedResult.LogError();

        return Result.Ok();
    }

    private Result<long> GetDashOutputFileLength()
    {
        if (string.IsNullOrWhiteSpace(_dashOutputFilePath))
            return Result.Fail<long>("DASH output path was not configured.");

        var outputPath = _dashOutputFilePath!;
        return Result.Try(() =>
        {
            var fileInfo = _fileInfoFactory.New(outputPath);
            if (!fileInfo.Exists)
                throw new FileNotFoundException("DASH output file does not exist.", outputPath);

            return fileInfo.Length;
        });
    }

    private async Task<Result> SetDownloadStatusAsync(DownloadStatus status, Result? errorResult = null)
    {
        if (_downloadTaskKey is null || Volatile.Read(ref _disposed))
            return Result.Ok();

        if (errorResult is null)
        {
            await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(_downloadTaskKey, status, CancellationToken.None);
            return Result.Ok();
        }

        await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
            _downloadTaskKey,
            status,
            errorResult,
            CancellationToken.None
        );

        return Result.Ok();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        Volatile.Write(ref _disposed, true);
        Interlocked.Exchange(ref _stopRequested, 1);
        try
        {
            await _dashWrapper.DisposeAsync();
            var terminalResult = await AwaitTerminalWorkAsync();
            terminalResult.LogIfFailed();
        }
        finally
        {
            _destroy.OnNext(Unit.Default);
            _destroy.OnCompleted();
            _subscriptions.Dispose();
            _destroy.Dispose();
            _dbContext.Dispose();
        }
    }
}
