namespace Reaparr.Application;

public record ResetFailedLibrarySyncJobsCommand(int PlexServerId) : ICommand<Result>;

public class ResetFailedLibrarySyncJobsCommandValidator : AbstractValidator<ResetFailedLibrarySyncJobsCommand>
{
    public ResetFailedLibrarySyncJobsCommandValidator()
    {
        RuleFor(x => x.PlexServerId).GreaterThan(0);
    }
}

public class ResetFailedLibrarySyncJobsCommandHandler : ICommandHandler<ResetFailedLibrarySyncJobsCommand, Result>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;
    private readonly INotificationHubService _notificationHubService;

    public ResetFailedLibrarySyncJobsCommandHandler(
        ILogger log,
        IReaparrDbContext dbContext,
        ICommandExecutor commandExecutor,
        INotificationHubService notificationHubService
    )
    {
        _log = log.ForContext<ResetFailedLibrarySyncJobsCommandHandler>();
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
        _notificationHubService = notificationHubService;
    }

    public async Task<Result> ExecuteAsync(
        ResetFailedLibrarySyncJobsCommand command,
        CancellationToken cancellationToken
    )
    {
        var plexServerName = await _dbContext.GetPlexServerNameById(command.PlexServerId);

        // Reset failed library sync jobs for this server to queue
        var resetCount = await _dbContext
            .LibrarySyncJobQueues.Where(x =>
                x.PlexServerId == command.PlexServerId
                && x.Status == LibrarySyncJobStatus.Failed
                && x.PlexLibrary!.IsEnabled
            )
            .ResetJobsToQueuedAsync(cancellationToken);

        var hasQueuedJobs = await _dbContext.LibrarySyncJobQueues.AnyAsync(
            x =>
                x.PlexServerId == command.PlexServerId
                && x.Status == LibrarySyncJobStatus.Queued
                && x.PlexLibrary!.IsEnabled,
            cancellationToken
        );
        if (resetCount > 0)
        {
            _log.Here()
                .Information(
                    "Reset {Count} failed library sync jobs to queued for server {PlexServerName}",
                    resetCount,
                    plexServerName
                );

            var notificationResult = await Result.Try(async Task () =>
                await _notificationHubService.SendRefreshNotificationAsync([RefreshDataType.PlexLibrarySyncStatus])
            );
            notificationResult.LogIfFailed();
        }

        if (resetCount > 0 || hasQueuedJobs)
        {
            var checkQueuedResult = await _commandExecutor.Send(
                new CheckQueuedPlexLibraryToSyncCommand(),
                cancellationToken
            );
            checkQueuedResult.LogIfFailed();
        }
        return Result.Ok();
    }
}
