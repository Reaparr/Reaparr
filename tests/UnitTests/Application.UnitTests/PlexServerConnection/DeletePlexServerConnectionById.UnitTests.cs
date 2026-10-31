namespace Reaparr.Application.UnitTests;

public class DeletePlexServerConnectionByIdUnitTests
    : BaseEndpointUnitTest<DeletePlexServerConnectionById, DeletePlexServerConnectionByIdRequest>
{
    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public async Task ShouldRejectRequest_WhenConnectionIdIsNonPositive(int invalidConnectionId)
    {
        // Arrange
        await SetupDatabase(
            44901,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexServerConnectionPerServerCount = 2;
            }
        );

        var dbContext = IDbContext;
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

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new DeletePlexServerConnectionByIdRequest(invalidConnectionId)
        );
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
        endpointResult.IsValid.ShouldBeFalse();
        endpointResult.ValidationErrors.Count.ShouldBe(1);
        endpointResult
            .ValidationErrors.Single()
            .PropertyName.ShouldBe(nameof(DeletePlexServerConnectionByIdRequest.PlexServerConnectionId));
        endpointResult.Response.ShouldBeNull();
        connectionsAfter.ShouldBe(connectionsBefore);
        serversAfter.ShouldBe(serversBefore);
    }

    [Test]
    public async Task ShouldReturnNotFoundAndPreserveRows_WhenConnectionDoesNotExist()
    {
        // Arrange
        await SetupDatabase(
            44902,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexServerConnectionPerServerCount = 2;
            }
        );

        var dbContext = IDbContext;
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
        var missingConnectionId = connectionsBefore.Max(x => x.Id) + 1000;
        connectionsBefore.ShouldNotContain(x => x.Id == missingConnectionId);

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new DeletePlexServerConnectionByIdRequest(missingConnectionId)
        );
        var response = endpointResult
            .Response.ShouldBeOfType<JsonElement>()
            .Deserialize<BaseResultDTO>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
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
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        response.ShouldNotBeNull();
        response.IsSuccess.ShouldBeFalse();
        response.Errors.Count.ShouldBe(1);
        connectionsAfter.ShouldBe(connectionsBefore);
        serversAfter.ShouldBe(serversBefore);
    }

    [Test]
    public async Task ShouldRejectDeletionAndPreserveRows_WhenConnectionWasGenerated()
    {
        // Arrange
        await SetupDatabase(
            44903,
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
        var serversBefore = await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.PreferredConnectionId })
            .ToListAsync(CancellationToken);

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new DeletePlexServerConnectionByIdRequest(targetConnection.Id)
        );
        var response = endpointResult
            .Response.ShouldBeOfType<JsonElement>()
            .Deserialize<BaseResultDTO>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
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
        response.ShouldNotBeNull();
        response.IsSuccess.ShouldBeFalse();
        response.Errors.Count.ShouldBe(1);
        connectionsAfter.ShouldBe(connectionsBefore);
        serversAfter.ShouldBe(serversBefore);
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
