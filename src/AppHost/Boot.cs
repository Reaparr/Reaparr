using Quartz;
using Reaparr.Application.Contracts;

namespace Reaparr.AppHost;

/// <summary>
/// The Boot class is used to sequentially start various processes needed to start Reaparr.
/// </summary>
public class Boot : IHostedService
{
    #region Fields

    private readonly Serilog.ILogger _log;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IAppRuntimeInfo _appRuntimeInfo;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly IScheduler _scheduler;
    private readonly IBackgroundJobsSetup _backgroundJobsSetup;
    private readonly IDownloadQueue _downloadQueue;

    #endregion

    #region Constructor

    /// <summary>
    /// The Boot class is used to sequentially start various processes needed to start Reaparr.
    /// </summary>
    public Boot(
        Serilog.ILogger log,
        ICommandExecutor commandExecutor,
        IAppRuntimeInfo appRuntimeInfo,
        IHostApplicationLifetime appLifetime,
        IScheduler scheduler,
        IBackgroundJobsSetup backgroundJobsSetup,
        IDownloadQueue downloadQueue
    )
    {
        _log = log.ForContext<Boot>();
        _commandExecutor = commandExecutor;
        _appRuntimeInfo = appRuntimeInfo;
        _appLifetime = appLifetime;
        _scheduler = scheduler;
        _backgroundJobsSetup = backgroundJobsSetup;
        _downloadQueue = downloadQueue;

        appLifetime.ApplicationStarted.Register(OnStarted);
        appLifetime.ApplicationStopping.Register(OnStopping);
        appLifetime.ApplicationStopped.Register(OnStopped);
    }

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_appRuntimeInfo is { PUID: 911, PGID: 1001 })
        {
            _log.Here()
                .Error("Reaparr has invalid PUID and PGID values and thus has defaulted to root, this is not allowed");
            TerminateApplication();
            return;
        }

        var defaultUser = await _commandExecutor.Send(new CreateDefaultAppUserCommand(), cancellationToken);
        defaultUser.LogIfFailed();
        if (defaultUser.IsFailed)
        {
            TerminateApplication();
            return;
        }

        var downloadQueueSetup = _downloadQueue.Setup(_appLifetime.ApplicationStopping);
        downloadQueueSetup.LogIfFailed();
        if (downloadQueueSetup.IsFailed)
        {
            TerminateApplication();
            return;
        }

        var recoverResult = await _commandExecutor.Send(new RecoverInterruptedDownloadsCommand(), cancellationToken);
        recoverResult.LogIfFailed();

        var setupResult = await _backgroundJobsSetup.SetupAsync(cancellationToken);
        setupResult.LogIfFailed();
        if (setupResult.IsFailed)
        {
            TerminateApplication();
            return;
        }

        if (!_appRuntimeInfo.IsIntegrationTestMode)
        {
            var bootQueueKickResult = await _downloadQueue.CheckDownloadQueueForAllServers(cancellationToken);
            bootQueueKickResult.LogIfFailed();
        }

        _log.Here().Information("Finished Initiating boot process");
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _log.Here().Information("Shutting down the container");

        await _scheduler.Standby(cancellationToken);
        var activeMoveJobs = (await _scheduler.GetCurrentlyExecutingJobs(cancellationToken))
            .Where(x => x.JobDetail.Key.Group == nameof(JobTypes.MoveDownloadFileJob))
            .ToList();
        foreach (var context in activeMoveJobs)
            await _scheduler.Interrupt(context.FireInstanceId, cancellationToken);

        var moveJobsResult = await _scheduler.WaitForJobsToFinish(
            activeMoveJobs.Select(x => x.JobDetail.Key),
            TimeSpan.FromSeconds(30),
            cancellationToken
        );
        if (moveJobsResult.IsFailed)
            moveJobsResult.LogError();

        var autoPauseResult = await _commandExecutor.Send(new AutoPauseActiveDownloadsCommand(), cancellationToken);
        if (autoPauseResult.IsFailed)
            autoPauseResult.LogError();

        var stopResult = await _backgroundJobsSetup.StopAsync(cancellationToken);
        stopResult.LogIfFailed();
    }

    #endregion

    #region Private Methods

    private void TerminateApplication()
    {
        _log.Here().Fatal("An error occurred during the boot process, terminating application");
        _appLifetime.StopApplication();
    }

    // ReSharper disable once AsyncVoidMethod
    private async void OnStarted()
    {
        _log.Here().Debug("Boot.OnStarted has been called");

        var result = await Result.Try(
            async Task () =>
            {
                var migrationResult = await _commandExecutor.Send(
                    new MigrateLegacyArrSettingsCommand(),
                    _appLifetime.ApplicationStopping
                );
                migrationResult.LogIfFailed();

                var genreTypeRecalculationResult = await _commandExecutor.Send(
                    new RecalculatePlexGenreTypesCommand(),
                    _appLifetime.ApplicationStopping
                );
                genreTypeRecalculationResult.LogIfFailed();

                var integrationCheckResult = await _commandExecutor.Send(
                    new NotifyArrAppsOnStartupCommand(),
                    _appLifetime.ApplicationStopping
                );
                integrationCheckResult.LogIfFailed();
            },
            exception =>
            {
                if (exception is OperationCanceledException && _appLifetime.ApplicationStopping.IsCancellationRequested)
                {
                    _log.Here().Debug("Boot.OnStarted was cancelled because application shutdown was requested");
                    return new ExceptionalError("Operation was cancelled", exception);
                }

                _log.Here().Error(exception, "Unexpected error while running post-startup tasks");
                return new ExceptionalError(exception);
            }
        );

        result.LogIfFailed();
    }

    private void OnStopping()
    {
        _log.Here().Debug("Boot.OnStopping has been called");

        // Perform on-stopping activities here
    }

    private void OnStopped()
    {
        _log.Here().Debug("Boot.OnStopped has been called");

        // Perform post-stopped activities here
        _log.Here().Information("Reaparr has been shutdown! R.I.P. ");
    }

    #endregion
}
