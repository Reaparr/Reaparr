namespace Reaparr.Application;

public record SetupRadarrIndexerCommand : ICommand<Result<SetupRadarrIndexerCommandResult>>
{
    public required int DownloadClientId { get; init; }
    public Guid IntegrationId { get; init; }
}

public record SetupRadarrIndexerCommandResult
{
    public int IndexerId { get; init; }
    public required RadarrIndexerContractDTO Resource { get; init; }
}

public class SetupRadarrIndexerCommandHandler
    : ICommandHandler<SetupRadarrIndexerCommand, Result<SetupRadarrIndexerCommandResult>>
{
    private readonly ILogger _log;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IReaparrDbContext _dbContext;
    private readonly INetworkSettings _networkSettings;

    private readonly string _indexerName = "Reaparr";

    public SetupRadarrIndexerCommandHandler(
        ILogger log,
        ICommandExecutor commandExecutor,
        IReaparrDbContext dbContext,
        INetworkSettings networkSettings
    )
    {
        _log = log.ForContext<SetupRadarrIndexerCommandHandler>();
        _commandExecutor = commandExecutor;
        _dbContext = dbContext;
        _networkSettings = networkSettings;
    }

    public async Task<Result<SetupRadarrIndexerCommandResult>> ExecuteAsync(
        SetupRadarrIndexerCommand command,
        CancellationToken ct
    )
    {
        var integration =
            command.IntegrationId == Guid.Empty
                ? null
                : await _dbContext
                    .RadarrIntegrations.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == command.IntegrationId, ct);
        if (integration is null)
            return Result.Fail("The Radarr integration was not found").LogError();

        _log.Here().Information("Setting up Radarr indexer '{IndexerName}'...", _indexerName);

        // Check for existing indexers
        var getResult = await _commandExecutor.Send(new RadarrApiGetIndexersCommand(integration.Id), ct);
        if (getResult.IsFailed)
            return Result
                .Fail("Failed to retrieve existing indexers from Radarr")
                .WithErrors(getResult.Errors)
                .LogError();

        var existing =
            getResult.Value.FirstOrDefault(d => d.Id == integration.ExternalIndexerId)
            ?? getResult.Value.FirstOrDefault(d =>
                string.Equals(d.Name, _indexerName, StringComparison.OrdinalIgnoreCase)
            );

        if (existing is not null)
        {
            _log.Here().Information("Indexer '{IndexerName}' already exists in Radarr. Updating...", _indexerName);
            var updateResource = BuildIndexerResource(command.DownloadClientId, existing.Id, integration);
            var updateResult = await _commandExecutor.Send(
                new RadarrApiUpdateIndexerCommand
                {
                    IntegrationId = integration.Id,
                    Id = existing.Id,
                    ForceSave = true,
                    Resource = updateResource,
                },
                ct
            );

            if (updateResult.IsFailed)
                return updateResult.LogError();

            _log.Here().Information("Successfully updated indexer '{IndexerName}' in Radarr", _indexerName);
            return Result.Ok(
                new SetupRadarrIndexerCommandResult { IndexerId = updateResult.Value.Id, Resource = updateResource }
            );
        }

        // Create a new indexer
        _log.Here().Information("Creating new indexer '{IndexerName}' in Radarr", _indexerName);
        var resource = BuildIndexerResource(command.DownloadClientId, 0, integration);
        var createResult = await _commandExecutor.Send(
            new RadarrApiCreateIndexerCommand
            {
                IntegrationId = integration.Id,
                ForceSave = true,
                Resource = resource,
            },
            ct
        );

        if (createResult.IsFailed)
            return createResult.LogError();

        resource.Id = createResult.Value.Id;
        _log.Here().Information("Successfully created indexer '{IndexerName}' in Radarr", _indexerName);
        return Result.Ok(
            new SetupRadarrIndexerCommandResult { IndexerId = createResult.Value.Id, Resource = resource }
        );
    }

    private RadarrIndexerContractDTO BuildIndexerResource(int downloadClientId, int? id, RadarrIntegration integration)
    {
        // FORCE this to be a string, and not an implicit URL type by Flurl
        // ReSharper disable once SuggestVarOrType_BuiltInTypes
        string baseUrl = _networkSettings.Url.AppendPathSegment($"api/public/integrations/{integration.Id}/indexer");

        return new RadarrIndexerContractDTO
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
            DownloadClientId = downloadClientId,
            Fields =
            [
                new RadarrIndexerContractFieldDTO { Name = "baseUrl", Value = baseUrl },
                new RadarrIndexerContractFieldDTO { Name = "apiPath", Value = "/api" },
                new RadarrIndexerContractFieldDTO { Name = "apiKey", Value = integration.TorznabApiKey },
                new RadarrIndexerContractFieldDTO
                {
                    Name = "categories",
                    Value = IntegrationDefinitions.SupportedTorznabCategories.Select(x => (int)x.Id).ToList(),
                },
                new RadarrIndexerContractFieldDTO { Name = "minimumSeeders", Value = 1 },
                new RadarrIndexerContractFieldDTO { Name = "seedCriteria.seedRatio", Value = null },
                new RadarrIndexerContractFieldDTO { Name = "seedCriteria.seedTime", Value = null },
            ],
            ImplementationName = "Torznab",
            Implementation = "Torznab",
            ConfigContract = "TorznabSettings",
            InfoLink = "https://wiki.servarr.com/radarr/supported#torznab",
            Tags = [],
        };
    }
}
