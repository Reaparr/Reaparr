namespace Reaparr.Application;

/// <summary>
/// Validates enabled Plex accounts and refreshes their accessible Plex servers and libraries.
/// Pass a Plex account id to refresh only that account, or zero to refresh all enabled accounts.
/// </summary>
public record RefreshPlexAccountAccessCommand(int PlexAccountId = 0)
    : ICommand<Result<List<RefreshPlexAccountAccessRapportDTO>>>;

public class RefreshPlexAccountAccessCommandValidator : AbstractValidator<RefreshPlexAccountAccessCommand>
{
    public RefreshPlexAccountAccessCommandValidator()
    {
        RuleFor(x => x.PlexAccountId).GreaterThanOrEqualTo(0);
    }
}

public class RefreshPlexAccountAccessCommandHandler
    : ICommandHandler<RefreshPlexAccountAccessCommand, Result<List<RefreshPlexAccountAccessRapportDTO>>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;
    private readonly INotificationHubService _notificationHubService;

    public RefreshPlexAccountAccessCommandHandler(
        ILogger log,
        IReaparrDbContext dbContext,
        ICommandExecutor commandExecutor,
        INotificationHubService notificationHubService
    )
    {
        _log = log.ForContext<RefreshPlexAccountAccessCommandHandler>();
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
        _notificationHubService = notificationHubService;
    }

    public async Task<Result<List<RefreshPlexAccountAccessRapportDTO>>> ExecuteAsync(
        RefreshPlexAccountAccessCommand command,
        CancellationToken cancellationToken
    )
    {
        var plexAccountsQuery = _dbContext.PlexAccounts.AsTracking().AsQueryable();
        plexAccountsQuery =
            command.PlexAccountId > 0
                ? plexAccountsQuery.Where(x => x.Id == command.PlexAccountId)
                : plexAccountsQuery.Where(x => x.IsEnabled);

        var plexAccounts = await plexAccountsQuery.ToListAsync(cancellationToken);
        if (!plexAccounts.Any())
        {
            _log.Here().Warning("No enabled Plex accounts found to refresh Plex access");
            return Result.Ok(new List<RefreshPlexAccountAccessRapportDTO>());
        }

        var rapports = new List<RefreshPlexAccountAccessRapportDTO>();
        foreach (var plexAccount in plexAccounts)
        {
            var validationResult = await _commandExecutor.Send(
                new ValidatePlexTokenCommand(plexAccount.GetAuthToken),
                cancellationToken
            );

            if (validationResult.IsCancelled)
                return validationResult.ToResult();

            if (validationResult.HasPlex401UnauthorizedError())
            {
                plexAccount.IsValidated = false;
                plexAccount.ValidatedAt = null;
                await _dbContext.SaveChangesAsync(cancellationToken);
                _log.Here()
                    .Warning(
                        "Plex account {PlexAccountName} has an invalid token; marking it invalid and revoking access",
                        plexAccount.DisplayName
                    );

                var revokeResult = await RevokeAllAccess(plexAccount, cancellationToken);
                if (revokeResult.IsCancelled)
                    return revokeResult.ToResult();
                if (revokeResult.IsFailed)
                {
                    revokeResult.LogError();
                    continue;
                }

                rapports.Add(revokeResult.Value);
                continue;
            }

            if (validationResult.IsFailed)
            {
                validationResult.LogError();
                continue;
            }

            plexAccount.IsValidated = true;
            plexAccount.ValidatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            var serverAccessResult = await _commandExecutor.Send(
                new RefreshPlexServerAccessCommand(plexAccount.Id),
                cancellationToken
            );

            if (serverAccessResult.IsCancelled)
                return serverAccessResult.ToResult();

            if (serverAccessResult.IsFailed)
            {
                serverAccessResult.LogError();
                continue;
            }

            var serverAccessRapport = serverAccessResult.Value;
            var lostServerIds = serverAccessRapport
                .Access.Where(x => x.State == PlexAccessState.Revoked)
                .Select(x => x.PlexServerId)
                .ToList();

            if (lostServerIds.Count > 0)
            {
                await _dbContext
                    .PlexAccountLibraries.Where(x =>
                        x.PlexAccountId == plexAccount.Id && lostServerIds.Contains(x.PlexServerId)
                    )
                    .ExecuteDeleteAsync(cancellationToken);
            }

            var libraryAccessResult = await _commandExecutor.Send(
                new RefreshLibraryAccessCommand(plexAccount.Id),
                cancellationToken
            );

            if (libraryAccessResult.IsCancelled)
                return libraryAccessResult.ToResult();

            if (libraryAccessResult.IsFailed)
            {
                libraryAccessResult.LogError();
                continue;
            }

            rapports.Add(ToDTO(serverAccessRapport, libraryAccessResult.Value));
        }

        await _notificationHubService.SendRefreshNotificationAsync([
            RefreshDataType.PlexAccount,
            RefreshDataType.PlexServer,
            RefreshDataType.PlexServerConnection,
            RefreshDataType.PlexLibrary,
        ]);

        var rebuildResult = await _commandExecutor.Send(new QueueMediaOverviewRebuildCommand(), cancellationToken);
        rebuildResult.LogIfFailed();

        return Result.Ok(rapports);
    }

    private async Task<Result<RefreshPlexAccountAccessRapportDTO>> RevokeAllAccess(
        PlexAccount plexAccount,
        CancellationToken cancellationToken
    )
    {
        var plexServers = await _dbContext
            .PlexAccountServers.Include(x => x.PlexServer)
            .Where(x => x.PlexAccountId == plexAccount.Id)
            .Select(x => new { x.PlexServerId, x.PlexServer!.Name })
            .ToListAsync(cancellationToken);

        var serverAccessRapport = new RefreshPlexServerAccessRapport(plexAccount.Id, plexAccount.DisplayName);
        serverAccessRapport.Access.AddRange(
            plexServers.Select(x => new RefreshPlexServerAccessRapportRow(
                PlexAccessState.Revoked,
                x.PlexServerId,
                x.Name
            ))
        );

        var libraryAccess = await BuildRevokedLibraryAccessResponse(
            plexAccount,
            serverAccessRapport,
            cancellationToken
        );

        var transactionResult = await _dbContext.ExecuteTransactionAsync(
            async (ctx, txCt) =>
            {
                await ctx.BulkDeleteByIdsAsync(
                    libraryAccess.LostServerAccess,
                    (db, lostServerIds) =>
                        db.PlexAccountLibraries.Where(x =>
                            x.PlexAccountId == plexAccount.Id && lostServerIds.Contains(x.PlexServerId)
                        ),
                    txCt
                );
                await ctx.PlexAccountServers.Where(x => x.PlexAccountId == plexAccount.Id).ExecuteDeleteAsync(txCt);
            },
            cancellationToken
        );
        if (transactionResult.IsFailed)
            return Result.Fail(transactionResult.Errors);

        return Result.Ok(ToDTO(serverAccessRapport, libraryAccess.Response));
    }

    private async Task<(
        PlexLibraryAccessRefreshResponse Response,
        List<int> AffectedLibraryIds,
        List<int> LostServerAccess
    )> BuildRevokedLibraryAccessResponse(
        PlexAccount plexAccount,
        RefreshPlexServerAccessRapport serverAccessRapport,
        CancellationToken cancellationToken
    )
    {
        var lostServerAccess = serverAccessRapport
            .Access.Where(x => x.State == PlexAccessState.Revoked)
            .Select(x => x.PlexServerId)
            .ToList();

        var lostLibraryAccess = await _dbContext
            .PlexAccountLibraries.IgnoreQueryFilters()
            .Include(x => x.PlexLibrary)
            .Include(x => x.PlexServer)
            .Where(x => x.PlexAccountId == plexAccount.Id && lostServerAccess.Contains(x.PlexServerId))
            .ToListAsync(cancellationToken);

        var response = new PlexLibraryAccessRefreshResponse
        {
            Reports = lostLibraryAccess
                .Select(x =>
                    new PlexLibraryAccessRapport(
                        plexAccount.DisplayName,
                        x.PlexServerId,
                        x.PlexServer!.Name
                    ).AddRevoked(x.PlexLibraryId, x.PlexLibrary!.Name)
                )
                .ToList(),
            OfflineServers = [],
        };

        var affectedLibraryIds = lostLibraryAccess.Select(x => x.PlexLibraryId).Distinct().ToList();
        return (response, affectedLibraryIds, lostServerAccess);
    }

    private static RefreshPlexAccountAccessRapportDTO ToDTO(
        RefreshPlexServerAccessRapport serverAccessRapport,
        PlexLibraryAccessRefreshResponse libraryAccessRapport
    ) =>
        new(serverAccessRapport.PlexAccountId, serverAccessRapport.PlexAccountName)
        {
            Access = serverAccessRapport
                .Access.Select(x => new PlexServerAccessRapportDTO
                {
                    State = x.State,
                    PlexServerId = x.PlexServerId,
                    PlexServerName = x.PlexServerName,
                    IsServerOffline = libraryAccessRapport.OfflineServers.Contains(x.PlexServerId),
                    LibraryAccess = libraryAccessRapport
                        .Reports.Where(y => y.PlexServerId == x.PlexServerId)
                        .SelectMany(y => y.Data)
                        .Select(y => new PlexLibraryAccessRapportDTO
                        {
                            PlexLibraryName = y.PlexLibraryName,
                            PlexServerId = y.PlexServerId,
                            State = y.State,
                            PlexLibraryId = y.PlexLibraryId,
                        })
                        .ToList(),
                })
                .ToList(),
        };
}
