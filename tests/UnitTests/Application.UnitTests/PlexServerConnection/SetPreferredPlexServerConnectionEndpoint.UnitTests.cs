namespace Reaparr.Application.UnitTests;

public class SetPreferredPlexServerConnectionEndpointUnitTests
    : BaseEndpointUnitTest<
        SetPreferredPlexServerConnectionEndpoint,
        SetPreferredPlexServerConnectionEndpointRequest,
        BaseResultDTO
    >
{
    [Test]
    public async Task ShouldReportSuccessfulSelection_WhenConnectionIsAlreadyPreferred()
    {
        // Arrange
        await SetupDatabase(
            44806,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexServerConnectionPerServerCount = 2;
            }
        );

        var dbContext = IDbContext;
        var server = await dbContext.PlexServers.IgnoreIsEnabledFilter().AsTracking().SingleAsync(CancellationToken);
        var connectionId = await dbContext
            .PlexServerConnections.Where(x => x.PlexServerId == server.Id)
            .Select(x => x.Id)
            .FirstAsync(CancellationToken);
        server.PreferredConnectionId = connectionId;
        await dbContext.SaveChangesAsync(CancellationToken);
        Mock.Mock<ITelemetry>().Invocations.Clear();
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.PlexConnectionChanged(
                    TelemetryAction.Preferred,
                    It.Is<PlexServerConnection>(connection => connection.Id == connectionId),
                    connectionId
                )
            )
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SetPreferredPlexServerConnectionEndpointRequest(server.Id, connectionId)
        );

        // Assert
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response!.Errors.Count.ShouldBe(0);
        endpointResult.Response!.IsSuccess.ShouldBeTrue();
        var persistedServer = await IDbContext.PlexServers.IgnoreIsEnabledFilter().SingleAsync(CancellationToken);
        persistedServer.PreferredConnectionId.ShouldBe(connectionId);
        Mock.Mock<ITelemetry>()
            .Verify(
                x =>
                    x.PlexConnectionChanged(
                        TelemetryAction.Preferred,
                        It.Is<PlexServerConnection>(connection => connection.Id == connectionId),
                        connectionId
                    ),
                Times.Once()
            );
    }
}
