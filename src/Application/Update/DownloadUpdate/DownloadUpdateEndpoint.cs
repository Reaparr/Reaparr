namespace Reaparr.Application;

/// <summary>
/// Downloads the latest desktop update package when an update is available.
/// </summary>
public class DownloadUpdateEndpoint : EndpointWithoutRequest<BaseResultDTO>
{
    private readonly UpdateManager _velopackManager;
    private readonly IAppBuildInfo _appBuildInfo;
    private readonly IProgressHubService _progressHub;
    private readonly ILogger _log;
    private readonly ITelemetry _telemetry;

    public DownloadUpdateEndpoint(
        ILogger log,
        IAppBuildInfo appBuildInfo,
        UpdateManager velopackManager,
        IProgressHubService progressHub,
        ITelemetry telemetry
    )
    {
        _log = log.ForContext<DownloadUpdateEndpoint>();
        _appBuildInfo = appBuildInfo;
        _velopackManager = velopackManager;
        _progressHub = progressHub;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Post(ApiRoutes.UpdateController + "/DownloadUpdate");
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
            _log.Here().Debug("Skipping update download — not running in desktop mode");
            var unsupportedResult = Result.Fail("Desktop updates are not supported in the current runtime mode");
            _telemetry.UpdateDownloadFinished(unsupportedResult);
            await Send.FluentResult(unsupportedResult, ct);
            return;
        }

        string? targetVersion = null;
        bool? pendingRestart = null;

        var result = await Result.Try(async Task () =>
        {
            _log.Here().Information("Checking for Velopack update before download");

            // Single network call: fetch latest update info, then download it.
            var updateInfo = await _velopackManager.CheckForUpdatesAsync();
            if (updateInfo is null)
            {
                _log.Here().Information("No Velopack update available to download");
                return;
            }

            targetVersion = updateInfo.TargetFullRelease.Version.ToString();
            _log.Here().Information("Downloading Velopack update {Version}", targetVersion);

            await _velopackManager.DownloadUpdatesAsync(
                updateInfo,
                progress =>
                {
                    var dto = new AppUpdateDownloadProgressDTO(progress);
                    _ = _progressHub.SendAppUpdateDownloadProgressAsync(dto);
                },
                ct
            );

            pendingRestart = _velopackManager.UpdatePendingRestart is not null;
            _log.Here()
                .Information(
                    "Downloaded Velopack update {Version}; PendingRestart: {HasPendingRestart}",
                    targetVersion,
                    pendingRestart
                );
        });

        _telemetry.UpdateDownloadFinished(result, targetVersion, pendingRestart);
        if (result.IsFailed)
            _telemetry.CaptureErrors(result, TelemetryActivity.UpdateLauncher);
        await Send.FluentResult(result, ct);
    }
}
