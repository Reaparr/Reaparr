namespace Reaparr.Application;

public record SetupRadarrDownloadClientCommand : ICommand<Result<SetupRadarrDownloadClientCommandResult>>
{
    public Guid IntegrationId { get; init; }
}

public record SetupRadarrDownloadClientCommandResult
{
    public int DownloadClientId { get; init; }
    public required RadarrDownloadContractDTO Resource { get; init; }
}

public class SetupRadarrDownloadClientCommandHandler
    : ICommandHandler<SetupRadarrDownloadClientCommand, Result<SetupRadarrDownloadClientCommandResult>>
{
    private readonly ILogger _log;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IReaparrDbContext _dbContext;
    private readonly INetworkSettings _networkSettings;
    private readonly IProgressHubService _progressHubService;

    private const string DOWNLOAD_CLIENT_NAME = "Reaparr DownloadClient";

    private const string PUBLIC_URL_HINT =
        "If Radarr cannot reach Reaparr, set the 'Public URL' in Advanced → Network settings to an address reachable from Radarr (e.g. http://reaparr:5000 in Docker).";

    public SetupRadarrDownloadClientCommandHandler(
        ILogger log,
        ICommandExecutor commandExecutor,
        IReaparrDbContext dbContext,
        INetworkSettings networkSettings,
        IProgressHubService progressHubService
    )
    {
        _log = log.ForContext<SetupRadarrDownloadClientCommandHandler>();
        _commandExecutor = commandExecutor;
        _dbContext = dbContext;
        _networkSettings = networkSettings;
        _progressHubService = progressHubService;
    }

    public async Task<Result<SetupRadarrDownloadClientCommandResult>> ExecuteAsync(
        SetupRadarrDownloadClientCommand command,
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

        var normalizedBaseUrl = integration.BaseUrl.TrimEnd('/');
        if (!normalizedBaseUrl.IsValidHttpUrl())
            return Result.Fail("Radarr BaseUrl is invalid").LogError();
        var radarrBaseUri = new Uri(normalizedBaseUrl, UriKind.Absolute);

        await _progressHubService.SendIntegrationSetupProgressAsync(
            new IntegrationSetupProgressDTO
            {
                IntegrationId = integration.Id,
                Stage = IntegrationSetupProgressStage.Connecting,
                IsRunning = true,
                IsSuccess = false,
            }
        );

        _log.Here()
            .Debug(
                "Setting up Radarr download client. RadarrBaseUrl: {RadarrBaseUrl}, ReaparrBaseUrl: {ReaparrBaseUrl}",
                radarrBaseUri,
                _networkSettings.Url
            );

        try
        {
            var result = await _commandExecutor.Send(new RadarrApiGetDownloadClientsCommand(integration.Id), ct);
            if (result.IsCancelled)
            {
                await _progressHubService.SendIntegrationSetupProgressAsync(
                    new IntegrationSetupProgressDTO
                    {
                        IntegrationId = integration.Id,
                        Stage = IntegrationSetupProgressStage.Connecting,
                        IsRunning = false,
                        IsSuccess = false,
                    }
                );
                return result.ToResult<SetupRadarrDownloadClientCommandResult>().LogWarning();
            }

            if (result.IsFailed)
            {
                await _progressHubService.SendIntegrationSetupProgressAsync(
                    new IntegrationSetupProgressDTO
                    {
                        IntegrationId = integration.Id,
                        Stage = IntegrationSetupProgressStage.Connecting,
                        IsRunning = false,
                        IsSuccess = false,
                    }
                );
                return Result
                    .Fail("Failed to retrieve existing download clients from Radarr")
                    .WithErrors(result.Errors)
                    .LogError();
            }

            await _progressHubService.SendIntegrationSetupProgressAsync(
                new IntegrationSetupProgressDTO
                {
                    IntegrationId = integration.Id,
                    Stage = IntegrationSetupProgressStage.Connecting,
                    IsRunning = false,
                    IsSuccess = true,
                }
            );

            var list = result.Value;
            var resource = BuildDownloadClientResource(_networkSettings.Uri, integration);

            var currentDownloadClient = list.FirstOrDefault(d => d.Id == integration.ExternalDownloadClientId)
                ?? list.FirstOrDefault(d =>
                    string.Equals(d.Name, DOWNLOAD_CLIENT_NAME, StringComparison.OrdinalIgnoreCase)
                );

            if (currentDownloadClient != null)
            {
                resource.Id = currentDownloadClient.Id;
                var updateResult = await _commandExecutor.Send(
                    new RadarrApiUpdateDownloadClientCommand
                    {
                        IntegrationId = integration.Id,
                        Id = currentDownloadClient.Id,
                        ForceSave = true,
                        Resource = resource,
                    },
                    ct
                );

                if (updateResult.IsFailed)
                    return updateResult.WithError(PUBLIC_URL_HINT).LogError();

                return Result.Ok(
                    new SetupRadarrDownloadClientCommandResult
                    {
                        DownloadClientId = updateResult.Value.Id,
                        Resource = resource,
                    }
                );
            }

            var createResult = await _commandExecutor.Send(
                new RadarrApiCreateDownloadClientCommand
                {
                    IntegrationId = integration.Id,
                    ForceSave = true,
                    Resource = resource,
                },
                ct
            );

            if (createResult.IsFailed)
                return createResult.WithError(PUBLIC_URL_HINT).LogError();

            resource.Id = createResult.Value.Id;
            return Result.Ok(
                new SetupRadarrDownloadClientCommandResult
                {
                    DownloadClientId = createResult.Value.Id,
                    Resource = resource,
                }
            );
        }
        catch (TaskCanceledException e)
        {
            _log.Here().Error(e, "Timeout while communicating with Radarr");
            return Result.Fail("Timeout while communicating with Radarr").LogError();
        }
        catch (HttpRequestException e)
        {
            _log.Here().Error(e, "HTTP error while communicating with Radarr");
            return Result.Fail("HTTP error while communicating with Radarr").LogError();
        }
    }

    private RadarrDownloadContractDTO BuildDownloadClientResource(Uri reaparrBaseUri, RadarrIntegration integration)
    {
        var useSsl =
            Uri.TryCreate(_networkSettings.ReverseProxyUrl, UriKind.Absolute, out var reverseProxyUri)
            && string.Equals(reverseProxyUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        string urlBase = _networkSettings.BasePath.AppendPathSegment(
            $"api/public/integrations/{integration.Id}/download-client"
        );
        return new RadarrDownloadContractDTO
        {
            Enable = true,
            Protocol = "torrent",
            Priority = 1,
            RemoveCompletedDownloads = true,
            RemoveFailedDownloads = true,
            Name = DOWNLOAD_CLIENT_NAME,
            Fields =
            [
                new() { Name = "host", Value = reaparrBaseUri.Host },
                new() { Name = "port", Value = reaparrBaseUri.Port },
                new() { Name = "useSsl", Value = useSsl },
                new() { Name = "urlBase", Value = urlBase },
                new() { Name = "apiKey", Value = integration.QBittorrentApiKey },
                new() { Name = "movieCategory", Value = integration.Category },
                new() { Name = "recentMoviePriority", Value = 0 },
                new() { Name = "olderMoviePriority", Value = 0 },
                new() { Name = "initialState", Value = 0 },
                new() { Name = "sequentialOrder", Value = false },
                new() { Name = "firstAndLast", Value = false },
                new() { Name = "contentLayout", Value = 0 },
            ],
            ImplementationName = "qBittorrent",
            Implementation = "QBittorrent",
            ConfigContract = "QBittorrentSettings",
            InfoLink = "https://wiki.servarr.com/radarr/supported#qbittorrent",
            Tags = [],
        };
    }
}
