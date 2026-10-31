namespace Reaparr.Application.UnitTests;

public class UpdatePlexServerConnectionEndpointUnitTests
    : BaseEndpointUnitTest<
        UpdatePlexServerConnectionEndpoint,
        UpdatePlexServerConnectionEndpointRequest,
        ResultDTO<PlexServerConnectionDTO>
    >
{
    [Test]
    [Arguments(nameof(UpdatePlexServerConnectionEndpointRequest.Id))]
    [Arguments(nameof(UpdatePlexServerConnectionEndpointRequest.PlexServerId))]
    [Arguments(nameof(UpdatePlexServerConnectionEndpointRequest.Url))]
    [Arguments(nameof(UpdatePlexServerConnectionEndpointRequest.Protocol))]
    [Arguments(nameof(UpdatePlexServerConnectionEndpointRequest.Address))]
    [Arguments(nameof(UpdatePlexServerConnectionEndpointRequest.Port))]
    public async Task ShouldRejectRequest_WhenValidatorFieldIsInvalid(string invalidProperty)
    {
        // Arrange
        await SetupDatabase(
            44801,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexServerConnectionPerServerCount = 2;
            }
        );

        var dbContext = IDbContext;
        var serverId = await dbContext.PlexServers.Select(x => x.Id).SingleAsync(CancellationToken);
        var connectionId = await dbContext.PlexServerConnections.Select(x => x.Id).FirstAsync(CancellationToken);
        var connectionsBefore = await dbContext
            .PlexServerConnections.OrderBy(x => x.Id)
            .Select(x => new ConnectionSnapshot(
                x.Id,
                x.PlexServerId,
                x.Protocol,
                x.Address,
                x.Port,
                x.Url,
                x.Local,
                x.Relay,
                x.IPv4,
                x.IPv6,
                x.IsCustom
            ))
            .ToListAsync(CancellationToken);
        var request = CreateInvalidRequest(invalidProperty, connectionId, serverId);

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);
        var connectionsAfter = await dbContext
            .PlexServerConnections.OrderBy(x => x.Id)
            .Select(x => new ConnectionSnapshot(
                x.Id,
                x.PlexServerId,
                x.Protocol,
                x.Address,
                x.Port,
                x.Url,
                x.Local,
                x.Relay,
                x.IPv4,
                x.IPv6,
                x.IsCustom
            ))
            .ToListAsync(CancellationToken);

        // Assert
        endpointResult.IsValid.ShouldBeFalse();
        endpointResult.ValidationErrors.Count.ShouldBe(1);
        endpointResult.ValidationErrors.Single().PropertyName.ShouldBe(invalidProperty);
        endpointResult.Response.ShouldBeNull();
        connectionsAfter.ShouldBe(connectionsBefore);
    }

    [Test]
    public async Task ShouldReturnNotFoundAndPreserveConnections_WhenConnectionDoesNotExist()
    {
        // Arrange
        await SetupDatabase(
            44802,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexServerConnectionPerServerCount = 2;
            }
        );

        var dbContext = IDbContext;
        var serverIds = await dbContext.PlexServers.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(CancellationToken);
        var connectionsBefore = await dbContext
            .PlexServerConnections.OrderBy(x => x.Id)
            .Select(x => new ConnectionSnapshot(
                x.Id,
                x.PlexServerId,
                x.Protocol,
                x.Address,
                x.Port,
                x.Url,
                x.Local,
                x.Relay,
                x.IPv4,
                x.IPv6,
                x.IsCustom
            ))
            .ToListAsync(CancellationToken);
        var missingConnectionId = connectionsBefore.Max(x => x.Id) + 1000;
        connectionsBefore.ShouldNotContain(x => x.Id == missingConnectionId);
        var request = new UpdatePlexServerConnectionEndpointRequest
        {
            Id = missingConnectionId,
            PlexServerId = serverIds[0],
            Url = "https://missing.example.com:32400",
            Protocol = "HTTPS",
            Address = "missing.example.com",
            Port = 32400,
        };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);
        var result = endpointResult.Response!;
        var connectionsAfter = await dbContext
            .PlexServerConnections.OrderBy(x => x.Id)
            .Select(x => new ConnectionSnapshot(
                x.Id,
                x.PlexServerId,
                x.Protocol,
                x.Address,
                x.Port,
                x.Url,
                x.Local,
                x.Relay,
                x.IPv4,
                x.IPv6,
                x.IsCustom
            ))
            .ToListAsync(CancellationToken);

        // Assert
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        result.IsSuccess.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        result.Value.ShouldBeNull();
        connectionsAfter.ShouldBe(connectionsBefore);
    }

    [Test]
    public async Task ShouldRejectUpdateAndPreserveConnections_WhenConnectionWasGenerated()
    {
        // Arrange
        await SetupDatabase(
            44803,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexServerConnectionPerServerCount = 2;
            }
        );

        var dbContext = IDbContext;
        var generatedConnections = await dbContext
            .PlexServerConnections.OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var targetConnection = generatedConnections[0];
        var controlConnection = generatedConnections[1];
        targetConnection.IsCustom.ShouldBeFalse();
        controlConnection.IsCustom.ShouldBeFalse();
        var connectionsBefore = await dbContext
            .PlexServerConnections.OrderBy(x => x.Id)
            .Select(x => new ConnectionSnapshot(
                x.Id,
                x.PlexServerId,
                x.Protocol,
                x.Address,
                x.Port,
                x.Url,
                x.Local,
                x.Relay,
                x.IPv4,
                x.IPv6,
                x.IsCustom
            ))
            .ToListAsync(CancellationToken);
        var request = new UpdatePlexServerConnectionEndpointRequest
        {
            Id = targetConnection.Id,
            PlexServerId = targetConnection.PlexServerId,
            Url = "https://replacement.example.com:32400",
            Protocol = "HTTPS",
            Address = "replacement.example.com",
            Port = 32400,
        };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);
        var result = endpointResult.Response!;
        var connectionsAfter = await dbContext
            .PlexServerConnections.OrderBy(x => x.Id)
            .Select(x => new ConnectionSnapshot(
                x.Id,
                x.PlexServerId,
                x.Protocol,
                x.Address,
                x.Port,
                x.Url,
                x.Local,
                x.Relay,
                x.IPv4,
                x.IPv6,
                x.IsCustom
            ))
            .ToListAsync(CancellationToken);

        // Assert
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        result.IsSuccess.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        result.Value.ShouldBeNull();
        connectionsAfter.ShouldBe(connectionsBefore);
    }

    [Test]
    public async Task ShouldRejectUpdateAndPreserveConnections_WhenRequestUsesWrongPlexServer()
    {
        // Arrange
        await SetupDatabase(
            44804,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexServerConnectionPerServerCount = 1;
            }
        );

        var dbContext = IDbContext;
        var servers = await dbContext.PlexServers.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var targetConnection = new PlexServerConnection
        {
            Id = 0,
            Protocol = "https",
            Address = "custom-owner.example.com",
            Port = 32400,
            Url = "https://custom-owner.example.com:32400",
            Local = false,
            Relay = false,
            IPv4 = false,
            IPv6 = false,
            IsCustom = true,
            PlexServerId = servers[0].Id,
        };
        dbContext.PlexServerConnections.Add(targetConnection);
        await dbContext.SaveChangesAsync(CancellationToken);

        var connectionsBefore = await dbContext
            .PlexServerConnections.OrderBy(x => x.Id)
            .Select(x => new ConnectionSnapshot(
                x.Id,
                x.PlexServerId,
                x.Protocol,
                x.Address,
                x.Port,
                x.Url,
                x.Local,
                x.Relay,
                x.IPv4,
                x.IPv6,
                x.IsCustom
            ))
            .ToListAsync(CancellationToken);
        var serversBefore = await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.PreferredConnectionId })
            .ToListAsync(CancellationToken);
        var request = new UpdatePlexServerConnectionEndpointRequest
        {
            Id = targetConnection.Id,
            PlexServerId = servers[1].Id,
            Url = "https://replacement.example.com:32400",
            Protocol = "HTTPS",
            Address = "replacement.example.com",
            Port = 32400,
        };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);
        var result = endpointResult.Response!;
        var connectionsAfter = await dbContext
            .PlexServerConnections.OrderBy(x => x.Id)
            .Select(x => new ConnectionSnapshot(
                x.Id,
                x.PlexServerId,
                x.Protocol,
                x.Address,
                x.Port,
                x.Url,
                x.Local,
                x.Relay,
                x.IPv4,
                x.IPv6,
                x.IsCustom
            ))
            .ToListAsync(CancellationToken);
        var serversAfter = await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.PreferredConnectionId })
            .ToListAsync(CancellationToken);

        // Assert
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        result.IsSuccess.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        result.Value.ShouldBeNull();
        connectionsAfter.ShouldBe(connectionsBefore);
        serversAfter.ShouldBe(serversBefore);
    }

    private static UpdatePlexServerConnectionEndpointRequest CreateInvalidRequest(
        string invalidProperty,
        int connectionId,
        int serverId
    )
    {
        var request = new UpdatePlexServerConnectionEndpointRequest
        {
            Id = connectionId,
            PlexServerId = serverId,
            Url = "https://custom.example.com:32400",
            Protocol = "HTTPS",
            Address = "custom.example.com",
            Port = 32400,
        };
        return invalidProperty switch
        {
            nameof(UpdatePlexServerConnectionEndpointRequest.Id) => request with { Id = 0 },
            nameof(UpdatePlexServerConnectionEndpointRequest.PlexServerId) => request with { PlexServerId = 0 },
            nameof(UpdatePlexServerConnectionEndpointRequest.Url) => request with { Url = string.Empty },
            nameof(UpdatePlexServerConnectionEndpointRequest.Protocol) => request with { Protocol = string.Empty },
            nameof(UpdatePlexServerConnectionEndpointRequest.Address) => request with { Address = string.Empty },
            // Port is the remaining declared validator test case.
            _ => request with { Port = 0 },
        };
    }

    private sealed record ConnectionSnapshot(
        int Id,
        int PlexServerId,
        string Protocol,
        string Address,
        int Port,
        string Url,
        bool Local,
        bool Relay,
        bool IPv4,
        bool IPv6,
        bool IsCustom
    );
}
