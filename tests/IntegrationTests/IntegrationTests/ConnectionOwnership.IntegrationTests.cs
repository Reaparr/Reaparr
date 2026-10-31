using System.Net.Http.Json;

namespace Reaparr.IntegrationTests;

public class ConnectionOwnershipIntegrationTests : BaseIntegrationTests
{
    [Test]
    [Arguments(RejectedMutation.ManagedUpdate, HttpStatusCode.BadRequest)]
    [Arguments(RejectedMutation.ManagedDelete, HttpStatusCode.BadRequest)]
    [Arguments(RejectedMutation.MissingUpdate, HttpStatusCode.NotFound)]
    [Arguments(RejectedMutation.MissingDelete, HttpStatusCode.NotFound)]
    [Arguments(RejectedMutation.WrongOwnerUpdate, HttpStatusCode.BadRequest)]
    public async Task ShouldRejectMutationAndPreserveRows_WhenConnectionIsNotEditable(
        RejectedMutation mutation,
        HttpStatusCode expectedStatus
    )
    {
        // Arrange
        using var container = await CreateConnectionContainerAsync(44101);
        var initial = await ReadStateAsync(container);
        var targetId =
            mutation == RejectedMutation.WrongOwnerUpdate
                ? (await AddCustomConnectionAsync(container, 1)).Id
                : initial.Connections.Single(x => x.PlexServerId == 1).Id;
        await SetPreferredConnectionAsync(container, 1, targetId);
        var before = await ReadStateAsync(container);
        before
            .Connections.Single(x => x.Id == targetId)
            .IsCustom.ShouldBe(mutation == RejectedMutation.WrongOwnerUpdate);
        before.Servers.Single(x => x.Id == 1).PreferredConnectionId.ShouldBe(targetId);
        using var client = container.GetApiClient();
        await client.SignIn();
        using var request = CreateRejectedMutationRequest(mutation, targetId, before);

        // Act
        using var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(expectedStatus);
        var result = await response.Content.ReadFromJsonAsync<BaseResultDTO>();
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.StatusCode.ShouldBe((int)expectedStatus);
        result.Errors.Count.ShouldBe(1);
        var after = await ReadStateAsync(container);
        after.Connections.ShouldBe(before.Connections);
        after.Servers.ShouldBe(before.Servers);
    }

    public enum RejectedMutation
    {
        ManagedUpdate,
        ManagedDelete,
        MissingUpdate,
        MissingDelete,
        WrongOwnerUpdate,
    }

    private static HttpRequestMessage CreateRejectedMutationRequest(
        RejectedMutation mutation,
        int targetId,
        ConnectionState before
    )
    {
        var requestedId = mutation is RejectedMutation.MissingUpdate or RejectedMutation.MissingDelete
            ? before.Connections.Max(x => x.Id) + 1000
            : targetId;
        return CreateMutationRequest(
            mutation is RejectedMutation.ManagedDelete or RejectedMutation.MissingDelete,
            requestedId,
            mutation == RejectedMutation.WrongOwnerUpdate ? 2 : 1
        );
    }

    [Test]
    public async Task ShouldUpdateCustomConnectionAndPreserveOwnerFlagsAndPreference_WhenRequestIsValid()
    {
        // Arrange
        using var container = await CreateConnectionContainerAsync(44103);
        var custom = await AddCustomConnectionAsync(container, 1);
        await SetPreferredConnectionAsync(container, 1, custom.Id);
        var before = await ReadStateAsync(container);
        before
            .Connections.Single(x => x.Id == custom.Id)
            .ShouldBe(
                new ConnectionSnapshot(
                    custom.Id,
                    "http",
                    "custom.example.test",
                    32400,
                    "http://custom.example.test:32400",
                    false,
                    false,
                    false,
                    false,
                    true,
                    1
                )
            );
        before.Servers.ShouldBe([new ServerSnapshot(1, custom.Id), new ServerSnapshot(2, 0)]);
        var expected = before.Connections.Single(x => x.Id == custom.Id) with
        {
            Protocol = "https",
            Address = "127.0.0.42",
            Port = 32401,
            Url = "https://127.0.0.42:32401",
            IPv4 = true,
        };
        using var client = container.GetApiClient();
        await client.SignIn();
        using var request = CreateMutationRequest(false, custom.Id, 1);

        // Act
        using var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Deserialize<PlexServerConnectionDTO>();
        result.IsSuccess.ShouldBeTrue();
        result.StatusCode.ShouldBe(StatusCodes.Status200OK);
        result.Errors.Count.ShouldBe(0);
        result.Successes.Count.ShouldBe(1);
        result.Value.ShouldNotBeNull();
        var value = result.Value;
        new ConnectionSnapshot(
            value.Id,
            value.Protocol,
            value.Address,
            value.Port,
            value.Url,
            value.Local,
            value.Relay,
            value.IPv4,
            value.IPv6,
            value.IsCustom,
            value.PlexServerId
        ).ShouldBe(expected);
        value.ChosenConnection.ShouldBeFalse();
        value.IsPlexTvConnection.ShouldBeFalse();
        value.Type.ShouldBe(PlexConnectionTypes.Public);
        value.LatestConnectionStatus.ShouldBeNull();
        var after = await ReadStateAsync(container);
        after.Connections.ShouldBe(before.Connections.Select(x => x.Id == custom.Id ? expected : x).ToList());
        after.Servers.ShouldBe(before.Servers);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldDeleteOnlyCustomConnectionAndClearOnlyMatchingPreference(bool preferred)
    {
        // Arrange
        using var container = await CreateConnectionContainerAsync(44105);
        var custom = await AddCustomConnectionAsync(container, 1);
        var initial = await ReadStateAsync(container);
        var otherServerConnectionId = initial.Connections.Single(x => x.PlexServerId == 2).Id;
        var retainedConnectionId = initial.Connections.Single(x => x.PlexServerId == 1 && !x.IsCustom).Id;
        var originalPreference = preferred ? custom.Id : retainedConnectionId;
        await SetPreferredConnectionAsync(container, 1, originalPreference);
        await SetPreferredConnectionAsync(container, 2, otherServerConnectionId);
        var before = await ReadStateAsync(container);
        before.Servers.ShouldBe([
            new ServerSnapshot(1, originalPreference),
            new ServerSnapshot(2, otherServerConnectionId),
        ]);
        using var client = container.GetApiClient();
        await client.SignIn();

        // Act
        using var response = await client.DeleteAsync($"{ApiRoutes.PlexServerConnectionController}/{custom.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<BaseResultDTO>();
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.StatusCode.ShouldBe(StatusCodes.Status200OK);
        result.Errors.Count.ShouldBe(0);
        var after = await ReadStateAsync(container);
        after.Connections.ShouldBe(before.Connections.Where(x => x.Id != custom.Id).ToList());
        after.Servers.ShouldBe([
            new ServerSnapshot(1, preferred ? 0 : retainedConnectionId),
            new ServerSnapshot(2, otherServerConnectionId),
        ]);
    }

    [Test]
    public async Task ShouldRollbackPreferredCustomDeletion_WhenPreferenceUpdateFails()
    {
        // Arrange
        using var container = await CreateConnectionContainerAsync(44109);
        var custom = await AddCustomConnectionAsync(container, 1);
        await SetPreferredConnectionAsync(container, 1, custom.Id);
        var before = await ReadStateAsync(container);
        before.Servers.ShouldBe([new ServerSnapshot(1, custom.Id), new ServerSnapshot(2, 0)]);
        using var setupContext = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        await setupContext.ExecuteSqlInterpolatedAsync(
            $"CREATE TRIGGER FailPreferredConnectionUpdate BEFORE UPDATE OF PreferredConnectionId ON PlexServers BEGIN SELECT RAISE(ABORT, 'preferred connection update blocked'); END;"
        );
        using var client = container.GetApiClient();
        await client.SignIn();

        // Act
        using var response = await client.DeleteAsync($"{ApiRoutes.PlexServerConnectionController}/{custom.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        var result = await response.Content.ReadFromJsonAsync<BaseResultDTO>();
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        result.Errors.Count.ShouldBe(1);
        var after = await ReadStateAsync(container);
        after.Connections.ShouldBe(before.Connections);
        after.Servers.ShouldBe(before.Servers);
    }

    private Task<BaseContainer> CreateConnectionContainerAsync(int seed) =>
        CreateContainer(
            seed,
            config =>
            {
                config.DatabaseOptions = data =>
                {
                    data.PlexServerCount = 2;
                    data.PlexServerConnectionPerServerCount = 1;
                };
            }
        );

    private static HttpRequestMessage CreateMutationRequest(bool delete, int connectionId, int serverId) =>
        new(
            delete ? HttpMethod.Delete : HttpMethod.Patch,
            delete
                ? $"{ApiRoutes.PlexServerConnectionController}/{connectionId}"
                : ApiRoutes.PlexServerConnectionController
        )
        {
            Content = delete
                ? null
                : JsonContent.Create(
                    new UpdatePlexServerConnectionEndpointRequest
                    {
                        Id = connectionId,
                        PlexServerId = serverId,
                        Url = "https://127.0.0.42:32401",
                        Protocol = "HTTPS",
                        Address = "127.0.0.42",
                        Port = 32401,
                    }
                ),
        };

    private static async Task<ConnectionState> ReadStateAsync(BaseContainer container)
    {
        using var context = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        var connections = await context
            .PlexServerConnections.OrderBy(x => x.Id)
            .Select(x => new ConnectionSnapshot(
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
            ))
            .ToListAsync();
        var servers = await context
            .PlexServers.OrderBy(x => x.Id)
            .Select(x => new ServerSnapshot(x.Id, x.PreferredConnectionId))
            .ToListAsync();
        return new ConnectionState(connections, servers);
    }

    private static async Task<PlexServerConnection> AddCustomConnectionAsync(BaseContainer container, int serverId)
    {
        var connection = new PlexServerConnection
        {
            Id = 0,
            Protocol = "http",
            Address = "custom.example.test",
            Port = 32400,
            Url = "http://custom.example.test:32400",
            Local = false,
            Relay = false,
            IPv4 = false,
            IPv6 = false,
            IsCustom = true,
            PlexServerId = serverId,
        };
        using var context = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        context.PlexServerConnections.Add(connection);
        await context.SaveChangesAsync();
        return connection;
    }

    private static async Task SetPreferredConnectionAsync(BaseContainer container, int serverId, int connectionId)
    {
        using var context = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        var server = await context.PlexServers.AsTracking().SingleAsync(x => x.Id == serverId);
        server.PreferredConnectionId = connectionId;
        await context.SaveChangesAsync();
    }

    private sealed record ConnectionSnapshot(
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

    private sealed record ServerSnapshot(int Id, int PreferredConnectionId);

    private sealed record ConnectionState(List<ConnectionSnapshot> Connections, List<ServerSnapshot> Servers);
}
