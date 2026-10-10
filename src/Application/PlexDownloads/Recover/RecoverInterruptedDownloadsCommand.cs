namespace Reaparr.Application;

/// <summary>
/// Reset download tasks left in active download or move states from a previous run before the scheduler starts.
/// </summary>
public record RecoverInterruptedDownloadsCommand : ICommand<Result>;

public class RecoverInterruptedDownloadsCommandHandler : ICommandHandler<RecoverInterruptedDownloadsCommand, Result>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContextFactory _dbContextFactory;
    private readonly IDownloadTaskUpdateDispatcher _downloadTaskUpdateDispatcher;

    public RecoverInterruptedDownloadsCommandHandler(
        ILogger log,
        IReaparrDbContextFactory dbContextFactory,
        IDownloadTaskUpdateDispatcher downloadTaskUpdateDispatcher
    )
    {
        _log = log.ForContext<RecoverInterruptedDownloadsCommandHandler>();
        _dbContextFactory = dbContextFactory;
        _downloadTaskUpdateDispatcher = downloadTaskUpdateDispatcher;
    }

    public async Task<Result> ExecuteAsync(
        RecoverInterruptedDownloadsCommand command,
        CancellationToken cancellationToken
    )
    {
        using var dbContext = await _dbContextFactory.CreateAsync();

        var fileTasks = await dbContext.GetDownloadTaskFilesByStatusAsync(
            [DownloadStatus.Downloading, DownloadStatus.Moving, DownloadStatus.MoveFinished],
            cancellationToken
        );

        var totalReset = 0;
        foreach (var zombie in fileTasks)
        {
            var resetStatus = zombie.DownloadStatus switch
            {
                DownloadStatus.Moving => DownloadStatus.AutoMovePaused,
                DownloadStatus.MoveFinished => DownloadStatus.Completed,
                _ => DownloadStatus.AutoPaused,
            };
            _log.Here()
                .Warning(
                    "Recovering interrupted download task {DownloadTaskId} ({FullTitle}) on PlexServer {PlexServerId} — was left in {DownloadStatus} across a restart, resetting to {ResetStatus}",
                    zombie.Id,
                    zombie.FullTitle,
                    zombie.PlexServerId,
                    zombie.DownloadStatus,
                    resetStatus
                );
            await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
                zombie.ToKey(),
                resetStatus,
                cancellationToken
            );
            totalReset++;
        }

        if (totalReset > 0)
        {
            _log.Here()
                .Information(
                    "Recovered {Count} interrupted active download or move task(s) from a previous run",
                    totalReset
                );
        }

        return Result.Ok();
    }
}
