namespace Reaparr.Application;

public record SetupSonarrIndexerCommand : ICommand<Result<SetupSonarrIndexerCommandResult>>
{
    public required int DownloadClientId { get; init; }
    public Guid IntegrationId { get; init; }
}

public record SetupSonarrIndexerCommandResult
{
    public int IndexerId { get; init; }
    public required SonarrIndexerContractDTO Resource { get; init; }
}

public class SetupSonarrIndexerCommandHandler
    : ICommandHandler<SetupSonarrIndexerCommand, Result<SetupSonarrIndexerCommandResult>>
{
    private readonly ILogger _log;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IReaparrDbContext _dbContext;
    private readonly INetworkSettings _networkSettings;

    private readonly string _indexerName = "Reaparr";

    public SetupSonarrIndexerCommandHandler(
        ILogger log,
        ICommandExecutor commandExecutor,
        IReaparrDbContext dbContext,
        INetworkSettings networkSettings
    )
    {
        _log = log.ForContext<SetupSonarrIndexerCommandHandler>();
        _commandExecutor = commandExecutor;
        _dbContext = dbContext;
        _networkSettings = networkSettings;
    }

    public async Task<Result<SetupSonarrIndexerCommandResult>> ExecuteAsync(
        SetupSonarrIndexerCommand command,
        CancellationToken ct
    )
    {
        var integration =
            command.IntegrationId == Guid.Empty
                ? null
                : await _dbContext
                    .SonarrIntegrations.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == command.IntegrationId, ct);
        if (integration is null)
            return Result.Fail("The Sonarr integration was not found").LogError();

        _log.Here().Information("Setting up Sonarr indexer '{IndexerName}'...", _indexerName);

        var getResult = await _commandExecutor.Send(new SonarrApiGetIndexersCommand(integration.Id), ct);
        if (getResult.IsFailed)
            return Result
                .Fail("Failed to retrieve existing indexers from Sonarr")
                .WithErrors(getResult.Errors)
                .LogError();

        var existing =
            getResult.Value.FirstOrDefault(d => d.Id == integration.ExternalIndexerId)
            ?? getResult.Value.FirstOrDefault(d =>
                string.Equals(d.Name, _indexerName, StringComparison.OrdinalIgnoreCase)
            );

        if (existing is not null)
        {
            _log.Here().Information("Indexer '{IndexerName}' already exists in Sonarr. Updating...", _indexerName);
            var resource = BuildIndexerResource(command.DownloadClientId, existing.Id, integration);
            var updateResult = await _commandExecutor.Send(
                new SonarrApiUpdateIndexerCommand
                {
                    IntegrationId = integration.Id,
                    Id = existing.Id,
                    ForceSave = true,
                    Resource = resource,
                },
                ct
            );

            if (updateResult.IsFailed)
                return updateResult.LogError();

            _log.Here().Information("Successfully updated indexer '{IndexerName}' in Sonarr", _indexerName);
            return Result.Ok(
                new SetupSonarrIndexerCommandResult { IndexerId = updateResult.Value.Id, Resource = resource }
            );
        }

        _log.Here().Information("Creating new indexer '{IndexerName}' in Sonarr...", _indexerName);
        var createResource = BuildIndexerResource(command.DownloadClientId, null, integration);
        var createResult = await _commandExecutor.Send(
            new SonarrApiCreateIndexerCommand
            {
                IntegrationId = integration.Id,
                ForceSave = true,
                Resource = createResource,
            },
            ct
        );

        if (createResult.IsFailed)
            return createResult.LogError();

        createResource.Id = createResult.Value.Id;
        _log.Here().Information("Successfully created indexer '{IndexerName}' in Sonarr", _indexerName);
        return Result.Ok(
            new SetupSonarrIndexerCommandResult { IndexerId = createResult.Value.Id, Resource = createResource }
        );
    }

    private SonarrIndexerContractDTO BuildIndexerResource(int downloadClientId, int? id, SonarrIntegration integration)
    {
        // FORCE this to be a string, and not an implicit URL type by Flurl
        // ReSharper disable once SuggestVarOrType_BuiltInTypes
        string baseUrl = _networkSettings.Url.AppendPathSegment($"api/public/integrations/{integration.Id}/indexer");

        return new SonarrIndexerContractDTO
        {
            Id = id ?? 0,
            Name = _indexerName,
            EnableRss = true,
            EnableAutomaticSearch = true,
            EnableInteractiveSearch = true,
            SupportsRss = true,
            SupportsSearch = true,
            Protocol = "torrent",
            Priority = 25,
            SeasonSearchMaximumSingleEpisodeAge = 0,
            DownloadClientId = downloadClientId,
            Fields =
            [
                new SonarrIndexerContractFieldDTO { Name = "baseUrl", Value = baseUrl },
                new SonarrIndexerContractFieldDTO { Name = "apiPath", Value = "/api" },
                new SonarrIndexerContractFieldDTO { Name = "apiKey", Value = integration.TorznabApiKey },
                new SonarrIndexerContractFieldDTO
                {
                    Name = "categories",
                    Value = IntegrationDefinitions.SupportedTorznabCategories.Select(x => (int)x.Id).ToList(),
                },
                new SonarrIndexerContractFieldDTO { Name = "animeStandardFormatSearch", Value = false },
                new SonarrIndexerContractFieldDTO { Name = "additionalParameters", Value = null },
                new SonarrIndexerContractFieldDTO { Name = "multiLanguages", Value = new List<string>() },
                new SonarrIndexerContractFieldDTO { Name = "failDownloads", Value = new List<string>() },
                new SonarrIndexerContractFieldDTO { Name = "minimumSeeders", Value = 1 },
                new SonarrIndexerContractFieldDTO { Name = "seedCriteria.seedRatio", Value = null },
                new SonarrIndexerContractFieldDTO { Name = "seedCriteria.seedTime", Value = null },
                new SonarrIndexerContractFieldDTO { Name = "seedCriteria.seasonPackSeedTime", Value = null },
                new SonarrIndexerContractFieldDTO
                {
                    Name = "rejectBlocklistedTorrentHashesWhileGrabbing",
                    Value = false,
                },
            ],
            ImplementationName = "Torznab",
            Implementation = "Torznab",
            ConfigContract = "TorznabSettings",
            InfoLink = "https://wiki.servarr.com/sonarr/supported#torznab",
            Tags = [],
        };
    }
}
