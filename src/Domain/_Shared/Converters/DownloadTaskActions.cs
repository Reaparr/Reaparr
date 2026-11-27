namespace Reaparr.Domain;

public static class DownloadTaskActions
{
    private static readonly ILogger _log = LogFactory.Create(typeof(DownloadTaskActions));

    private static readonly DownloadStatus[] _anyStatuses =
    [
        DownloadStatus.ServerUnreachable,
        DownloadStatus.AuthError,
        DownloadStatus.StorageError,
        DownloadStatus.SourceUnavailable,
        DownloadStatus.DownloadClientError,
        DownloadStatus.IntegrityError,
        DownloadStatus.Error,
        DownloadStatus.MoveError,
        DownloadStatus.Downloading,
        DownloadStatus.Paused,
        DownloadStatus.AutoPaused,
        DownloadStatus.MovePaused,
        DownloadStatus.AutoMovePaused,
        DownloadStatus.Stopped,
        DownloadStatus.Restarting,
        DownloadStatus.Moving,
        DownloadStatus.MoveFinished,
        DownloadStatus.DownloadFinished,
        DownloadStatus.Queued,
        DownloadStatus.Deleted,
        DownloadStatus.Completed,
    ];

    public static List<DownloadActions> Convert(DownloadStatus downloadStatus)
    {
        var actions = new List<DownloadActions> { DownloadActions.Details };

        // NOTE: When updating this, also update front-end: src/AppHost/ClientApp/src/composables/conversion.ts
        switch (downloadStatus)
        {
            case DownloadStatus.Unknown:
                actions.Add(DownloadActions.Delete);
                break;
            case DownloadStatus.Queued:
                actions.Add(DownloadActions.Start);
                actions.Add(DownloadActions.Delete);
                break;
            case DownloadStatus.Downloading:
                actions.Add(DownloadActions.Pause);
                actions.Add(DownloadActions.Stop);
                break;
            case DownloadStatus.DownloadFinished:
                actions.Add(DownloadActions.Start);
                actions.Add(DownloadActions.Delete);
                break;
            case DownloadStatus.MoveFinished:
                actions.Add(DownloadActions.Delete);
                break;
            case DownloadStatus.Paused:
            case DownloadStatus.AutoPaused:
            case DownloadStatus.MovePaused:
            case DownloadStatus.AutoMovePaused:
                actions.Add(DownloadActions.Start);
                actions.Add(DownloadActions.Stop);
                actions.Add(DownloadActions.Delete);
                break;
            case DownloadStatus.Completed:
                actions.Add(DownloadActions.Clear);
                actions.Add(DownloadActions.Restart);
                break;
            case DownloadStatus.Stopped:
                actions.Add(DownloadActions.Restart);
                actions.Add(DownloadActions.Delete);
                break;
            case DownloadStatus.Restarting:
                actions.Add(DownloadActions.Stop);
                actions.Add(DownloadActions.Delete);
                break;
            case DownloadStatus.Moving:
                actions.Add(DownloadActions.Pause);
                actions.Add(DownloadActions.Stop);
                break;
            case DownloadStatus.Error:
                actions.Add(DownloadActions.Restart);
                actions.Add(DownloadActions.Delete);
                break;
            case DownloadStatus.SourceUnavailable:
                actions.Add(DownloadActions.Delete);
                break;
            case DownloadStatus.DownloadClientError:
            case DownloadStatus.IntegrityError:
                actions.Add(DownloadActions.Restart);
                actions.Add(DownloadActions.Delete);
                break;
            case DownloadStatus.AuthError:
            case DownloadStatus.StorageError:
            case DownloadStatus.MoveError:
                actions.Add(DownloadActions.Start);
                actions.Add(DownloadActions.Restart);
                actions.Add(DownloadActions.Delete);
                break;
            case DownloadStatus.ServerUnreachable:
                actions.Add(DownloadActions.Start);
                actions.Add(DownloadActions.Stop);
                actions.Add(DownloadActions.Delete);
                break;
            default:
                _log.Here().Error("Unknown download status {DownloadStatus}", downloadStatus);
                break;
        }

        return actions;
    }

    /// <summary>
    /// Determines the overall <see cref="DownloadStatus"/> based on the child list of <see cref="DownloadStatus"/>
    /// </summary>
    /// <param name="downloadStatusList">The DownloadStatus list to aggregate from.</param>
    /// <returns>The aggregated <see cref="DownloadStatus"/>.</returns>
    public static DownloadStatus Aggregate(List<DownloadStatus> downloadStatusList)
    {
        if (downloadStatusList.Count == 0)
        {
            _log.Here()
                .Warning(
                    "{NameOfDownloadStatusList} was empty, cannot determine the aggregate status of the download tasks",
                    nameof(downloadStatusList)
                );
            return DownloadStatus.Unknown;
        }

        var firstStatus = downloadStatusList[0];
        var isAllSameStatus = true;
        ulong statusMask = 0;

        foreach (var status in downloadStatusList)
        {
            if (status != firstStatus)
                isAllSameStatus = false;

            statusMask |= 1UL << (int)status;
        }

        if (isAllSameStatus)
            return firstStatus;

        // If any of these statuses are present, return that status.
        // Earlier statuses take precedence.
        foreach (var status in _anyStatuses)
        {
            if ((statusMask & (1UL << (int)status)) != 0)
                return status;
        }

        _log.Here()
            .Error("Unable to determine the aggregate status of the download tasks. {StatusList}", downloadStatusList);

        return DownloadStatus.Unknown;
    }
}
