using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Downloader;
using Polly;
using Polly.Retry;

namespace Reaparr.Application;

public class DirectPlexDownloadClient : IPlexDownloadClient
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly IReaparrDbContextFactory _dbContextFactory;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IDownloadTaskUpdateDispatcher _downloadTaskUpdateDispatcher;
    private readonly IDownloadSpeedLimitProvider _speedLimits;
    private readonly IFile _file;
    private readonly IFileInfoFactory _fileInfoFactory;

    private DownloadTaskKey? _downloadTaskKey;
    private string _filename = string.Empty;

    private readonly IDownloadService _downloader;

    private readonly DownloadConfiguration _configuration = new();

    private readonly CompositeDisposable _subscriptions = new();
    private readonly Subject<Unit> _destroy = new();
    private readonly object _disposeLock = new();
    private bool _isDisposed;
    private const int MAX_TRANSIENT_DOWNLOAD_RETRIES = 2;

    private int _lastObservedProgressPercentagePercent;

    public DirectPlexDownloadClient(
        ILogger log,
        IReaparrDbContextFactory dbContextFactory,
        ICommandExecutor commandExecutor,
        IDownloadTaskUpdateDispatcher downloadTaskUpdateDispatcher,
        IDownloadManagerSettings downloadManagerSettings,
        IDownloadSpeedLimitProvider speedLimits,
        IFile file,
        IFileInfoFactory fileInfoFactory,
        Func<DownloadConfiguration, IDownloadService> downloadServiceFactory
    )
    {
        _log = log.ForContext<DirectPlexDownloadClient>();
        _dbContextFactory = dbContextFactory;
        _commandExecutor = commandExecutor;
        _downloadTaskUpdateDispatcher = downloadTaskUpdateDispatcher;
        _dbContext = dbContextFactory.Create();
        _speedLimits = speedLimits;
        _file = file;
        _fileInfoFactory = fileInfoFactory;

        var downloadSegments = Math.Max(1, downloadManagerSettings.DownloadSegments);

        // Number of file parts, default is 1
        _configuration.ChunkCount = downloadSegments;
        _configuration.ParallelCount = downloadSegments;
        _configuration.ParallelDownload = downloadSegments > 1;
        _configuration.MaxTryAgainOnFailure = 3;
        _configuration.HttpClientTimeout = (int)TimeSpan.FromSeconds(100).TotalMilliseconds;
        _configuration.EnableAutoResumeDownload = false;
        _configuration.DownloadFileExtension = FilePathExtensions.TempDownloadFileSuffix;
        _configuration.CheckDiskSizeBeforeDownload = false;
        _configuration.MaximumMemoryBufferBytes = 50 * 1024 * 1024; // 50MB memory buffer cap

        _downloader = downloadServiceFactory(_configuration);
    }

    /// <inheritdoc/>
    public async Task<Result> Start(DownloadTaskKey downloadTaskKey, CancellationToken cancellationToken = default)
    {
        var startupStopwatch = Stopwatch.StartNew();
        _downloadTaskKey = downloadTaskKey;
        var downloadTask = await _dbContext.GetDownloadTaskFileAsync(downloadTaskKey, cancellationToken);
        if (downloadTask is null)
        {
            return ResultExtensions
                .EntityNotFound(nameof(DownloadTaskGeneric), downloadTaskKey.ToString())
                .LogWarning();
        }

        _filename = downloadTask.FileName;

        var directUrlStopwatch = Stopwatch.StartNew();
        var downloadUrlResult = await _commandExecutor.Send(
            new GetDirectDownloadUrlCommand(downloadTask.PlexServerId, downloadTask.FileLocationUrl),
            cancellationToken
        );
        _log.Here()
            .Debug(
                "Resolved direct download URL for {MediaFileName} in {ElapsedMilliseconds} ms",
                _filename,
                directUrlStopwatch.ElapsedMilliseconds
            );

        if (downloadUrlResult.IsCancelled)
            return downloadUrlResult.ToResult();

        if (downloadUrlResult.IsFailed)
        {
            var failedResult = downloadUrlResult.ToResult();
            var failureStatus = GetFailureStatus(failedResult);

            await SendDownloadClientLog(NotificationLevel.Error, failureStatus, failedResult.ToString());

            await SetDownloadStatusAsync(failureStatus);
            return failedResult;
        }

        var downloadUrl = downloadUrlResult.Value;

        // Ensure the download directory exists and has enough disk space
        var ensureDirectoryStopwatch = Stopwatch.StartNew();
        var ensureDirectoryResult = await _commandExecutor.Send(
            new EnsureDownloadDirectoryCommand(downloadTask.DownloadDirectory, downloadTask.DataTotal),
            cancellationToken
        );
        _log.Here()
            .Debug(
                "Ensured download directory for {MediaFileName} in {ElapsedMilliseconds} ms",
                _filename,
                ensureDirectoryStopwatch.ElapsedMilliseconds
            );

        if (ensureDirectoryResult.IsCancelled)
            return ensureDirectoryResult;

        if (ensureDirectoryResult.IsFailed)
        {
            await SetDownloadStatusAsync(Domain.DownloadStatus.StorageError, ensureDirectoryResult);
            return ensureDirectoryResult;
        }

        await SetupDownloadListeners(downloadTaskKey);
        await SetDownloadStatusAsync(Domain.DownloadStatus.Downloading);

        var downloaderStartStopwatch = Stopwatch.StartNew();

        var retryAttempt = 0;
        DirectDownloadSnapshot? latestCallbackSnapshot = null;
        var retryPipeline = new ResiliencePipelineBuilder<Result>()
            .AddRetry(
                new RetryStrategyOptions<Result>
                {
                    ShouldHandle = new PredicateBuilder<Result>().HandleResult(result =>
                        result.HasException<TaskCanceledException>() && !cancellationToken.IsCancellationRequested
                    ),
                    MaxRetryAttempts = MAX_TRANSIENT_DOWNLOAD_RETRIES,
                    Delay = TimeSpan.FromSeconds(3),
                    BackoffType = DelayBackoffType.Constant,
                    UseJitter = false,
                    OnRetry = args =>
                    {
                        _log.Here()
                            .Warning(
                                "Direct download transport cancellation for {MediaFileName}; retrying attempt {RetryAttempt} of {RetryCount} in {RetryDelay} after Downloader exhausted its retries",
                                _filename,
                                args.AttemptNumber + 1,
                                MAX_TRANSIENT_DOWNLOAD_RETRIES,
                                args.RetryDelay
                            );
                        return default;
                    },
                }
            )
            .Build();

        var result = await Result.Try(async Task<Result> () =>
            await retryPipeline.ExecuteAsync(
                async token =>
                {
                    var attempt = retryAttempt++;
                    if (attempt > 0)
                        await _downloader.Clear();

                    var completionSource = new TaskCompletionSource<AsyncCompletedEventArgs>(
                        TaskCreationOptions.RunContinuationsAsynchronously
                    );
                    void OnDownloadCompleted(object? _, AsyncCompletedEventArgs args) =>
                        completionSource.TrySetResult(args);
                    _downloader.DownloadFileCompleted += OnDownloadCompleted;

                    try
                    {
                        var attemptDownloadUrl = downloadUrl;
                        if (attempt > 0)
                        {
                            var attemptUrlResult = await _commandExecutor.Send(
                                new GetDirectDownloadUrlCommand(
                                    downloadTask.PlexServerId,
                                    downloadTask.FileLocationUrl
                                ),
                                token
                            );
                            if (!attemptUrlResult.IsSuccess)
                                return attemptUrlResult.ToResult();

                            attemptDownloadUrl = attemptUrlResult.Value;
                        }
                        using var retryDbContext = await _dbContextFactory.CreateAsync();
                        var latestDownloadTask = await retryDbContext.GetDownloadTaskFileAsync(downloadTaskKey, token);
                        var persistedSnapshot = latestDownloadTask?.DirectDownloadSnapshot;
                        var snapshot =
                            latestCallbackSnapshot is not null
                            && (
                                persistedSnapshot is null
                                || latestCallbackSnapshot.SaveProgress >= persistedSnapshot.SaveProgress
                            )
                                ? latestCallbackSnapshot
                                : persistedSnapshot;
                        _log.Here()
                            .Debug(
                                "Starting direct download attempt {DownloadAttempt} for {DownloadTaskId} ({MediaFileName}) from {SnapshotProgress}% with {SnapshotChunkCount} chunks",
                                attempt + 1,
                                downloadTaskKey.Id,
                                _filename,
                                snapshot?.SaveProgress ?? 0,
                                snapshot?.Chunks.Count ?? 0
                            );
                        if (snapshot is { Chunks.Count: > 0 })
                        {
                            await SendDownloadClientLog(
                                NotificationLevel.Information,
                                Domain.DownloadStatus.Downloading,
                                $"Resuming {_filename} download from pause"
                            );
                            var progress = snapshot.ToDownloadPackage();
                            progress.Urls = [attemptDownloadUrl]; // Ensure we use the latest connection string
                            await _downloader.DownloadFileTaskAsync(progress, token);
                        }
                        else
                        {
                            if (snapshot is not null)
                            {
                                _log.Here()
                                    .Warning(
                                        "Ignoring invalid resume snapshot without chunks for {MediaFileName}; starting a fresh download",
                                        _filename
                                    );
                            }

                            var downloaderTargetPath = Path.Combine(
                                downloadTask.DownloadDirectory,
                                downloadTask.FileName
                            );
                            await _downloader.DownloadFileTaskAsync(attemptDownloadUrl, downloaderTargetPath, token);
                        }

                        if (completionSource.Task.IsCompleted)
                        {
                            var completionArgs = completionSource.Task.Result;
                            latestCallbackSnapshot = (completionArgs.UserState as DownloadPackage)?.ToSnapshot();
                            return await ProcessDownloadCompletedAsync(
                                completionArgs,
                                downloadTaskKey,
                                downloadTask.DownloadFilePath,
                                downloadTask.DataTotal
                            );
                        }
                        else
                        {
                            var reconciliationResult = await ReconcileMissingCompletionCallbackAsync(
                                downloadTaskKey,
                                downloadTask.DownloadFilePath,
                                downloadTask.DataTotal
                            );
                            return reconciliationResult;
                        }
                    }
                    finally
                    {
                        _downloader.DownloadFileCompleted -= OnDownloadCompleted;
                    }
                },
                cancellationToken
            )
        );

        var transportCancellation = result
            .Errors.OfType<ExceptionalError>()
            .FirstOrDefault(error => error.Exception is TaskCanceledException);
        if (transportCancellation is not null && !cancellationToken.IsCancellationRequested)
        {
            result = Result
                .Fail(
                    new ExceptionalError(
                        new IOException(
                            "Direct download transport connection was cancelled.",
                            transportCancellation.Exception
                        )
                    )
                )
                .Add503ServiceUnavailableError();

            await SetDownloadStatusAsync(Domain.DownloadStatus.ServerUnreachable, result);
        }

        if (!result.IsSuccess)
            return result.IsCancelled ? result : result.LogError();

        _log.Here()
            .Debug(
                "Direct downloader start workflow for {MediaFileName} completed in {DownloaderElapsedMilliseconds} ms ({TotalElapsedMilliseconds} ms total startup)",
                _filename,
                downloaderStartStopwatch.ElapsedMilliseconds,
                startupStopwatch.ElapsedMilliseconds
            );

        return Result.Ok();
    }

    /// <inheritdoc/>
    public async Task<Result> StopAsync()
    {
        if (_downloadTaskKey is null)
            return Result.Ok();

        _log.Here()
            .Debug(
                "DownloadTask {DownloadTaskId} ({MediaFileName}) has been requested to stop",
                _downloadTaskKey.Id,
                _filename
            );

        await _downloader.CancelTaskAsync();

        var stopMsg = _log.Here()
            .InformationMsg(
                "DownloadTask {DownloadTaskId} ({MediaFileName}) was stopped",
                _downloadTaskKey.Id,
                _filename
            );

        await SendDownloadClientLog(NotificationLevel.Information, Domain.DownloadStatus.Stopped, stopMsg);
        return Result.Ok();
    }

    private async Task SetupDownloadListeners(DownloadTaskKey key)
    {
        // Setup DownloadLimit Subscription
        var serverMachineIdentifier = await _dbContext.GetPlexServerMachineIdentifierById(key.PlexServerId);
        _subscriptions.Add(
            _speedLimits
                .GetEffectiveDownloadSpeedLimitObservable(serverMachineIdentifier)
                .TakeUntil(_destroy)
                .Subscribe(value =>
                {
                    _configuration.MaximumBytesPerSecond = value;
                })
        );

        // Setup DownloadProgressChanged Subscription
        _subscriptions.Add(
            Observable
                .FromEventPattern<DownloadProgressChangedEventArgs>(
                    h => _downloader.DownloadProgressChanged += h,
                    h => _downloader.DownloadProgressChanged -= h
                )
                .Select(x => x.EventArgs)
                .Sample(TimeSpan.FromMilliseconds(300))
                .Subscribe(args =>
                {
                    var progress = new DownloadTaskProgress
                    {
                        DataTotal = args.TotalBytesToReceive,
                        Percentage = Convert.ToDecimal(args.ProgressPercentage),
                        DataReceived = args.ReceivedBytesSize,
                        DownloadSpeed = args.ProgressPercentage < 100 ? Convert.ToInt64(args.BytesPerSecondSpeed) : 0,
                        TimeRemaining = DataFormat.GetTimeRemaining(
                            args.TotalBytesToReceive - args.ReceivedBytesSize,
                            args.BytesPerSecondSpeed
                        ),
                    };

                    var progressPercentagePercent = Math.Clamp((int)Math.Round(progress.Percentage), 0, 100);
                    Volatile.Write(ref _lastObservedProgressPercentagePercent, progressPercentagePercent);
                    _downloadTaskUpdateDispatcher.OnProgressUpdated(key, progress, _downloader.Package.ToSnapshot());
                })
        );
    }

    private async Task<Result> ReconcileMissingCompletionCallbackAsync(
        DownloadTaskKey key,
        string downloadFilePath,
        long expectedFileSize
    )
    {
        var package = _downloader.Package;

        var completionResult = VerifyCompletedDownload(package, downloadFilePath, expectedFileSize);
        if (completionResult.IsFailed)
        {
            var failure = completionResult.ToResult();

            _log.Here()
                .Error(
                    "Download finished execution without completion callback and verification failed for {MediaFileName}",
                    _filename
                );

            await SetDownloadStatusAsync(Domain.DownloadStatus.Error, failure);
            return failure;
        }

        var verifiedFileSize = completionResult.Value;

        _log.Here()
            .Warning(
                "Download completion callback was not received for {MediaFileName}; applying verified completion reconciliation",
                _filename
            );

        var progress = new DownloadTaskProgress
        {
            DataTotal = verifiedFileSize,
            Percentage = 100,
            DataReceived = verifiedFileSize,
            DownloadSpeed = 0,
            TimeRemaining = 0,
        };

        _downloadTaskUpdateDispatcher.OnProgressUpdated(key, progress, package?.ToSnapshot());

        await SetDownloadStatusAsync(Domain.DownloadStatus.DownloadFinished);
        return Result.Ok();
    }

    private async Task<Result> ProcessDownloadCompletedAsync(
        AsyncCompletedEventArgs args,
        DownloadTaskKey key,
        string downloadFilePath,
        long expectedFileSize
    )
    {
        var package = args.UserState as DownloadPackage;
        _log.Here().Debug("The UserState at time of completion: {@Package}", package);
        if (args.Cancelled)
        {
            await SetDownloadStatusAsync(Domain.DownloadStatus.Paused);
            return Result.Ok();
        }

        if (args.Error is not null)
        {
            var errorResult = Result.Fail(new ExceptionalError(args.Error));
            if (args.Error is TaskCanceledException)
                return errorResult;

            errorResult.LogError();
            await SetDownloadStatusAsync(GetFailureStatus(errorResult), errorResult);
            return errorResult;
        }

        var completionResult = VerifyCompletedDownload(package, downloadFilePath, expectedFileSize);
        if (completionResult.IsFailed)
        {
            var failure = completionResult.ToResult();
            await SetDownloadStatusAsync(Domain.DownloadStatus.Error, failure);
            return failure;
        }

        var verifiedFileSize = completionResult.Value;
        _downloadTaskUpdateDispatcher.OnProgressUpdated(
            key,
            new DownloadTaskProgress
            {
                DataTotal = verifiedFileSize,
                Percentage = 100,
                DataReceived = verifiedFileSize,
                DownloadSpeed = 0,
                TimeRemaining = 0,
            },
            package!.ToSnapshot()
        );
        await SetDownloadStatusAsync(Domain.DownloadStatus.DownloadFinished);
        return Result.Ok();
    }

    private static Domain.DownloadStatus GetFailureStatus(Result result) =>
        result.Has404NotFoundError() ? Domain.DownloadStatus.SourceUnavailable
        : result.IsServerUnreachable() ? Domain.DownloadStatus.ServerUnreachable
        : result.HasStorageError() ? Domain.DownloadStatus.StorageError
        : Domain.DownloadStatus.Error;

    private Result<long> VerifyCompletedDownload(
        DownloadPackage? package,
        string downloadFilePath,
        long expectedFileSize
    )
    {
        if (package is null)
        {
            return Result
                .Fail<long>(
                    $"Download completion for {_filename} did not include a download package; refusing to mark as complete."
                )
                .LogError();
        }

        var completedFilePath = downloadFilePath.RemoveReapTempSuffix();
        var existingPath = _file.Exists(completedFilePath) ? completedFilePath : downloadFilePath;

        if (!_file.Exists(existingPath))
        {
            return Result
                .Fail<long>(
                    $"Download completion for {_filename} could not be verified because no completed file exists at '{completedFilePath}' or '{downloadFilePath}'. Expected {expectedFileSize} bytes. Package reported {package.ReceivedBytesSize} received bytes, {package.TotalFileSize} total bytes and {package.SaveProgress:F2}% save progress."
                )
                .LogError();
        }

        var actualFileSize = _fileInfoFactory.New(existingPath).Length;
        if (actualFileSize < expectedFileSize)
        {
            return Result
                .Fail<long>(
                    $"Download completion for {_filename} was rejected because the file is incomplete. Expected {expectedFileSize} bytes but found {actualFileSize} bytes at '{existingPath}'. Package reported {package.ReceivedBytesSize} received bytes, {package.TotalFileSize} total bytes and {package.SaveProgress:F2}% save progress."
                )
                .LogError();
        }

        return Result.Ok(actualFileSize);
    }

    private async Task SetDownloadStatusAsync(Domain.DownloadStatus status, Result? errorResult = null)
    {
        if (_downloadTaskKey is null)
        {
            _log.Here()
                .Error(
                    "Attempted to set download status to {Status} for a null DownloadTaskKey. This is likely a programming error",
                    status
                );
            return;
        }

        if (errorResult is null)
            await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(_downloadTaskKey, status, CancellationToken.None);
        else
            await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
                _downloadTaskKey,
                status,
                errorResult,
                CancellationToken.None
            );
    }

    private async Task SendDownloadClientLog(NotificationLevel logLevel, Domain.DownloadStatus status, string message)
    {
        using var dbContext = await _dbContextFactory.CreateAsync();
        await dbContext.CreateDownloadClientLog(_downloadTaskKey!, logLevel, status, message);
    }

    public async ValueTask DisposeAsync()
    {
        lock (_disposeLock)
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
        }

        // signals completion to all streams
        _destroy.OnNext(Unit.Default);
        _destroy.OnCompleted();
        _subscriptions.Dispose();
        _destroy.Dispose();

        if (_downloader is IAsyncDisposable asyncDisposableDownloader)
        {
            await asyncDisposableDownloader.DisposeAsync();
        }
        else if (_downloader is IDisposable disposableDownloader)
        {
            disposableDownloader.Dispose();
        }

        _dbContext.Dispose();
    }
}
