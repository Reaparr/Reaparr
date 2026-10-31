using System.Linq.Expressions;
using System.Net.Http.Json;
using System.Text.Json;

namespace Reaparr.IntegrationTests;

public class UrlValidationIntegrationTests : BaseIntegrationTests
{
    [Test]
    [Arguments(UrlTarget.Radarr, SaveOperation.Create)]
    [Arguments(UrlTarget.Radarr, SaveOperation.Update)]
    [Arguments(UrlTarget.Sonarr, SaveOperation.Create)]
    [Arguments(UrlTarget.Sonarr, SaveOperation.Update)]
    [Arguments(UrlTarget.Plex, SaveOperation.Create)]
    [Arguments(UrlTarget.Plex, SaveOperation.Update)]
    public async Task ShouldRejectInvalidUrlWithoutChangingStoredRecords(UrlTarget target, SaveOperation operation)
    {
        // Arrange
        using var container = await CreateUrlContainerAsync(target, operation);
        if (target == UrlTarget.Plex && operation == SaveOperation.Update)
            await AddCustomConnectionAsync(container);
        var before = await ReadSnapshotsAsync(container, target);
        using var request = BuildInvalidRequest(target, operation, before);
        using var client = container.GetApiClient();
        await client.SignIn();

        // Act
        using var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var result = document.RootElement;
        result.GetProperty("isSuccess").GetBoolean().ShouldBeFalse();
        result.GetProperty("errors").GetArrayLength().ShouldBe(1);
        result
            .GetProperty("errors")[0]
            .GetProperty("metadata")
            .GetProperty("url")
            .GetString()
            .ShouldBe(
                target == UrlTarget.Plex ? "string is not a valid URL" : "URL must be an absolute http/https URL."
            );
        result.GetProperty("successes").GetArrayLength().ShouldBe(0);
        result.TryGetProperty("value", out _).ShouldBeFalse();
        (await ReadSnapshotsAsync(container, target)).ShouldBe(before);
    }

    [Test]
    [Arguments(UrlTarget.Radarr, SaveOperation.Create)]
    [Arguments(UrlTarget.Radarr, SaveOperation.Update)]
    [Arguments(UrlTarget.Sonarr, SaveOperation.Create)]
    [Arguments(UrlTarget.Sonarr, SaveOperation.Update)]
    public async Task ShouldSaveValidArrUrlOfflineWithNormalization(UrlTarget target, SaveOperation operation)
    {
        // Arrange
        var scenario = _arrScenarios[(target, operation)];
        using var container = await CreateUrlContainerAsync(target, operation);
        var before = await ReadArrSnapshotsAsync(container, target);
        var original = before.Single();
        using var request = BuildArrRequest(scenario, scenario.Valid, original.Id);
        using var client = container.GetApiClient();
        await client.SignIn();

        // Act
        using var response = await client.SendAsync(request);

        // Assert
        var expectedStatus = operation == SaveOperation.Create ? HttpStatusCode.Created : HttpStatusCode.OK;
        response.StatusCode.ShouldBe(expectedStatus);
        var result = await response.Content.ReadFromJsonAsync<ResultDTO<JsonDocument>>();
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.StatusCode.ShouldBe((int)expectedStatus);
        result.Errors.Count.ShouldBe(0);
        result.Successes.Count.ShouldBe(operation == SaveOperation.Create ? 1 : 0);
        result.Value.ShouldNotBeNull();
        using var document = result.Value!;
        var value = document.RootElement;
        var id = value.GetProperty("id").GetGuid();
        var after = await ReadArrSnapshotsAsync(container, target);
        var saved = after.Single(x => x.Id == id);
        var expected = CreateExpectedArrSnapshot(scenario, original, saved);
        value.GetProperty("name").GetString().ShouldBe(scenario.Valid.Name);
        value.GetProperty("url").GetString().ShouldBe(scenario.ExpectedUrl);
        value.GetProperty("apiKey").GetString().ShouldBe(expected.ApiKey);
        value.GetProperty("category").GetString().ShouldBe(expected.Category);
        value.GetProperty("downloadFolderId").GetInt32().ShouldBe(FolderTypeDefaults.DefaultDownloadFolderId);
        value.GetProperty("provisioningState").GetString().ShouldBe(expected.ProvisioningState.ToString());
        var expectedRows =
            operation == SaveOperation.Create
                ? before.Append(expected).OrderBy(x => x.Id).ToArray()
                : before.Select(x => x.Id == original.Id ? expected : x).ToArray();
        after.ShouldBe(expectedRows);
        if (operation == SaveOperation.Create)
        {
            saved.QBittorrentApiKey.ShouldStartWith("qbt_");
            saved.QBittorrentApiKey.Length.ShouldBe(32);
            saved.TorznabApiKey.Length.ShouldBe(32);
            saved.TorznabApiKey.ShouldNotBe(saved.QBittorrentApiKey);
        }
        else
            id.ShouldBe(original.Id);
    }

    [Test]
    [Arguments(SaveOperation.Create)]
    [Arguments(SaveOperation.Update)]
    public async Task ShouldSaveValidPlexUrlOfflineWithNormalization(SaveOperation operation)
    {
        // Arrange
        var scenario = _plexScenarios[operation];
        using var container = await CreateUrlContainerAsync(UrlTarget.Plex, operation);
        if (operation == SaveOperation.Update)
            await AddCustomConnectionAsync(container);
        var before = await ReadPlexSnapshotsAsync(container);
        var id = operation == SaveOperation.Update ? before.Single(x => x.IsCustom).Id : 0;
        using var request = BuildPlexRequest(scenario, scenario.Valid, id);
        using var client = container.GetApiClient();
        await client.SignIn();

        // Act
        using var response = await client.SendAsync(request);

        // Assert
        var expectedStatus = operation == SaveOperation.Create ? HttpStatusCode.Created : HttpStatusCode.OK;
        response.StatusCode.ShouldBe(expectedStatus);
        var result = await response.Deserialize<PlexServerConnectionDTO>();
        result.IsSuccess.ShouldBeTrue();
        result.StatusCode.ShouldBe((int)expectedStatus);
        result.Errors.Count.ShouldBe(0);
        result.Successes.Count.ShouldBe(1);
        result.Value.ShouldNotBeNull();
        var value = result.Value!;
        value.Protocol.ShouldBe("https");
        value.Address.ShouldBe(scenario.Valid.Address);
        value.Port.ShouldBe(scenario.Valid.Port);
        value.Url.ShouldBe(scenario.ExpectedUrl);
        value.Local.ShouldBeTrue();
        value.Relay.ShouldBeFalse();
        value.IPv4.ShouldBeTrue();
        value.IPv6.ShouldBeFalse();
        value.IsCustom.ShouldBeTrue();
        value.PlexServerId.ShouldBe(1);
        value.ChosenConnection.ShouldBeFalse();
        value.IsPlexTvConnection.ShouldBeFalse();
        value.Type.ShouldBe(PlexConnectionTypes.Local);
        value.LatestConnectionStatus.ShouldBeNull();
        (await ReadPlexSnapshotsAsync(container))
            .Single(x => x.Id == value.Id)
            .ShouldBe(
                new PlexConnectionSnapshot(
                    value.Id,
                    "https",
                    scenario.Valid.Address,
                    scenario.Valid.Port,
                    scenario.ExpectedUrl,
                    true,
                    false,
                    true,
                    false,
                    true,
                    1
                )
            );
    }

    private Task<BaseContainer> CreateUrlContainerAsync(UrlTarget target, SaveOperation operation)
    {
        var seed = target == UrlTarget.Plex ? _plexScenarios[operation].Seed : _arrScenarios[(target, operation)].Seed;
        return CreateContainer(
            seed,
            config =>
            {
                config.DatabaseOptions = data =>
                {
                    data.PlexServerCount = target == UrlTarget.Plex ? 1 : 0;
                    data.PlexServerConnectionPerServerCount = 1;
                    data.RadarrIntegrationCount = target == UrlTarget.Radarr ? 1 : 0;
                    data.SonarrIntegrationCount = target == UrlTarget.Sonarr ? 1 : 0;
                };
                // Startup Arr probing is outside these save requests and must not contact a real service.
                config.OverrideServices = builder =>
                    builder.RegisterInstance(new SaveEndpointCommandExecutor()).As<ICommandExecutor>();
            }
        );
    }

    private static HttpRequestMessage BuildInvalidRequest(
        UrlTarget target,
        SaveOperation operation,
        IReadOnlyList<object> before
    )
    {
        if (target == UrlTarget.Plex)
        {
            var scenario = _plexScenarios[operation];
            var rows = (IReadOnlyList<PlexConnectionSnapshot>)before;
            var id = operation == SaveOperation.Update ? rows.Single(x => x.IsCustom).Id : 0;
            return BuildPlexRequest(scenario, scenario.Invalid, id);
        }

        var arr = _arrScenarios[(target, operation)];
        return BuildArrRequest(arr, arr.Invalid, ((IReadOnlyList<ArrSnapshot>)before).Single().Id);
    }

    private static HttpRequestMessage BuildArrRequest(ArrScenario scenario, ArrInput input, Guid id)
    {
        object body = (scenario.Target, scenario.Operation) switch
        {
            (UrlTarget.Radarr, SaveOperation.Create) => new CreateRadarrIntegrationRequest
            {
                Name = input.Name,
                Url = input.Url,
                ApiKey = input.ApiKey,
                Category = input.Category,
                DownloadFolderId = FolderTypeDefaults.DefaultDownloadFolderId,
            },
            (UrlTarget.Sonarr, SaveOperation.Create) => new CreateSonarrIntegrationRequest
            {
                Name = input.Name,
                Url = input.Url,
                ApiKey = input.ApiKey,
                Category = input.Category,
                DownloadFolderId = FolderTypeDefaults.DefaultDownloadFolderId,
            },
            (UrlTarget.Radarr, SaveOperation.Update) => new UpdateRadarrIntegrationRequest
            {
                IntegrationId = id,
                Name = input.Name,
                Url = input.Url,
                ApiKey = input.ApiKey,
                Category = input.Category,
                DownloadFolderId = FolderTypeDefaults.DefaultDownloadFolderId,
            },
            (UrlTarget.Sonarr, SaveOperation.Update) => new UpdateSonarrIntegrationRequest
            {
                IntegrationId = id,
                Name = input.Name,
                Url = input.Url,
                ApiKey = input.ApiKey,
                Category = input.Category,
                DownloadFolderId = FolderTypeDefaults.DefaultDownloadFolderId,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
        return CreateArrHttpRequest(scenario, body, id);
    }

    private static HttpRequestMessage CreateArrHttpRequest(ArrScenario scenario, object body, Guid id)
    {
        var route =
            scenario.Operation == SaveOperation.Create
                ? $"{ApiRoutes.IntegrationController}/{scenario.Target}/Configure"
                : $"{ApiRoutes.IntegrationController}/{scenario.Target}/{id}/Configure";
        return new HttpRequestMessage(
            scenario.Operation == SaveOperation.Create ? HttpMethod.Post : HttpMethod.Put,
            route
        )
        {
            Content = JsonContent.Create(body),
        };
    }

    private static HttpRequestMessage BuildPlexRequest(PlexScenario scenario, PlexInput input, int id)
    {
        object body =
            scenario.Operation == SaveOperation.Create
                ? new CreatePlexServerConnectionEndpointRequest
                {
                    Url = input.Url,
                    Protocol = input.Protocol,
                    Address = input.Address,
                    Port = input.Port,
                    PlexServerId = 1,
                }
                : new UpdatePlexServerConnectionEndpointRequest
                {
                    Id = id,
                    Url = input.Url,
                    Protocol = input.Protocol,
                    Address = input.Address,
                    Port = input.Port,
                    PlexServerId = 1,
                };
        return new HttpRequestMessage(
            scenario.Operation == SaveOperation.Create ? HttpMethod.Post : HttpMethod.Patch,
            ApiRoutes.PlexServerConnectionController
        )
        {
            Content = JsonContent.Create(body),
        };
    }

    private static async Task<IReadOnlyList<object>> ReadSnapshotsAsync(BaseContainer container, UrlTarget target)
    {
        if (target == UrlTarget.Plex)
            return await ReadPlexSnapshotsAsync(container);
        return await ReadArrSnapshotsAsync(container, target);
    }

    private static async Task<List<ArrSnapshot>> ReadArrSnapshotsAsync(BaseContainer container, UrlTarget target)
    {
        using var context = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        if (target == UrlTarget.Radarr)
            return await context.RadarrIntegrations.OrderBy(x => x.Id).Select(_radarrProjection).ToListAsync();
        return await context.SonarrIntegrations.OrderBy(x => x.Id).Select(_sonarrProjection).ToListAsync();
    }

    private static async Task<List<PlexConnectionSnapshot>> ReadPlexSnapshotsAsync(BaseContainer container)
    {
        using var context = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        return await context.PlexServerConnections.OrderBy(x => x.Id).Select(_plexProjection).ToListAsync();
    }

    private static async Task AddCustomConnectionAsync(BaseContainer container)
    {
        using var context = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        context.PlexServerConnections.Add(
            new PlexServerConnection
            {
                Protocol = "http",
                Address = "custom.example.test",
                Port = 32400,
                Url = "http://custom.example.test:32400",
                Local = false,
                Relay = false,
                IPv4 = false,
                IPv6 = false,
                IsCustom = true,
                PlexServerId = 1,
            }
        );
        await context.SaveChangesAsync();
    }

    private static ArrSnapshot CreateExpectedArrSnapshot(ArrScenario scenario, ArrSnapshot original, ArrSnapshot saved)
    {
        if (scenario.Operation == SaveOperation.Update)
        {
            return original with
            {
                DisplayName = scenario.Valid.Name,
                BaseUrl = scenario.ExpectedUrl,
                ApiKey = scenario.Valid.ApiKey.Trim(),
                Category = scenario.Valid.Category.Trim(),
                DownloadFolderId = FolderTypeDefaults.DefaultDownloadFolderId,
                ProvisioningState =
                    original.ProvisioningState == IntegrationProvisioningState.Configured
                        ? IntegrationProvisioningState.ChangesPending
                        : original.ProvisioningState,
                LastConnectionTestStatus = TestConnectionStatus.Unknown,
                LastConnectionTestHttpStatusCode = null,
                LastConnectionTestErrorMessage = null,
                LastConnectionTestedAt = null,
            };
        }

        return new ArrSnapshot(
            saved.Id,
            scenario.Valid.Name,
            scenario.ExpectedUrl,
            scenario.Valid.ApiKey.Trim(),
            saved.QBittorrentApiKey,
            saved.TorznabApiKey,
            scenario.Valid.Category.Trim(),
            null,
            null,
            IntegrationProvisioningState.Unconfigured,
            TestConnectionStatus.Unknown,
            null,
            null,
            null,
            FolderTypeDefaults.DefaultDownloadFolderId
        );
    }

    private static readonly Dictionary<(UrlTarget, SaveOperation), ArrScenario> _arrScenarios = new()
    {
        [(UrlTarget.Radarr, SaveOperation.Create)] = new(
            UrlTarget.Radarr,
            SaveOperation.Create,
            60101,
            "https://radarr-valid.example.com:7878/radarr",
            new("Invalid Radarr", "ftp://radarr-invalid.example.com", "invalid-key", "invalid-category"),
            new("Radarr Valid", "  https://radarr-valid.example.com:7878/radarr/  ", " valid-key ", " valid-category ")
        ),
        [(UrlTarget.Radarr, SaveOperation.Update)] = new(
            UrlTarget.Radarr,
            SaveOperation.Update,
            60102,
            "https://radarr-updated.example.com:7879/radarr",
            new("Updated Radarr", "not-a-url", "updated-key", "updated-category"),
            new(
                "Updated Radarr",
                "  https://radarr-updated.example.com:7879/radarr/  ",
                " updated-key ",
                " updated-category "
            )
        ),
        [(UrlTarget.Sonarr, SaveOperation.Create)] = new(
            UrlTarget.Sonarr,
            SaveOperation.Create,
            60103,
            "http://sonarr-valid.example.com:8989/sonarr",
            new("Invalid Sonarr", "file://sonarr-invalid.example.com", "invalid-key", "invalid-category"),
            new("Sonarr Valid", "  http://sonarr-valid.example.com:8989/sonarr/  ", " valid-key ", " valid-category ")
        ),
        [(UrlTarget.Sonarr, SaveOperation.Update)] = new(
            UrlTarget.Sonarr,
            SaveOperation.Update,
            60104,
            "https://sonarr-updated.example.com:8990/sonarr",
            new("Updated Sonarr", "https://", "updated-key", "updated-category"),
            new(
                "Updated Sonarr",
                "  https://sonarr-updated.example.com:8990/sonarr/  ",
                " updated-key ",
                " updated-category "
            )
        ),
    };

    private static readonly Dictionary<SaveOperation, PlexScenario> _plexScenarios = new()
    {
        [SaveOperation.Create] = new(
            SaveOperation.Create,
            60105,
            "https://192.168.0.42:32400/plex/",
            new("ftp://plex-invalid.example.com:32400", "ftp", "plex-invalid.example.com", 32400),
            new("  https://192.168.0.42:32400/plex/  ", "HTTPS", "192.168.0.42", 32400)
        ),
        [SaveOperation.Update] = new(
            SaveOperation.Update,
            60106,
            "https://10.0.0.42:32401/plex/",
            new("not a url", "HTTPS", "custom-updated.example.test", 32401),
            new("  https://10.0.0.42:32401/plex/  ", "HTTPS", "10.0.0.42", 32401)
        ),
    };

    private static readonly Expression<Func<RadarrIntegration, ArrSnapshot>> _radarrProjection = x => new ArrSnapshot(
        x.Id,
        x.DisplayName,
        x.BaseUrl,
        x.RadarrApiKey,
        x.QBittorrentApiKey,
        x.TorznabApiKey,
        x.Category,
        x.ExternalDownloadClientId,
        x.ExternalIndexerId,
        x.ProvisioningState,
        x.LastConnectionTestStatus,
        x.LastConnectionTestHttpStatusCode,
        x.LastConnectionTestErrorMessage,
        x.LastConnectionTestedAt,
        x.DownloadFolderId
    );

    private static readonly Expression<Func<SonarrIntegration, ArrSnapshot>> _sonarrProjection = x => new ArrSnapshot(
        x.Id,
        x.DisplayName,
        x.BaseUrl,
        x.SonarrApiKey,
        x.QBittorrentApiKey,
        x.TorznabApiKey,
        x.Category,
        x.ExternalDownloadClientId,
        x.ExternalIndexerId,
        x.ProvisioningState,
        x.LastConnectionTestStatus,
        x.LastConnectionTestHttpStatusCode,
        x.LastConnectionTestErrorMessage,
        x.LastConnectionTestedAt,
        x.DownloadFolderId
    );

    private static readonly Expression<Func<PlexServerConnection, PlexConnectionSnapshot>> _plexProjection =
        x => new PlexConnectionSnapshot(
            x.Id,
            x.Protocol,
            x.Address,
            x.Port,
            x.Url,
            x.Local,
            x.Relay,
            x.IPv4,
            x.IPv6,
            x.IsCustom,
            x.PlexServerId
        );

    private sealed class SaveEndpointCommandExecutor : ICommandExecutor
    {
        private readonly ICommandExecutor _inner = new CommandExecutor();

        public Task<TResult> Send<TResult>(FastEndpoints.ICommand<TResult> command, CancellationToken ct = default)
            where TResult : ResultBase, new()
        {
            if (command is NotifyArrAppsOnStartupCommand)
                return Task.FromResult((TResult)(object)Result.Ok());
            return _inner.Send(command, ct);
        }
    }

    public enum UrlTarget
    {
        Radarr,
        Sonarr,
        Plex,
    }

    public enum SaveOperation
    {
        Create,
        Update,
    }

    private sealed record ArrScenario(
        UrlTarget Target,
        SaveOperation Operation,
        int Seed,
        string ExpectedUrl,
        ArrInput Invalid,
        ArrInput Valid
    );

    private sealed record ArrInput(string Name, string Url, string ApiKey, string Category);

    private sealed record PlexScenario(
        SaveOperation Operation,
        int Seed,
        string ExpectedUrl,
        PlexInput Invalid,
        PlexInput Valid
    );

    private sealed record PlexInput(string Url, string Protocol, string Address, int Port);

    private sealed record ArrSnapshot(
        Guid Id,
        string DisplayName,
        string BaseUrl,
        string ApiKey,
        string QBittorrentApiKey,
        string TorznabApiKey,
        string Category,
        int? ExternalDownloadClientId,
        int? ExternalIndexerId,
        IntegrationProvisioningState ProvisioningState,
        TestConnectionStatus LastConnectionTestStatus,
        int? LastConnectionTestHttpStatusCode,
        string? LastConnectionTestErrorMessage,
        DateTime? LastConnectionTestedAt,
        int DownloadFolderId
    );

    private sealed record PlexConnectionSnapshot(
        int Id,
        string Protocol,
        string Address,
        int Port,
        string Url,
        bool Local,
        bool Relay,
        bool IPv4,
        bool IPv6,
        bool IsCustom,
        int PlexServerId
    );
}
