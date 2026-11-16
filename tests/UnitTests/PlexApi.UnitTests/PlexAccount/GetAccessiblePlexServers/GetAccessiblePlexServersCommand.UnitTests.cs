using System.Net;
using LukeHagar.PlexAPI.SDK;
using LukeHagar.PlexAPI.SDK.Models.Components;
using LukeHagar.PlexAPI.SDK.Models.Errors;
using LukeHagar.PlexAPI.SDK.Models.Requests;
using Newtonsoft.Json;
using Reaparr.PlexApi.Contracts;

namespace Reaparr.PlexApi.UnitTests;

public class GetAccessiblePlexServersUnitTests : BaseUnitTest<GetAccessiblePlexServersCommandHandler>
{
    private void SetCallMock(GetServerResourcesResponse response1, GetServerResourcesResponse response2)
    {
        Mock.Mock<IPlexApiClientFactory>()
            .Setup(x => x.CreateTvClient(It.IsAny<string>(), It.IsAny<PlexApiClientOptions?>()))
            .Returns<string, PlexApiClientOptions?>(
                (_, _) =>
                {
                    var plexApiMock = new Mock<IPlexAPI>();
                    var plexApiMockIPlex = new Mock<IPlex>();

                    plexApiMockIPlex
                        .SetupSequence(x =>
                            x.GetServerResourcesAsync(It.IsAny<GetServerResourcesRequest>(), It.IsAny<string>())
                        )
                        .ReturnsAsync(response1)
                        .ReturnsAsync(response2);

                    plexApiMock.SetupGet(x => x.Plex).Returns(plexApiMockIPlex.Object);

                    return plexApiMock.Object;
                }
            )
            .Verifiable(Times.AtLeastOnce);
    }

    [Test]
    public async Task ShouldReturnEmptyResult_WhenPlexAccountHasAnEmptyAuthToken()
    {
        // Arrange
        await SetupDatabase(1234, config => config.PlexAccountCount = 1);
        var dbContext = IDbContext;
        var plexAccount = await dbContext.PlexAccounts.AsTracking().FirstOrDefaultAsync(CancellationToken);
        plexAccount.ShouldNotBeNull();
        plexAccount.AuthenticationToken = string.Empty;
        await dbContext.SaveChangesAsync(CancellationToken);

        // Act
        var command = new GetAccessiblePlexServersCommand(plexAccount.Id);
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Has400BadRequestError().ShouldBeTrue();
    }

    [Test]
    public async Task ShouldReturnFailedResult_WhenBothResponsesFail()
    {
        // Arrange
        await SetupDatabase(1234, config => config.PlexAccountCount = 1);
        var plexAccount = await IDbContext.PlexAccounts.FirstOrDefaultAsync(CancellationToken);
        plexAccount.ShouldNotBeNull();

        Mock.Mock<IPlexApiClientFactory>()
            .Setup(x => x.CreateTvClient(It.IsAny<string>(), It.IsAny<PlexApiClientOptions?>()))
            .Returns<string, PlexApiClientOptions?>(
                (_, _) =>
                {
                    var plexApiMock = new Mock<IPlexAPI>();
                    var plexApiMockIPlex = new Mock<IPlex>();

                    var response = FakePlexApiData.GetServerResourcesResponse(
                        HttpStatusCode.Unauthorized,
                        new Seed(939)
                    );
                    plexApiMockIPlex
                        .Setup(x =>
                            x.GetServerResourcesAsync(It.IsAny<GetServerResourcesRequest>(), It.IsAny<string>())
                        )
                        .ThrowsAsync(
                            new SDKException("Not authorized to access Plex server", response.RawResponse, string.Empty)
                        );

                    plexApiMock.SetupGet(x => x.Plex).Returns(plexApiMockIPlex.Object);

                    return plexApiMock.Object;
                }
            )
            .Verifiable(Times.AtLeastOnce);

        // Act
        var command = new GetAccessiblePlexServersCommand(plexAccount.Id);
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldNotBeEmpty();
    }

    [Test]
    public async Task ShouldRemoveDuplicateConnections_WhenMultipleServersHaveTheSameConnection()
    {
        // Arrange
        await SetupDatabase(1234, config => config.PlexAccountCount = 1);
        var plexAccount = await IDbContext.PlexAccounts.FirstOrDefaultAsync(CancellationToken);
        plexAccount.ShouldNotBeNull();

        var serverCount = 5;
        var response1 = FakePlexApiData.GetServerResourcesResponse(HttpStatusCode.OK, new Seed(939), options: config =>
        {
            config.PlexServerAccessCount = serverCount;
        });

        var response2 = FakePlexApiData.GetServerResourcesResponse(HttpStatusCode.OK, new Seed(939), options: config =>
        {
            config.PlexServerAccessCount = serverCount;
            config.PlexServerAccessConnectionsIncludeHttps = true;
        });

        response1.PlexDevices.ShouldNotBeNull();

        var testConnection = response1.PlexDevices[0].Connections[0];

        testConnection.ShouldNotBeNull();
        foreach (var plexDevice in response1.PlexDevices)
        {
            plexDevice.Connections.Add(testConnection);
        }

        SetCallMock(response1, response2);

        // Act
        var command = new GetAccessiblePlexServersCommand(plexAccount.Id);
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(serverCount);

        var connections = result.Value.SelectMany(x => x.PlexServer.PlexServerConnections).ToList();

        connections.Select(connection => connection.Url).ShouldBeUnique();
    }

    [Test]
    public async Task ShouldRemoveDuplicateConnectionsFromRealResponse_WhenMultipleServersHaveTheSameConnection()
    {
        // Arrange
        await SetupDatabase(1234, config => config.PlexAccountCount = 1);
        var plexAccount = await IDbContext.PlexAccounts.FirstOrDefaultAsync(CancellationToken);
        plexAccount.ShouldNotBeNull();

        var response1 = new GetServerResourcesResponse
        {
            PlexDevices = JsonConvert.DeserializeObject<List<PlexDevice>>(PlexApiWrapperTestData.Response1),
            StatusCode = (int)HttpStatusCode.OK,
            RawResponse = new HttpResponseMessage
            {
                Content = PlexApiWrapperTestData.Response1.ToStringContent(),
                ReasonPhrase = nameof(HttpStatusCode.OK),
                RequestMessage = null,
                StatusCode = HttpStatusCode.OK,
                Version = new Version(1, 1),
            },
        };

        var response2 = new GetServerResourcesResponse
        {
            PlexDevices = JsonConvert.DeserializeObject<List<PlexDevice>>(PlexApiWrapperTestData.Response2),
            StatusCode = (int)HttpStatusCode.OK,
            RawResponse = new HttpResponseMessage
            {
                Content = PlexApiWrapperTestData.Response2.ToStringContent(),
                ReasonPhrase = nameof(HttpStatusCode.OK),
                RequestMessage = null,
                StatusCode = HttpStatusCode.OK,
                Version = new Version(1, 1),
            },
        };

        SetCallMock(response1, response2);

        // Act
        var command = new GetAccessiblePlexServersCommand(plexAccount.Id);
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var connections = result.Value.SelectMany(x => x.PlexServer.PlexServerConnections).ToList();

        connections.Select(connection => connection.Url).ShouldBeUnique();
    }

    [Test]
    public async Task ShouldReturnServersFromSecondResponse_WhenFirstResponseFails()
    {
        // Arrange
        await SetupDatabase(1234, config => config.PlexAccountCount = 1);
        var plexAccount = await IDbContext.PlexAccounts.FirstOrDefaultAsync(CancellationToken);
        plexAccount.ShouldNotBeNull();

        var serverCount = 2;

        var response1 = new GetServerResourcesResponse
        {
            PlexDevices = [],
            StatusCode = (int)HttpStatusCode.InternalServerError,
            RawResponse = FakePlexApiData.GetHttpResponseMessage<string?>(
                HttpStatusCode.InternalServerError,
                null,
                null
            ),
        };

        var response2 = FakePlexApiData.GetServerResourcesResponse(HttpStatusCode.OK, new Seed(939), options: config =>
        {
            config.PlexServerAccessCount = serverCount;
            config.PlexServerAccessConnectionsIncludeHttps = true;
        });

        SetCallMock(response1, response2);

        // Act
        var command = new GetAccessiblePlexServersCommand(plexAccount.Id);
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(serverCount);
    }

    [Test]
    public async Task ShouldCombineConnectionsFromBothResponses_WhenBothAreSuccessful()
    {
        // Arrange
        await SetupDatabase(1234, config => config.PlexAccountCount = 1);
        var plexAccount = await IDbContext.PlexAccounts.FirstOrDefaultAsync(CancellationToken);
        plexAccount.ShouldNotBeNull();

        var serverCount = 5;
        var response1 = FakePlexApiData.GetServerResourcesResponse(HttpStatusCode.OK, new Seed(940), options: config =>
        {
            config.PlexServerAccessCount = serverCount;
            config.PlexServerAccessConnectionsIncludeHttps = true;
        });

        var response2 = FakePlexApiData.GetServerResourcesResponse(HttpStatusCode.OK, new Seed(939), options: config =>
        {
            config.PlexServerAccessCount = serverCount;
        });

        SetCallMock(response1, response2);

        // Act
        var command = new GetAccessiblePlexServersCommand(plexAccount.Id);
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(serverCount);

        var connections = result.Value.SelectMany(x => x.PlexServer.PlexServerConnections).ToList();

        connections.Select(connection => connection.Url).ShouldBeUnique();
    }

    [Test]
    public async Task ShouldCombineAllConnectionsFromBothResponses_WhenBothAreSuccessful()
    {
        // Arrange
        await SetupDatabase(1234, config => config.PlexAccountCount = 1);
        var plexAccount = await IDbContext.PlexAccounts.FirstOrDefaultAsync(CancellationToken);
        plexAccount.ShouldNotBeNull();

        var serverResource1 = FakePlexApiData.GetServerResource(new Seed(939)).Generate(2);
        serverResource1[0].Connections.Clear();
        serverResource1[0]
            .Connections.AddRange([
                new Connections
                {
                    Protocol = PlexDeviceProtocol.Http,
                    Address = "192.168.200.95",
                    Port = 32400,
                    Uri = "http://192.168.200.95:32400",
                    Local = true,
                    Relay = false,
                    IPv6 = false,
                },
                new Connections
                {
                    Protocol = PlexDeviceProtocol.Https,
                    Address = "www.albertflix.nl",
                    Port = 43324,
                    Uri = "https://www.albertflix.nl:43324",
                    Local = false,
                    Relay = false,
                    IPv6 = false,
                },
                new Connections
                {
                    Protocol = PlexDeviceProtocol.Http,
                    Address = "77.170.188.28",
                    Port = 43324,
                    Uri = "http://77.170.188.28:43324",
                    Local = false,
                    Relay = false,
                    IPv6 = false,
                },
            ]);
        serverResource1[1].Provides = "client,player";
        serverResource1[1].Connections.Clear();
        serverResource1[1]
            .Connections.AddRange([
                new Connections
                {
                    Protocol = PlexDeviceProtocol.Http,
                    Address = "192.168.200.95",
                    Port = 32400,
                    Uri = "http://192.168.200.95:32400",
                    Local = true,
                    Relay = false,
                    IPv6 = false,
                },
                new Connections
                {
                    Protocol = PlexDeviceProtocol.Https,
                    Address = "www.albertflix.nl",
                    Port = 43324,
                    Uri = "https://www.albertflix.nl:43324",
                    Local = false,
                    Relay = false,
                    IPv6 = false,
                },
                new Connections
                {
                    Protocol = PlexDeviceProtocol.Http,
                    Address = "77.170.188.28",
                    Port = 43324,
                    Uri = "http://77.170.188.28:43324",
                    Local = false,
                    Relay = false,
                    IPv6 = false,
                },
            ]);

        var serverResource2 = FakePlexApiData.GetServerResource(new Seed(939)).Generate(2);
        serverResource2[0].Connections.Clear();
        serverResource2[0]
            .Connections.AddRange([
                new Connections
                {
                    Protocol = PlexDeviceProtocol.Https,
                    Address = "192.168.201.95",
                    Port = 32400,
                    Uri = "https://192-168-201-95.fc4cf89b047845c0a48b8926677abe46.plex.direct:32400",
                    Local = true,
                    Relay = false,
                    IPv6 = false,
                },
                new Connections
                {
                    Protocol = PlexDeviceProtocol.Https,
                    Address = "www.albertflix.nl",
                    Port = 43324,
                    Uri = "https://www.albertflix.nl:43324",
                    Local = false,
                    Relay = false,
                    IPv6 = false,
                },
                new Connections
                {
                    Protocol = PlexDeviceProtocol.Https,
                    Address = "177.170.188.28",
                    Port = 43324,
                    Uri = "https://177-170-188-28.fc4cf89b047845c0a48b8926677abe46.plex.direct:43324",
                    Local = false,
                    Relay = false,
                    IPv6 = false,
                },
                new Connections
                {
                    Protocol = PlexDeviceProtocol.Https,
                    Address = "139.162.215.184",
                    Port = 8443,
                    Uri = "https://139-162-215-184.fc4cf89b047845c0a48b8926677abe46.plex.direct:8443",
                    Local = false,
                    Relay = true,
                    IPv6 = false,
                },
            ]);
        serverResource2[1].Provides = "client,player";
        serverResource2[1].Connections.Clear();
        serverResource2[1]
            .Connections.AddRange([
                new Connections
                {
                    Protocol = PlexDeviceProtocol.Https,
                    Address = "192.168.200.95",
                    Port = 32400,
                    Uri = "https://192-168-200-95.fc4cf89b047845c0a48b8926677abe46.plex.direct:32400",
                    Local = true,
                    Relay = false,
                    IPv6 = false,
                },
                new Connections
                {
                    Protocol = PlexDeviceProtocol.Https,
                    Address = "www.albertflix.nl",
                    Port = 43324,
                    Uri = "https://www.albertflix.nl:43324",
                    Local = false,
                    Relay = false,
                    IPv6 = false,
                },
                new Connections
                {
                    Protocol = PlexDeviceProtocol.Https,
                    Address = "177.170.188.28",
                    Port = 43324,
                    Uri = "https://177-170-188-28.fc4cf89b047845c0a48b8926677abe46.plex.direct:43324",
                    Local = false,
                    Relay = false,
                    IPv6 = false,
                },
            ]);

        var response1 = FakePlexApiData.GetServerResourcesResponse(
            HttpStatusCode.OK,
            new Seed(940),
            serverResource1,
            options: config =>
            {
                config.PlexServerAccessConnectionsIncludeHttps = true;
            }
        );

        var response2 = FakePlexApiData.GetServerResourcesResponse(HttpStatusCode.OK, new Seed(939), serverResource2);

        SetCallMock(response1, response2);

        // Act
        var command = new GetAccessiblePlexServersCommand(plexAccount.Id);
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(1);

        var server1 = result.Value.First();
        server1.ShouldNotBeNull();

        result.IsSuccess.ShouldBeTrue();

        var connections = result.Value.SelectMany(x => x.PlexServer.PlexServerConnections).ToList();

        connections.Select(connection => connection.Url).ShouldBeUnique();
    }
}
