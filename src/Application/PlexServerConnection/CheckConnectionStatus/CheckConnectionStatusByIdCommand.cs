namespace Reaparr.Application;

public record CheckConnectionStatusByIdCommand(int PlexServerConnectionId, int Timeout = 10)
    : ICommand<Result<PlexServerStatus>>;

public class CheckConnectionStatusByIdCommandValidator : AbstractValidator<CheckConnectionStatusByIdCommand>
{
    public CheckConnectionStatusByIdCommandValidator()
    {
        RuleFor(x => x.PlexServerConnectionId).GreaterThan(0);
        RuleFor(x => x.Timeout).GreaterThan(0);
    }
}

public class CheckConnectionStatusByIdCommandHandler
    : ICommandHandler<CheckConnectionStatusByIdCommand, Result<PlexServerStatus>>
{
    private readonly IProgressHubService _progressHubService;
    private readonly ICommandExecutor _commandDispatcher;
    private readonly IReaparrDbContextFactory _dbContextFactory;
    private readonly ILogger _log;
    private PlexServerConnection? _plexServerConnection;

    public CheckConnectionStatusByIdCommandHandler(
        IReaparrDbContextFactory dbContextFactory,
        IProgressHubService progressHubService,
        ICommandExecutor commandDispatcher,
        ILogger log
    )
    {
        _dbContextFactory = dbContextFactory;
        _progressHubService = progressHubService;
        _commandDispatcher = commandDispatcher;
        _log = log.ForContext<CheckConnectionStatusByIdCommandHandler>();
    }

    public async Task<Result<PlexServerStatus>> ExecuteAsync(
        CheckConnectionStatusByIdCommand command,
        CancellationToken cancellationToken
    )
    {
        // Create a dedicated DbContext for this handler to support parallel execution
        using var dbContext = await _dbContextFactory.CreateAsync();

        var plexServerConnection = await dbContext.PlexServerConnections.GetAsync(
            command.PlexServerConnectionId,
            cancellationToken
        );

        if (plexServerConnection is null)
        {
            return ResultExtensions
                .EntityNotFound(nameof(PlexServerConnection), command.PlexServerConnectionId)
                .LogError();
        }

        _plexServerConnection = plexServerConnection;

        // Request status
        var serverStatusResult = await _commandDispatcher.Send(
            new GetServerStatusCommand
            {
                PlexServerConnectionId = command.PlexServerConnectionId,
                Timeout = command.Timeout,
                ProgressAction = progress =>
                {
                    if (_plexServerConnection is not null)
                    {
                        _ = Task.Run(
                            async () =>
                            {
                                try
                                {
                                    var checkStatusProgress = progress.ToServerConnectionCheckStatusProgress(
                                        _plexServerConnection
                                    );
                                    await _progressHubService.SendServerConnectionCheckStatusProgressAsync(
                                        checkStatusProgress
                                    );
                                }
                                catch (Exception ex)
                                {
                                    _log.Here()
                                        .ErrorResult(
                                            ex,
                                            "Error sending server connection check status progress update"
                                        );
                                }
                            },
                            CancellationToken.None
                        );
                    }
                },
            },
            cancellationToken
        );

        if (serverStatusResult.IsFailed)
            return serverStatusResult.LogIfFailed();

        // Store the latest Plex server status for this connection.
        var plexServerStatus = serverStatusResult.Value;

        var relationExists = await dbContext.PlexServerConnections.AnyAsync(
            x => x.Id == plexServerStatus.PlexServerConnectionId && x.PlexServerId == plexServerStatus.PlexServerId,
            cancellationToken
        );

        if (!relationExists)
        {
            return ResultExtensions
                .EntityNotFound(nameof(PlexServerConnection), plexServerStatus.PlexServerConnectionId)
                .LogWarning();
        }

        var upsertResult = await dbContext.ExecuteTransactionAsync(
            async (transactionContext, ct) =>
            {
                var existingCount = await transactionContext
                    .PlexServerStatuses.Where(x => x.PlexServerConnectionId == plexServerStatus.PlexServerConnectionId)
                    .ExecuteUpdateAsync(
                        setters =>
                            setters
                                .SetProperty(x => x.IsSuccessful, plexServerStatus.IsSuccessful)
                                .SetProperty(x => x.StatusCode, plexServerStatus.StatusCode)
                                .SetProperty(x => x.StatusMessage, plexServerStatus.StatusMessage)
                                .SetProperty(x => x.LastChecked, plexServerStatus.LastChecked)
                                .SetProperty(x => x.PlexServerId, plexServerStatus.PlexServerId),
                        ct
                    );

                if (existingCount == 0)
                {
                    transactionContext.PlexServerStatuses.Add(plexServerStatus);
                    await transactionContext.SaveChangesAsync(ct);
                }

                return plexServerStatus;
            },
            cancellationToken
        );

        upsertResult.LogIfFailed();

        return upsertResult.Value;
    }
}
