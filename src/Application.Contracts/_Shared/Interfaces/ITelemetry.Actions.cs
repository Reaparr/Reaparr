using System.Diagnostics;

namespace Reaparr.Application.Contracts;

public partial interface ITelemetry
{
    /// <summary>
    /// Records an authentication attempt.
    /// </summary>
    void AuthenticationProcessed(TelemetryAction action, ResultBase result, bool? rememberMe = null);

    /// <summary>
    /// Records a credentials update.
    /// </summary>
    void CredentialsUpdateProcessed(ResultBase result, bool usernameChanged, bool passwordChanged);

    /// <summary>
    /// Records a folder directory listing.
    /// </summary>
    void FolderDirectoryListed(string path, Result<FileSystemResult> result);

    /// <summary>
    /// Records a folder path change.
    /// </summary>
    void FolderPathChanged(FolderPath folderPath, TelemetryAction action);

    /// <summary>
    /// Records deletion of folder paths.
    /// </summary>
    void FolderPathDeleted(int folderPathId, int deletedCount);

    /// <summary>
    /// Records a notification visibility change.
    /// </summary>
    void NotificationVisibilityProcessed(int notificationId, bool hidden, int affectedCount);

    /// <summary>
    /// Records cleared notifications.
    /// </summary>
    void NotificationsCleared(int deletedCount);

    /// <summary>
    /// Records completion of an application update download.
    /// </summary>
    void UpdateDownloadFinished(
        ResultBase result,
        string? targetVersion = null,
        bool? pendingRestart = null
    );

    /// <summary>
    /// Records acceptance of an application update.
    /// </summary>
    void UpdateApplyAccepted(string packageId, string version);

    /// <summary>
    /// Records completion of the update launcher.
    /// </summary>
    void UpdateLauncherFinished(ResultBase result, string packageId, string version);

    /// <summary>
    /// Starts telemetry for the update launcher.
    /// </summary>
    Activity? StartUpdateLauncher(string packageId, string version, ActivityContext parentContext = default);
}
