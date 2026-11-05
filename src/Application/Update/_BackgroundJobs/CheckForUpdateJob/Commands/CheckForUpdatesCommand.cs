namespace Reaparr.Application;

/// <summary>
/// Checks whether an application update is available via Velopack or the GitHub fallback.
/// </summary>
public record CheckForUpdatesCommand : ICommand<Result<AppUpdateCheckResult>>;

public class CheckForUpdatesCommandHandler : ICommandHandler<CheckForUpdatesCommand, Result<AppUpdateCheckResult>>
{
    private readonly ILogger _log;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IAppBuildInfo _appBuildInfo;
    private readonly UpdateManager _velopackManager;

    private AppUpdateCheckResult NoUpdate() =>
        new()
        {
            IsUpdateAvailable = false,
            NewestVersion = _appBuildInfo.InformationalVersion,
            CurrentVersion = _appBuildInfo.InformationalVersion,
            ReleaseNotes = [],
        };

    public CheckForUpdatesCommandHandler(
        ILogger log,
        ICommandExecutor commandExecutor,
        IAppBuildInfo appBuildInfo,
        UpdateManager velopackManager
    )
    {
        _log = log.ForContext<CheckForUpdatesCommandHandler>();
        _commandExecutor = commandExecutor;
        _appBuildInfo = appBuildInfo;
        _velopackManager = velopackManager;
    }

    public async Task<Result<AppUpdateCheckResult>> ExecuteAsync(
        CheckForUpdatesCommand command,
        CancellationToken cancellationToken
    )
    {
        // Desktop mode
        if (_appBuildInfo.IsDesktopMode)
        {
            var isInstalled = _velopackManager.IsInstalled;
            var appId = _velopackManager.AppId;
            var currentVersion = _velopackManager.CurrentVersion;

            if (!isInstalled)
            {
                _log.Here()
                    .Information(
                        "Skipping update check because the application is not installed; AppId: {AppId}; VelopackCurrentVersion: {VelopackCurrentVersion}; BuildInformationalVersion: {InformationalVersion}; DesktopMode: {IsDesktopMode}",
                        appId,
                        currentVersion,
                        _appBuildInfo.InformationalVersion,
                        _appBuildInfo.IsDesktopMode
                    );
                return Result.Ok(NoUpdate());
            }

            _log.Here()
                .Information(
                    "Checking for Velopack updates for {AppId}; Installed: {IsInstalled}; VelopackCurrentVersion: {VelopackCurrentVersion}; BuildInformationalVersion: {InformationalVersion}; DesktopMode: {IsDesktopMode}",
                    appId,
                    isInstalled,
                    currentVersion,
                    _appBuildInfo.InformationalVersion,
                    _appBuildInfo.IsDesktopMode
                );

            var updateResult = await Result.Try(() => _velopackManager.CheckForUpdatesAsync());
            if (updateResult.IsFailed)
            {
                _log.Here()
                    .Warning(
                        "Velopack update check failed; AppId: {AppId}; VelopackCurrentVersion: {VelopackCurrentVersion}; BuildInformationalVersion: {InformationalVersion}; Errors: {Errors}",
                        appId,
                        currentVersion,
                        _appBuildInfo.InformationalVersion,
                        string.Join("; ", updateResult.Errors.Select(x => x.Message))
                    );
                return updateResult.LogError();
            }

            var updateInfo = updateResult.Value;
            if (updateInfo is null)
            {
                _log.Here()
                    .Information(
                        "No Velopack update available; AppId: {AppId}; VelopackCurrentVersion: {VelopackCurrentVersion}; BuildInformationalVersion: {InformationalVersion}; Installed: {IsInstalled}",
                        appId,
                        currentVersion,
                        _appBuildInfo.InformationalVersion,
                        isInstalled
                    );
                return Result.Ok(NoUpdate());
            }

            var targetVersion = updateInfo.TargetFullRelease.Version.ToString();
            _log.Here()
                .Information(
                    "Update available: {Version}; AppId: {AppId}; VelopackCurrentVersion: {VelopackCurrentVersion}; BuildInformationalVersion: {InformationalVersion}",
                    targetVersion,
                    appId,
                    currentVersion,
                    _appBuildInfo.InformationalVersion
                );

            var desktopReleasesResult = await _commandExecutor.Send(new GetGitHubReleasesCommand(), cancellationToken);

            return Result.Ok(
                new AppUpdateCheckResult
                {
                    IsUpdateAvailable = true,
                    NewestVersion = targetVersion,
                    CurrentVersion = _appBuildInfo.InformationalVersion,
                    ReleaseNotes = desktopReleasesResult.IsSuccess ? desktopReleasesResult.Value : [],
                }
            );
        }

        var releasesResult = await _commandExecutor.Send(new GetGitHubReleasesCommand(), cancellationToken);
        var releases = releasesResult.IsSuccess ? releasesResult.Value : [];

        var nonDesktopUpdateResult = await Result.Try(() => _velopackManager.CheckForUpdatesAsync());
        if (nonDesktopUpdateResult.IsSuccess && nonDesktopUpdateResult.Value is not null)
        {
            var targetVersion = nonDesktopUpdateResult.Value.TargetFullRelease.Version.ToString();
            _log.Here().Information("Update available: {Version}", targetVersion);

            return Result.Ok(
                new AppUpdateCheckResult
                {
                    IsUpdateAvailable = true,
                    NewestVersion = targetVersion,
                    CurrentVersion = _appBuildInfo.InformationalVersion,
                    ReleaseNotes = releases,
                }
            );
        }

        if (releasesResult.IsFailed)
            return releasesResult.ToResult<AppUpdateCheckResult>();

        // Docker Mode
        var isDevRelease = _appBuildInfo.IsDevRelease;
        var latest = releases.FirstOrDefault(r => r.IsDevRelease == isDevRelease);
        if (latest is null)
        {
            _log.Here().Information("No update available");
            return Result.Ok(NoUpdate());
        }

        var latestVersion = latest.Version.TrimStart('v');

        _log.Here().Information("Update available: {Version}", latestVersion);

        return Result.Ok(
            new AppUpdateCheckResult
            {
                IsUpdateAvailable = true,
                NewestVersion = latestVersion,
                CurrentVersion = _appBuildInfo.InformationalVersion,
                ReleaseNotes = releases,
            }
        );
    }
}
