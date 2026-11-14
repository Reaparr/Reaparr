namespace Reaparr.Application;

public sealed partial class Telemetry
{
    /// <inheritdoc/>
    public void DownloadPreviewFinished(
        DownloadPreviewRequest request,
        ResultBase result,
        IReadOnlyCollection<(int Id, string Title, long Size, int ChildCount, PlexMediaType MediaType)> report
    )
    {
        CaptureErrors(result, TelemetryActivity.DownloadPreview);

        var properties = new Dictionary<string, object>
        {
            [TelemetryProperty.Outcome] = Outcome(result, result.IsSuccess && report.Count == 0),
            [TelemetryProperty.Downloads.SelectedCount] = request.DownloadMedias.Sum(x => x.MediaIds.Count),
            [TelemetryProperty.Downloads.SelectionCount] = request.DownloadMedias.Count,
            [TelemetryProperty.Downloads.SelectedMediaTypes] = request
                .DownloadMedias.Select(x => x.Type.ToString())
                .Distinct()
                .Order()
                .ToArray(),
            [TelemetryProperty.Downloads.CustomDestinationFolderPath] = request.CustomDestinationFolderPath,
        };

        if (request.DestinationFolderPathId is { } destinationFolderPathId)
            properties[TelemetryProperty.Downloads.DestinationFolderPathId] = destinationFolderPathId;

        if (result.IsSuccess)
        {
            properties[TelemetryProperty.Downloads.PreviewCount] = report.Count;
            properties[TelemetryProperty.Downloads.PreviewTotalSize] = report.Sum(x => x.Size);
            properties[TelemetryProperty.Downloads.PreviewTotalChildren] = report.Sum(x => x.ChildCount);
            properties[TelemetryProperty.Downloads.Report] = report
                .Select(x => new Dictionary<string, object>
                {
                    [TelemetryProperty.Downloads.ReportId] = x.Id,
                    [TelemetryProperty.Downloads.ReportTitle] = x.Title,
                    [TelemetryProperty.Downloads.ReportSize] = x.Size,
                    [TelemetryProperty.Downloads.ReportChildCount] = x.ChildCount,
                    [TelemetryProperty.Downloads.ReportMediaType] = x.MediaType.ToString(),
                })
                .ToArray();
        }

        _ = Capture(TelemetryEvent.DownloadPreviewFinished, properties);
    }

    /// <inheritdoc/>
    public void DownloadCreationProcessed(CreateDownloadTasksRequest request, Result<DownloadTaskCreationReport> result)
    {
        var properties = new Dictionary<string, object>
        {
            [TelemetryProperty.Outcome] = Outcome(result, result.IsSuccess && result.Value.Total == 0),
            [TelemetryProperty.Downloads.SelectedCount] = request.DownloadMedias.Sum(x => x.MediaIds.Count),
            [TelemetryProperty.Downloads.SelectionCount] = request.DownloadMedias.Count,
            [TelemetryProperty.Downloads.SelectedMediaTypes] = request
                .DownloadMedias.Select(x => x.Type.ToString())
                .Distinct()
                .Order()
                .ToArray(),
        };

        if (result.IsSuccess)
        {
            var report = result.Value;
            properties[TelemetryProperty.Downloads.CreatedTotal] = report.Total;
            properties[TelemetryProperty.Downloads.CreatedReport] = new Dictionary<string, object>
            {
                [TelemetryProperty.Downloads.Movies] = report.Movies,
                [TelemetryProperty.Downloads.TvShows] = report.TvShows,
                [TelemetryProperty.Downloads.Seasons] = report.Seasons,
                [TelemetryProperty.Downloads.Episodes] = report.Episodes,
                [TelemetryProperty.Downloads.PhotoFolders] = report.PhotoFolders,
                [TelemetryProperty.Downloads.PhotoImages] = report.PhotoImages,
                [TelemetryProperty.Downloads.MusicArtists] = report.MusicArtists,
                [TelemetryProperty.Downloads.MusicAlbums] = report.MusicAlbums,
                [TelemetryProperty.Downloads.MusicTracks] = report.MusicTracks,
                [TelemetryProperty.Downloads.OtherVideos] = report.OtherVideos,
            };
        }

        _ = Capture(TelemetryEvent.DownloadCreationProcessed, properties);
    }

    /// <inheritdoc/>
    public void DownloadControlProcessed(
        TelemetryAction action,
        Guid downloadTaskId,
        ResultBase result,
        bool deleteFiles = false
    )
    {
        var properties = new Dictionary<string, object>
        {
            [TelemetryProperty.Action] = action.ToString(),
            [TelemetryProperty.TaskIds] = new[] { downloadTaskId },
            [TelemetryProperty.Outcome] = Outcome(result),
        };

        if (deleteFiles)
            properties[TelemetryProperty.DeleteFiles] = true;

        _ = Capture(TelemetryEvent.DownloadControlProcessed, properties);
    }

    /// <inheritdoc/>
    public void DownloadDeletionProcessed(
        IReadOnlyCollection<Guid> requestedTaskIds,
        IReadOnlyCollection<DownloadTaskKey> taskKeys,
        ResultBase result
    )
    {
        var properties = new Dictionary<string, object>
        {
            [TelemetryProperty.TaskIds] = requestedTaskIds.ToArray(),
            [TelemetryProperty.TaskKeys] = taskKeys.Select(ToTaskKeyProperties).ToArray(),
            [TelemetryProperty.DeleteFiles] = true,
            [TelemetryProperty.Outcome] = Outcome(result, taskKeys.Count == 0),
        };

        _ = Capture(TelemetryEvent.DownloadDeletionProcessed, properties);
    }

    /// <inheritdoc/>
    public void CompletedDownloadsCleared(
        IReadOnlyCollection<Guid> requestedTaskIds,
        IReadOnlyCollection<DownloadTaskKey> taskKeys,
        Result<int> result
    )
    {
        var properties = new Dictionary<string, object>
        {
            [TelemetryProperty.Scope] = TelemetryScope.Selection.ToString(),
            [TelemetryProperty.TaskIds] = requestedTaskIds.ToArray(),
            [TelemetryProperty.TaskKeys] = taskKeys.Select(ToTaskKeyProperties).ToArray(),
            [TelemetryProperty.Outcome] = Outcome(result, result.IsSuccess && result.Value == 0),
        };

        if (result.IsSuccess)
            properties[TelemetryProperty.MatchedCount] = result.Value;

        _ = Capture(TelemetryEvent.CompletedDownloadsCleared, properties);
    }

    /// <inheritdoc/>
    public void CompletedDownloadsCleared(int plexServerId, Result<int> result)
    {
        var properties = new Dictionary<string, object>
        {
            [TelemetryProperty.Scope] = TelemetryScope.Server.ToString(),
            [TelemetryProperty.ServerId] = plexServerId,
            [TelemetryProperty.Outcome] = Outcome(result, result.IsSuccess && result.Value == 0),
        };

        if (result.IsSuccess)
            properties[TelemetryProperty.MatchedCount] = result.Value;

        _ = Capture(TelemetryEvent.CompletedDownloadsCleared, properties);
    }

    /// <inheritdoc/>
    public void DownloadLogsCleared(DownloadTaskKey taskKey, Result<int> result)
    {
        var properties = ToTaskKeyProperties(taskKey);
        properties[TelemetryProperty.Outcome] = Outcome(result, result.IsSuccess && result.Value == 0);

        if (result.IsSuccess)
            properties[TelemetryProperty.DeletedCount] = result.Value;

        _ = Capture(TelemetryEvent.DownloadLogsCleared, properties);
    }

    /// <inheritdoc/>
    public void DownloadLifecycleChanged(DownloadTaskKey rootTaskKey, DownloadStatus status)
    {
        var properties = ToTaskKeyProperties(rootTaskKey);
        properties[TelemetryProperty.Status] = status.ToString();
        _ = Capture(TelemetryEvent.DownloadLifecycleChanged, properties);
    }

    /// <inheritdoc/>
    public Activity? StartDirectDownload(DownloadTaskFileBase downloadTask, int segments) =>
        StartActivityCore(
        TelemetryActivity.DirectDownload,
        tags: new Dictionary<string, object?>
        {
            [TelemetryProperty.TaskId] = downloadTask.Id,
            [TelemetryProperty.Downloads.TaskType] = downloadTask.DownloadTaskType.ToString(),
            [TelemetryProperty.ServerId] = downloadTask.PlexServerId,
            [TelemetryProperty.LibraryId] = downloadTask.PlexLibraryId,
            [TelemetryProperty.Downloads.Client] = PlexDownloadClientType.Direct.ToString(),
            [TelemetryProperty.Downloads.Source] = PlexDownloadClientType.Direct.ToString(),
            [TelemetryProperty.Downloads.DownloadFileNameTag] = downloadTask.FileName,
            [TelemetryProperty.Downloads.DownloadSourcePathTag] = downloadTask.FileLocationUrl,
            [TelemetryProperty.Downloads.DownloadPathTag] = downloadTask.DownloadFilePath,
            [TelemetryProperty.Downloads.DestinationPathTag] = downloadTask.DestinationFilePath,
            [TelemetryProperty.Downloads.SegmentsTag] = segments,
        }
    );

    /// <inheritdoc/>
    public Activity? StartDirectDownloadAttempt(
        DownloadTaskFileBase downloadTask,
        int attempt,
        int configuredSegments,
        int segments,
        long effectiveSpeedLimitBytes
    ) => StartActivityCore(
        TelemetryActivity.DirectDownloadAttempt,
        tags: new Dictionary<string, object?>
        {
            [TelemetryProperty.TaskId] = downloadTask.Id,
            [TelemetryProperty.Downloads.TaskType] = downloadTask.DownloadTaskType.ToString(),
            [TelemetryProperty.ServerId] = downloadTask.PlexServerId,
            [TelemetryProperty.LibraryId] = downloadTask.PlexLibraryId,
            [TelemetryProperty.Downloads.Client] = PlexDownloadClientType.Direct.ToString(),
            [TelemetryProperty.Downloads.Source] = PlexDownloadClientType.Direct.ToString(),
            [TelemetryProperty.Downloads.Attempt] = attempt,
            [TelemetryProperty.Downloads.ConfiguredSegments] = configuredSegments,
            [TelemetryProperty.Downloads.Segments] = segments,
            [TelemetryProperty.Downloads.DownloadPath] = downloadTask.DownloadFilePath,
            [TelemetryProperty.Downloads.EffectiveSpeedLimitBytes] = effectiveSpeedLimitBytes,
        }
    );

    /// <inheritdoc/>
    public Activity? StartDashDownload(
        DownloadTaskFileBase downloadTask,
        VideoQuality outputQuality,
        string normalizedFileName
    ) => StartActivityCore(
        TelemetryActivity.DashDownload,
        tags: new Dictionary<string, object?>
        {
            [TelemetryProperty.TaskId] = downloadTask.Id,
            [TelemetryProperty.Downloads.TaskType] = downloadTask.DownloadTaskType.ToString(),
            [TelemetryProperty.ServerId] = downloadTask.PlexServerId,
            [TelemetryProperty.LibraryId] = downloadTask.PlexLibraryId,
            [TelemetryProperty.Downloads.Client] = PlexDownloadClientType.Dash.ToString(),
            [TelemetryProperty.Downloads.Source] = PlexDownloadClientType.Dash.ToString(),
            [TelemetryProperty.Downloads.DownloadFileNameTag] = downloadTask.FileName,
            [TelemetryProperty.Downloads.DownloadSourcePathTag] = downloadTask.FileLocationUrl,
            [TelemetryProperty.Downloads.DownloadPathTag] = downloadTask.DownloadFilePath,
            [TelemetryProperty.Downloads.DestinationPathTag] = downloadTask.DestinationFilePath,
            [TelemetryProperty.Downloads.OutputQualityTag] = outputQuality.ToString(),
            [TelemetryProperty.Downloads.NormalizedFileNameTag] = normalizedFileName,
        }
    );

    /// <inheritdoc/>
    public Activity? StartDashDownloadAttempt(
        DownloadTaskFileBase downloadTask,
        string outputPath,
        long effectiveSpeedLimitBytes
    ) => StartActivityCore(
        TelemetryActivity.DashDownloadAttempt,
        tags: new Dictionary<string, object?>
        {
            [TelemetryProperty.TaskId] = downloadTask.Id,
            [TelemetryProperty.Downloads.TaskType] = downloadTask.DownloadTaskType.ToString(),
            [TelemetryProperty.ServerId] = downloadTask.PlexServerId,
            [TelemetryProperty.LibraryId] = downloadTask.PlexLibraryId,
            [TelemetryProperty.Downloads.Client] = PlexDownloadClientType.Dash.ToString(),
            [TelemetryProperty.Downloads.Source] = PlexDownloadClientType.Dash.ToString(),
            [TelemetryProperty.Downloads.NormalizedFileName] = downloadTask.FileName,
            [TelemetryProperty.Downloads.OutputPath] = outputPath,
            [TelemetryProperty.Downloads.DownloadPath] = downloadTask.DownloadFilePath,
            [TelemetryProperty.Downloads.DestinationPath] = downloadTask.DestinationFilePath,
            [TelemetryProperty.Downloads.EffectiveSpeedLimitBytes] = effectiveSpeedLimitBytes,
        }
    );

    /// <inheritdoc/>
    public Activity? StartMoveDownloadFile(
        DownloadTaskFileBase downloadTask,
        string sourcePath,
        string destinationPath,
        bool keepInDownloads,
        TelemetryRetentionReason retentionReason
    ) => StartActivityCore(
        TelemetryActivity.MoveDownloadFile,
        tags: new Dictionary<string, object?>
        {
            [TelemetryProperty.TaskId] = downloadTask.Id,
            [TelemetryProperty.Downloads.TaskType] = downloadTask.DownloadTaskType.ToString(),
            [TelemetryProperty.ServerId] = downloadTask.PlexServerId,
            [TelemetryProperty.LibraryId] = downloadTask.PlexLibraryId,
            [TelemetryProperty.Downloads.SourcePath] = sourcePath,
            [TelemetryProperty.Downloads.DestinationPath] = destinationPath,
            [TelemetryProperty.Downloads.DataTotal] = downloadTask.DataTotal,
            [TelemetryProperty.Downloads.KeepInDownloads] = keepInDownloads,
            [TelemetryProperty.Downloads.RetentionReason] = retentionReason.ToString(),
        }
    );

    /// <inheritdoc/>
    public void SetMoveDownloadFileSourcePath(Activity? activity, string sourcePath) =>
        activity?.SetTag(TelemetryProperty.Downloads.SourcePath, sourcePath);

    /// <inheritdoc/>
    public Activity? StartDownloadCleanup(DownloadTaskFileBase downloadTask) =>
        StartActivityCore(
            TelemetryActivity.DownloadCleanup,
            tags: new Dictionary<string, object?>
            {
                [TelemetryProperty.TaskId] = downloadTask.Id,
                [TelemetryProperty.Downloads.TaskType] = downloadTask.DownloadTaskType.ToString(),
                [TelemetryProperty.ServerId] = downloadTask.PlexServerId,
                [TelemetryProperty.LibraryId] = downloadTask.PlexLibraryId,
                [TelemetryProperty.Downloads.DownloadPathTag] = downloadTask.DownloadFilePath,
                [TelemetryProperty.Downloads.CleanupDirectoryTag] = downloadTask.DownloadDirectory,
            }
        );

    /// <inheritdoc/>
    public void SetDownloadJobContext(DownloadTaskFileBase downloadTask, PlexDownloadClientType clientType)
    {
        var activity = Activity.Current;
        if (activity is null)
            return;

        activity.SetTag(TelemetryProperty.TaskId, downloadTask.Id);
        activity.SetTag(TelemetryProperty.Downloads.TaskType, downloadTask.DownloadTaskType.ToString());
        activity.SetTag(TelemetryProperty.ServerId, downloadTask.PlexServerId);
        activity.SetTag(TelemetryProperty.LibraryId, downloadTask.PlexLibraryId);
        activity.SetTag(TelemetryProperty.Downloads.Source, TelemetryProperty.Downloads.PlexSource);
        activity.SetTag(TelemetryProperty.Downloads.DownloadFileNameTag, downloadTask.FileName);
        activity.SetTag(TelemetryProperty.Downloads.DownloadSourcePathTag, downloadTask.FileLocationUrl);
        activity.SetTag(TelemetryProperty.Downloads.DownloadPathTag, downloadTask.DownloadFilePath);
        activity.SetTag(TelemetryProperty.Downloads.DestinationPathTag, downloadTask.DestinationFilePath);
        activity.SetTag(TelemetryProperty.Downloads.ClientTag, clientType.ToString());
        activity.SetTag(TelemetryProperty.Downloads.SourceTag, clientType.ToString());
    }

    /// <inheritdoc/>
    public void SetMoveDownloadJobContext(DownloadTaskKey taskKey, int retryCount)
    {
        var activity = Activity.Current;
        if (activity is null)
            return;

        activity.SetTag(TelemetryProperty.TaskId, taskKey.Id);
        activity.SetTag(TelemetryProperty.Downloads.TaskType, taskKey.Type.ToString());
        activity.SetTag(TelemetryProperty.ServerId, taskKey.PlexServerId);
        activity.SetTag(TelemetryProperty.LibraryId, taskKey.PlexLibraryId);
        activity.SetTag(TelemetryProperty.Downloads.RetryCount, retryCount);
    }

    /// <inheritdoc/>
    public void CompleteDownloadJob(Activity? activity, ResultBase result) =>
        activity?.SetTag(TelemetryProperty.Downloads.OutcomeTag, Outcome(result));

    /// <inheritdoc/>
    public void CompleteScheduledDownloadLimits(Activity? activity, ResultBase result) =>
        activity?.SetTag(TelemetryProperty.Outcome, Outcome(result));

    private static Dictionary<string, object> ToTaskKeyProperties(DownloadTaskKey taskKey) =>
        new()
        {
            [TelemetryProperty.TaskId] = taskKey.Id,
            [TelemetryProperty.Downloads.TaskType] = taskKey.Type.ToString(),
            [TelemetryProperty.ServerId] = taskKey.PlexServerId,
            [TelemetryProperty.LibraryId] = taskKey.PlexLibraryId,
        };
}
