namespace Reaparr.Application;

public class MoveDownloadFileJobQueue : IMoveDownloadFileQueue
{
    private readonly ILogger _log;
    private readonly IReaparrDbContextFactory _dbContextFactory;
    private readonly IMoveDownloadFileScheduler _moveDownloadFileScheduler;

    public MoveDownloadFileJobQueue(
        ILogger log,
        IReaparrDbContextFactory dbContextFactory,
        IMoveDownloadFileScheduler moveDownloadFileScheduler
    )
    {
        _log = log.ForContext<MoveDownloadFileJobQueue>();
        _dbContextFactory = dbContextFactory;
        _moveDownloadFileScheduler = moveDownloadFileScheduler;
    }

    /// <inheritdoc/>
    public Task<Result> CheckMoveDownloadFileJobQueue() => CheckMoveDownloadFileJobQueue(CancellationToken.None);

    /// <inheritdoc/>
    public async Task<Result> CheckMoveDownloadFileJobQueue(CancellationToken cancellationToken)
    {
        // Create a new DbContext for this operation to avoid threading issues
        using var dbContext = await _dbContextFactory.CreateAsync();

        var key = await dbContext.GetNextMoveDownloadTaskKeyAsync(cancellationToken);

        if (key is null)
        {
            _log.Here().Debug("No DownloadTask with status DownloadFinished or MoveError found, nothing to move");
            return Result.Ok();
        }

        var startResult = await _moveDownloadFileScheduler.StartMoveDownloadFileJob(key, cancellationToken);
        return startResult.IsSuccess ? Result.Ok() : startResult;
    }
}
