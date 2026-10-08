namespace Reaparr.Application;

public class ClearCompletedDownloadTasksByDownloadTaskKeyCommandValidator
    : Validator<ClearCompletedDownloadTasksByDownloadTaskKeyCommand>
{
    public ClearCompletedDownloadTasksByDownloadTaskKeyCommandValidator()
    {
        RuleFor(x => x.DownloadTaskKeys).NotEmpty();
    }
}

public class ClearCompletedDownloadTasksByDownloadTaskKeyCommandHandler
    : ICommandHandler<ClearCompletedDownloadTasksByDownloadTaskKeyCommand, Result<int>>
{
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;

    public ClearCompletedDownloadTasksByDownloadTaskKeyCommandHandler(
        IReaparrDbContext dbContext,
        ICommandExecutor commandExecutor
    )
    {
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
    }

    public async Task<Result<int>> ExecuteAsync(
        ClearCompletedDownloadTasksByDownloadTaskKeyCommand request,
        CancellationToken ct
    )
    {
        var completedKeys = await _dbContext.GetDownloadTaskKeysByStatusAsync(
            request.DownloadTaskKeys,
            [DownloadStatus.Completed],
            ct
        );

        if (completedKeys.Count == 0)
            return Result.Ok(0);

        var deleteResult = await _commandExecutor.Send(new DeleteDownloadTasksByKeyCommand(completedKeys), ct);

        return deleteResult.IsFailed ? deleteResult.LogIfFailed() : Result.Ok(completedKeys.Count);
    }
}
