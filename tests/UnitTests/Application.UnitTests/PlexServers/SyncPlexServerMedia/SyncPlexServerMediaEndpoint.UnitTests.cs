namespace Reaparr.Application.UnitTests;

public class SyncPlexServerMediaEndpointUnitTests
    : BaseEndpointUnitTest<SyncPlexServerMediaEndpoint, SyncPlexServerMediaEndpointRequest, BaseResultDTO>
{
    [Test]
    public async Task ShouldReturnBadRequestAndSkipQueue_WhenServerIsDisabled()
    {
        // Arrange
        await SetupDatabase(91104, config =>
        {
            config.PlexServerCount = 1;
            config.PlexMovieLibraryCount = 1;
        });

        var dbContext = IDbContext;
        var plexServer = await dbContext.PlexServers.IgnoreIsEnabledFilter().FirstAsync(CancellationToken);

        await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .Where(x => x.Id == plexServer.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, false), CancellationToken);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<QueueLibrarySyncJobCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SyncPlexServerMediaEndpointRequest { PlexServerId = plexServer.Id }
        );

        // Assert
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBeFalse();
        endpointResult.Response.Errors.ShouldContain(x =>
            x.Message.Contains("disabled", StringComparison.OrdinalIgnoreCase)
        );

        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<QueueLibrarySyncJobCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public async Task ShouldQueueLibrarySyncJob_WhenServerIsEnabledAndLibrariesExist()
    {
        // Arrange
        await SetupDatabase(91106, config =>
        {
            config.PlexServerCount = 1;
            config.PlexMovieLibraryCount = 2;
        });

        var dbContext = IDbContext;
        var plexServerId = (await dbContext.PlexServers.IgnoreIsEnabledFilter().FirstAsync(CancellationToken)).Id;

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<QueueLibrarySyncJobCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        ServerMediaSyncTelemetryDTO? telemetryRequest = null;
        ResultBase? telemetryResult = null;
        Mock.Mock<ITelemetry>()
            .Setup(x => x.ServerMediaSyncRequested(It.IsAny<ServerMediaSyncTelemetryDTO>(), It.IsAny<ResultBase>()))
            .Callback<ServerMediaSyncTelemetryDTO, ResultBase>(
                (request, result) =>
                {
                    telemetryRequest = request;
                    telemetryResult = result;
                }
            )
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SyncPlexServerMediaEndpointRequest
            {
                PlexServerId = plexServerId,
                ForceLibrarySync = true,
                ForceMediaRefresh = true,
            }
        );

        // Assert
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBeTrue();

        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<QueueLibrarySyncJobCommand>(cmd => cmd.PlexLibraryIds.Count == 2),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
        telemetryRequest.ShouldNotBeNull();
        telemetryRequest.PlexServerId.ShouldBe(plexServerId);
        telemetryRequest.ForceLibrarySync.ShouldBeTrue();
        telemetryRequest.ForceMediaRefresh.ShouldBeTrue();
        telemetryResult.ShouldNotBeNull().IsSuccess.ShouldBeTrue();
        Mock.Mock<ITelemetry>().Verify();
    }

    [Test]
    public async Task ShouldReturnBadRequestAndSkipQueue_WhenEnabledServerHasNoLibraries()
    {
        // Arrange
        await SetupDatabase(91107, config =>
        {
            config.PlexServerCount = 1;
            config.PlexMovieLibraryCount = 0;
        });

        var dbContext = IDbContext;
        var plexServerId = (await dbContext.PlexServers.IgnoreIsEnabledFilter().FirstAsync(CancellationToken)).Id;

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<QueueLibrarySyncJobCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SyncPlexServerMediaEndpointRequest { PlexServerId = plexServerId }
        );

        // Assert
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBeFalse();
        endpointResult.Response.Errors.ShouldContain(x =>
            x.Message.Contains("no libraries", StringComparison.OrdinalIgnoreCase)
        );

        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<QueueLibrarySyncJobCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public async Task ShouldNotQueueLibrariesFromDifferentServer_WhenSyncingSingleServer()
    {
        // Arrange
        await SetupDatabase(91116, config =>
        {
            config.PlexServerCount = 2;
            config.PlexMovieLibraryCount = 2;
        });

        var db = IDbContext;
        var servers = await db.PlexServers.IgnoreIsEnabledFilter().OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var targetServerId = servers[0].Id;

        var targetLibraryCount = await db.PlexLibraries.CountAsync(
            x => x.PlexServerId == targetServerId,
            CancellationToken
        );

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<QueueLibrarySyncJobCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SyncPlexServerMediaEndpointRequest { PlexServerId = targetServerId }
        );

        // Assert
        endpointResult.ShouldNotBeNull();
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBeTrue();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<QueueLibrarySyncJobCommand>(cmd => cmd.PlexLibraryIds.Count == targetLibraryCount),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldReturnFailedResult_WhenServerDoesNotExist()
    {
        // Arrange
        await SetupDatabase(91117, config => config.PlexServerCount = 0);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<QueueLibrarySyncJobCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SyncPlexServerMediaEndpointRequest { PlexServerId = 9999 }
        );

        // Assert
        endpointResult.ShouldNotBeNull();
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBeFalse();
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<QueueLibrarySyncJobCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public async Task ShouldStillFailWhenServerDisabledEvenIfLibrariesExist()
    {
        // Arrange
        await SetupDatabase(91118, config =>
        {
            config.PlexServerCount = 1;
            config.PlexMovieLibraryCount = 2;
        });

        var db = IDbContext;
        var serverId = (await db.PlexServers.IgnoreIsEnabledFilter().FirstAsync(CancellationToken)).Id;
        await db
            .PlexServers.IgnoreIsEnabledFilter()
            .Where(x => x.Id == serverId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, false), CancellationToken);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<QueueLibrarySyncJobCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SyncPlexServerMediaEndpointRequest { PlexServerId = serverId }
        );

        // Assert
        endpointResult.ShouldNotBeNull();
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBeFalse();
        endpointResult.Response.Errors.ShouldContain(x =>
            x.Message.Contains("disabled", StringComparison.OrdinalIgnoreCase)
        );
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<QueueLibrarySyncJobCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public async Task ShouldAllowSyncForEnabledServer_WhenAnotherServerIsDisabled()
    {
        // Arrange
        await SetupDatabase(91119, config =>
        {
            config.PlexServerCount = 2;
            config.PlexMovieLibraryCount = 1;
        });

        var db = IDbContext;
        var servers = await db.PlexServers.IgnoreIsEnabledFilter().OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var enabledServerId = servers[0].Id;
        var disabledServerId = servers[1].Id;

        await db
            .PlexServers.IgnoreIsEnabledFilter()
            .Where(x => x.Id == disabledServerId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, false), CancellationToken);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<QueueLibrarySyncJobCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SyncPlexServerMediaEndpointRequest { PlexServerId = enabledServerId }
        );

        // Assert
        endpointResult.ShouldNotBeNull();
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBeTrue();
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<QueueLibrarySyncJobCommand>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Test]
    public void ShouldRejectInvalidServerId_WhenValidatingSyncRequest()
    {
        // Arrange
        var validator = new SyncPlexServerMediaEndpointRequestValidator();

        // Act
        var result = validator.Validate(new SyncPlexServerMediaEndpointRequest { PlexServerId = 0 });

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Test]
    public async Task ShouldQueueSingleLibrary_WhenServerHasExactlyOneLibrary()
    {
        // Arrange
        await SetupDatabase(91120, config =>
        {
            config.PlexServerCount = 1;
            config.PlexMovieLibraryCount = 1;
        });

        var db = IDbContext;
        var serverId = (await db.PlexServers.IgnoreIsEnabledFilter().FirstAsync(CancellationToken)).Id;

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<QueueLibrarySyncJobCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SyncPlexServerMediaEndpointRequest { PlexServerId = serverId }
        );

        // Assert
        endpointResult.ShouldNotBeNull();
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBeTrue();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<QueueLibrarySyncJobCommand>(cmd => cmd.PlexLibraryIds.Count == 1),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
    }
}
