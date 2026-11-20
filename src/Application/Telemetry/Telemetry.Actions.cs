namespace Reaparr.Application;

public sealed partial class Telemetry
{
    /// <inheritdoc/>
    public void AuthenticationProcessed(TelemetryAction action, ResultBase result, bool? rememberMe = null)
    {
        var outcome =
            result.IsSuccess ? TelemetryOutcome.Succeeded
            : result.HasStatusCode(StatusCodes.Status403Forbidden) ? TelemetryOutcome.LockedOut
            : TelemetryOutcome.Rejected;
        var properties = new Dictionary<string, object>
        {
            [TelemetryProperty.Action] = action.ToString(),
            [TelemetryProperty.Outcome] = outcome.ToString(),
        };
        if (rememberMe.HasValue)
            properties[TelemetryProperty.Actions.RememberMe] = rememberMe.Value;

        Capture(TelemetryEvent.AuthenticationProcessed, properties);
    }

    /// <inheritdoc/>
    public void CredentialsUpdateProcessed(ResultBase result, bool usernameChanged, bool passwordChanged)
    {
        var outcome = result.IsFailed
            ? usernameChanged
                ? TelemetryOutcome.Partial
                : TelemetryOutcome.Failed
            : usernameChanged || passwordChanged
                ? TelemetryOutcome.Succeeded
                : TelemetryOutcome.NoOp;
        Capture(
            TelemetryEvent.CredentialsUpdateProcessed,
            new Dictionary<string, object>
            {
                [TelemetryProperty.Outcome] = outcome.ToString(),
                [TelemetryProperty.Actions.UsernameChanged] = usernameChanged,
                [TelemetryProperty.Actions.PasswordChanged] = passwordChanged,
            }
        );
    }

    /// <inheritdoc/>
    public void FolderDirectoryListed(string path, Result<FileSystemResult> result)
    {
        var properties = new Dictionary<string, object>
        {
            [TelemetryProperty.Path] = path,
            [TelemetryProperty.Outcome] = Outcome(result),
        };
        if (result.IsSuccess)
        {
            properties[TelemetryProperty.Actions.DirectoryCount] = result.Value.Directories.Count;
            properties[TelemetryProperty.Actions.FileCount] = result.Value.Files.Count;
            properties[TelemetryProperty.Actions.RootListing] = result.Value.Current is null;
        }

        Capture(TelemetryEvent.FolderDirectoryListed, properties);
    }

    /// <inheritdoc/>
    public void FolderPathChanged(FolderPath folderPath, TelemetryAction action)
    {
        Capture(
            TelemetryEvent.FolderPathChanged,
            new Dictionary<string, object>
            {
                [TelemetryProperty.Action] = action.ToString(),
                [TelemetryProperty.Outcome] = nameof(TelemetryOutcome.Succeeded),
                [TelemetryProperty.FolderId] = folderPath.Id,
                [TelemetryProperty.Actions.FolderType] = folderPath.FolderType.ToString(),
                [TelemetryProperty.MediaType] = folderPath.MediaType.ToString(),
                [TelemetryProperty.DisplayName] = folderPath.DisplayName,
                [TelemetryProperty.DirectoryPath] = folderPath.DirectoryPath,
            }
        );
    }

    /// <inheritdoc/>
    public void FolderPathDeleted(int folderPathId, int deletedCount)
    {
        Capture(
            TelemetryEvent.FolderPathChanged,
            new Dictionary<string, object>
            {
                [TelemetryProperty.Action] = nameof(TelemetryAction.Delete),
                [TelemetryProperty.Outcome] = (
                    deletedCount == 0 ? TelemetryOutcome.NoOp : TelemetryOutcome.Succeeded
                ).ToString(),
                [TelemetryProperty.FolderId] = folderPathId,
                [TelemetryProperty.DeletedCount] = deletedCount,
            }
        );
    }

    /// <inheritdoc/>
    public void NotificationVisibilityProcessed(int notificationId, bool hidden, int affectedCount)
    {
        Capture(
            TelemetryEvent.NotificationVisibilityProcessed,
            new Dictionary<string, object>
            {
                [TelemetryProperty.Outcome] = (
                    affectedCount == 0 ? TelemetryOutcome.Rejected : TelemetryOutcome.Succeeded
                ).ToString(),
                [TelemetryProperty.Actions.NotificationId] = notificationId,
                [TelemetryProperty.Actions.Hidden] = hidden,
                [TelemetryProperty.MatchedCount] = affectedCount,
            }
        );
    }

    /// <inheritdoc/>
    public void NotificationsCleared(int deletedCount)
    {
        Capture(
            TelemetryEvent.NotificationsCleared,
            new Dictionary<string, object>
            {
                [TelemetryProperty.Outcome] = (
                    deletedCount == 0 ? TelemetryOutcome.NoOp : TelemetryOutcome.Succeeded
                ).ToString(),
                [TelemetryProperty.DeletedCount] = deletedCount,
            }
        );
    }

    /// <inheritdoc/>
    public void UpdateDownloadFinished(ResultBase result, string? targetVersion = null, bool? pendingRestart = null)
    {
        var outcome =
            !_appBuildInfo.IsDesktopMode ? nameof(TelemetryOutcome.Unsupported)
            : result.IsSuccess && targetVersion is null ? nameof(TelemetryOutcome.NoUpdate)
            : Outcome(result);
        var properties = new Dictionary<string, object> { [TelemetryProperty.Outcome] = outcome };
        if (targetVersion is not null)
            properties[TelemetryProperty.Version] = targetVersion;
        if (pendingRestart.HasValue)
            properties[TelemetryProperty.Actions.PendingRestart] = pendingRestart.Value;

        Capture(TelemetryEvent.UpdateDownloadFinished, properties);
    }

    /// <inheritdoc/>
    public void UpdateApplyAccepted(string packageId, string version)
    {
        Capture(
            TelemetryEvent.UpdateApplyAccepted,
            new Dictionary<string, object>
            {
                [TelemetryProperty.Outcome] = nameof(TelemetryOutcome.Accepted),
                [TelemetryProperty.Actions.PackageId] = packageId,
                [TelemetryProperty.Version] = version,
            }
        );
    }

    /// <inheritdoc/>
    public void UpdateLauncherFinished(ResultBase result, string packageId, string version)
    {
        Capture(
            TelemetryEvent.UpdateLauncherFinished,
            new Dictionary<string, object>
            {
                [TelemetryProperty.Outcome] = Outcome(result),
                [TelemetryProperty.Actions.PackageId] = packageId,
                [TelemetryProperty.Version] = version,
            }
        );
    }

    /// <inheritdoc/>
    public Activity? StartUpdateLauncher(string packageId, string version, ActivityContext parentContext = default) =>
        StartActivityCore(
            TelemetryActivity.UpdateLauncher,
            parentContext,
            [
                new KeyValuePair<string, object?>(TelemetryProperty.Actions.PackageId, packageId),
                new KeyValuePair<string, object?>(TelemetryProperty.Version, version),
            ],
            ActivityKind.Consumer
        );
}
