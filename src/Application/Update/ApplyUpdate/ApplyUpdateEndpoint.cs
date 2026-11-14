using Microsoft.Extensions.Hosting;

namespace Reaparr.Application;

/// <summary>
/// Applies a downloaded desktop update and restarts the application.
/// </summary>
public class ApplyUpdateEndpoint : EndpointWithoutRequest<BaseResultDTO>
{
    private readonly IAppBuildInfo _appBuildInfo;
    private readonly UpdateManager _velopackManager;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ILogger _log;
    private readonly ITelemetry _telemetry;

    public ApplyUpdateEndpoint(
        ILogger log,
        IAppBuildInfo appBuildInfo,
        UpdateManager velopackManager,
        IHostApplicationLifetime appLifetime,
        ITelemetry telemetry
    )
    {
        _log = log.ForContext<ApplyUpdateEndpoint>();
        _appBuildInfo = appBuildInfo;
        _velopackManager = velopackManager;
        _appLifetime = appLifetime;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Post(ApiRoutes.UpdateController + "/execute");
        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext);

        if (!_appBuildInfo.IsDesktopMode)
        {
            _log.Here().Debug("Skipping update apply — not running in desktop mode");
            await Send.FluentResult(Result.Fail("Desktop updates are not supported in the current runtime mode"), ct);
            return;
        }

        var asset = _velopackManager.UpdatePendingRestart;
        if (asset is null)
        {
            _log.Here().Warning("Skipping Velopack update apply because no update is staged");
            await Send.FluentResult(Result.Fail("No update staged"), ct);
            return;
        }

        _log.Here()
            .Warning(
                "Preparing to apply Velopack update and restart Reaparr; AppId: {AppId}; CurrentVersion: {CurrentVersion}; PackageId: {PackageId}; PackageVersion: {PackageVersion}",
                _velopackManager.AppId,
                _velopackManager.CurrentVersion,
                asset.PackageId,
                asset.Version
            );

        var parentContext = Activity.Current?.Context ?? default;
        HttpContext.Response.OnCompleted(() =>
        {
            _ = Task.Run(() => ApplyUpdateAfterResponseAsync(asset, parentContext));
            return Task.CompletedTask;
        });

        _telemetry.UpdateApplyAccepted(asset.PackageId, asset.Version.ToString());
        await Send.FluentResult(Result.Ok(), ct);
    }

    private async Task ApplyUpdateAfterResponseAsync(VelopackAsset asset, ActivityContext parentContext)
    {
        using var activity = _telemetry.StartUpdateLauncher(asset.PackageId, asset.Version.ToString(), parentContext);

        _log.Here()
            .Warning(
                "Launching Velopack updater to wait for Reaparr exit, apply update, and restart; AppId: {AppId}; CurrentVersion: {CurrentVersion}; PackageId: {PackageId}; PackageVersion: {PackageVersion}",
                _velopackManager.AppId,
                _velopackManager.CurrentVersion,
                asset.PackageId,
                asset.Version
            );

        var launchResult = Result.Try(() =>
            _velopackManager.WaitExitThenApplyUpdates(asset, silent: false, restart: true, restartArgs: [])
        );
        _telemetry.UpdateLauncherFinished(launchResult, asset.PackageId, asset.Version.ToString());

        if (launchResult.IsCancelledOrFailed)
        {
            launchResult.LogIfFailed();
            _telemetry.CaptureErrors(launchResult, TelemetryActivity.UpdateLauncher);
            return;
        }

        try
        {
            _log.Here()
                .Warning(
                    "Velopack updater launched; stopping Reaparr host before exiting process; AppId: {AppId}; CurrentVersion: {CurrentVersion}; PackageId: {PackageId}; PackageVersion: {PackageVersion}",
                    _velopackManager.AppId,
                    _velopackManager.CurrentVersion,
                    asset.PackageId,
                    asset.Version
                );

            _appLifetime.StopApplication();

            var stoppedInTime = await Task.Run(() =>
                _appLifetime.ApplicationStopped.WaitHandle.WaitOne(TimeSpan.FromSeconds(60))
            );

            if (!stoppedInTime)
            {
                _log.Here()
                    .Warning(
                        "Timed out waiting for ApplicationStopped before update exit; forcing process exit to continue Velopack update; AppId: {AppId}; CurrentVersion: {CurrentVersion}; PackageId: {PackageId}; PackageVersion: {PackageVersion}",
                        _velopackManager.AppId,
                        _velopackManager.CurrentVersion,
                        asset.PackageId,
                        asset.Version
                    );
            }

            _log.Here()
                .Warning(
                    "Exiting Reaparr process so Velopack can apply update and restart; AppId: {AppId}; CurrentVersion: {CurrentVersion}; PackageId: {PackageId}; PackageVersion: {PackageVersion}",
                    _velopackManager.AppId,
                    _velopackManager.CurrentVersion,
                    asset.PackageId,
                    asset.Version
                );

            global::System.Environment.Exit(0);
        }
        catch (Exception ex)
        {
            _telemetry.CaptureErrors(Result.Fail(new ExceptionalError(ex)), TelemetryActivity.UpdateLauncher);
            _log.Here()
                .Error(
                    ex,
                    "Velopack update shutdown failed; AppId: {AppId}; CurrentVersion: {CurrentVersion}; PackageId: {PackageId}; PackageVersion: {PackageVersion}",
                    _velopackManager.AppId,
                    _velopackManager.CurrentVersion,
                    asset.PackageId,
                    asset.Version
                );
        }
    }
}
