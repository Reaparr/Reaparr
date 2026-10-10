namespace Reaparr.PlexApi;

public class GetLibrarySectionsCommandHandler : ICommandHandler<GetLibrarySectionsCommand, Result<List<PlexLibrary>>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly IPlexApiClientFactory _plexApiClientFactory;

    public GetLibrarySectionsCommandHandler(
        ILogger log,
        IReaparrDbContext dbContext,
        IPlexApiClientFactory plexApiClientFactory
    )
    {
        _log = log.ForContext<GetLibrarySectionsCommandHandler>();
        _dbContext = dbContext;
        _plexApiClientFactory = plexApiClientFactory;
    }

    public async Task<Result<List<PlexLibrary>>> ExecuteAsync(GetLibrarySectionsCommand command, CancellationToken ct)
    {
        var plexServerId = command.PlexServerId;
        var plexAccountId = command.PlexAccountId;

        var tokenResult = await _dbContext.GetPlexServerTokenAsync(plexServerId, plexAccountId, ct);
        if (tokenResult.IsFailed)
            return tokenResult.ToResult();

        var plexServerConnectionResult = await _dbContext.ChoosePlexServerConnection(plexServerId, ct);
        if (plexServerConnectionResult.IsFailed)
            return plexServerConnectionResult.ToResult();

        var connection = plexServerConnectionResult.Value;

        var plexServer = connection.PlexServer;
        if (plexServer is null)
            return ResultExtensions.EntityNotFound(nameof(PlexServer), plexServerId);

        var client = _plexApiClientFactory.CreateClient(
            tokenResult.Value,
            new PlexApiClientOptions { ConnectionUrl = connection.Url }
        );

        var response = await client.Library.GetSectionsAsync().ToResponse(ct);
        if (response.IsFailed)
            return response.ToResult();

        if (response.Value.Object?.MediaContainer?.Directory is null)
        {
            _log.Here()
                .Error(
                    "Plex server: {PlexServerName} returned an empty response when libraries were requested",
                    connection.PlexServer?.Name
                );
            return Result.Fail("Plex returned no library section collection").LogError();
        }

        var directories = response.Value.Object.MediaContainer.Directory;

        var mappedLibraries = directories
            .Select(x => new PlexLibrary
            {
                Id = 0,
                Type = x.Type.ToPlexMediaType(x.Agent, x.Scanner),
                Title = x.Title!,
                Key = x.Key!,
                CreatedAt = DateTimeExtensions.FromUnixTime(x.CreatedAt),
                UpdatedAt = DateTimeExtensions.FromUnixTime(x.UpdatedAt),
                ScannedAt = DateTimeExtensions.FromUnixTime(x.ScannedAt),
                ContentChangedAt = x.ContentChangedAt ?? 0,
                SyncedAt = null,
                Outdated = false,
                Uuid = x.Uuid,
                PlexServer = null,
                PlexServerId = plexServerId,
                DefaultDestination = null,
                DefaultDestinationId = null,
                Language = x.Language,
            })
            .ToList();

        return Result.Ok(mappedLibraries);
    }
}
