namespace Reaparr.Application.UnitTests;

public class SetServerOwnedEndpointUnitTests
    : BaseEndpointUnitTest<SetServerOwnedEndpoint, SetServerOwnedRequest, ResultDTO<PlexServerDTO>>
{
    [Before(Test)]
    public void SetupComparisonJobLifecycle()
    {
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<InvalidateLibraryComparisonJobsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<ScheduleAffectedLibraryComparisonJobsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
    }

    [Test]
    public async Task ShouldPersistOwnedOverrideOnPlexServer_WhenRequestIsValid()
    {
        // Arrange
        await SetupDatabase(91103, config => config.PlexServerCount = 1);

        var dbContext = IDbContext;
        var plexServer = await dbContext.PlexServers.IgnoreIsEnabledFilter().FirstAsync(CancellationToken);

        await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .Where(x => x.Id == plexServer.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.OwnedOverride, (bool?)null), CancellationToken);

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SetServerOwnedRequest { PlexServerId = plexServer.Id, IsOwned = true }
        );

        // Assert
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBe(true);

        var updatedServer = await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .FirstAsync(x => x.Id == plexServer.Id, CancellationToken);
        updatedServer.OwnedOverride.ShouldBe(true);
    }

    [Test]
    public async Task ShouldSetOwnedOverrideFalse_WhenRequestSetsServerNotOwned()
    {
        // Arrange
        await SetupDatabase(91105, config => config.PlexServerCount = 1);

        var dbContext = IDbContext;
        var plexServer = await dbContext.PlexServers.IgnoreIsEnabledFilter().FirstAsync(CancellationToken);

        await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .Where(x => x.Id == plexServer.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.OwnedOverride, true), CancellationToken);

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SetServerOwnedRequest { PlexServerId = plexServer.Id, IsOwned = false }
        );

        // Assert
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBe(true);

        var updatedServer = await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .FirstAsync(x => x.Id == plexServer.Id, CancellationToken);
        updatedServer.OwnedOverride.ShouldBe(false);
    }

    [Test]
    public async Task ShouldReturnFailedResult_WhenServerDoesNotExist()
    {
        // Arrange
        await SetupDatabase(91110, config => config.PlexServerCount = 0);

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SetServerOwnedRequest { PlexServerId = 9999, IsOwned = true }
        );

        // Assert
        endpointResult.ShouldNotBeNull();
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBe(false);
    }

    [Test]
    public async Task ShouldNotChangeIsEnabled_WhenChangingOwnedOverride()
    {
        // Arrange
        await SetupDatabase(91111, config => config.PlexServerCount = 1);
        var db = IDbContext;
        var server = await db.PlexServers.IgnoreIsEnabledFilter().FirstAsync(CancellationToken);
        await db
            .PlexServers.IgnoreIsEnabledFilter()
            .Where(x => x.Id == server.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, false), CancellationToken);

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SetServerOwnedRequest { PlexServerId = server.Id, IsOwned = true }
        );

        // Assert
        endpointResult.ShouldNotBeNull();
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBe(true);
        var updated = await db
            .PlexServers.IgnoreIsEnabledFilter()
            .FirstAsync(x => x.Id == server.Id, CancellationToken);
        updated.IsEnabled.ShouldBe(false);
        updated.OwnedOverride.ShouldBe(true);
    }

    [Test]
    public async Task ShouldToggleOwnedOverrideTwice_WhenTwoRequestsAreSent()
    {
        // Arrange
        await SetupDatabase(91112, config => config.PlexServerCount = 1);
        var db = IDbContext;
        var server = await db.PlexServers.IgnoreIsEnabledFilter().FirstAsync(CancellationToken);
        // Act
        await TestEndpointHandleAsync(new SetServerOwnedRequest { PlexServerId = server.Id, IsOwned = true });
        await TestEndpointHandleAsync(new SetServerOwnedRequest { PlexServerId = server.Id, IsOwned = false });

        // Assert
        var updated = await db
            .PlexServers.IgnoreIsEnabledFilter()
            .FirstAsync(x => x.Id == server.Id, CancellationToken);
        updated.OwnedOverride.ShouldBe(false);
    }

    [Test]
    public async Task ShouldSetOwnedOverrideTrue_WhenCurrentValueIsNull()
    {
        // Arrange
        await SetupDatabase(91113, config => config.PlexServerCount = 1);
        var db = IDbContext;
        var server = await db.PlexServers.IgnoreIsEnabledFilter().FirstAsync(CancellationToken);
        await db
            .PlexServers.IgnoreIsEnabledFilter()
            .Where(x => x.Id == server.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.OwnedOverride, (bool?)null), CancellationToken);

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SetServerOwnedRequest { PlexServerId = server.Id, IsOwned = true }
        );

        // Assert
        endpointResult.ShouldNotBeNull();
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBe(true);
        var updated = await db
            .PlexServers.IgnoreIsEnabledFilter()
            .FirstAsync(x => x.Id == server.Id, CancellationToken);
        updated.OwnedOverride.ShouldBe(true);
    }

    [Test]
    public async Task ShouldSetOwnedOverrideFalse_WhenCurrentValueIsNull()
    {
        // Arrange
        await SetupDatabase(91114, config => config.PlexServerCount = 1);
        var db = IDbContext;
        var server = await db.PlexServers.IgnoreIsEnabledFilter().FirstAsync(CancellationToken);
        await db
            .PlexServers.IgnoreIsEnabledFilter()
            .Where(x => x.Id == server.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.OwnedOverride, (bool?)null), CancellationToken);

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SetServerOwnedRequest { PlexServerId = server.Id, IsOwned = false }
        );

        // Assert
        endpointResult.ShouldNotBeNull();
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBe(true);
        var updated = await db
            .PlexServers.IgnoreIsEnabledFilter()
            .FirstAsync(x => x.Id == server.Id, CancellationToken);
        updated.OwnedOverride.ShouldBe(false);
    }

    [Test]
    public async Task ShouldNotUpdateOtherServersOwnedOverride_WhenOneServerIsUpdated()
    {
        // Arrange
        await SetupDatabase(91115, config => config.PlexServerCount = 2);
        var db = IDbContext;
        var servers = await db.PlexServers.IgnoreIsEnabledFilter().OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var targetId = servers[0].Id;
        var otherId = servers[1].Id;

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SetServerOwnedRequest { PlexServerId = targetId, IsOwned = true }
        );

        // Assert
        endpointResult.ShouldNotBeNull();
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBe(true);
        var target = await db.PlexServers.IgnoreIsEnabledFilter().FirstAsync(x => x.Id == targetId, CancellationToken);
        var other = await db.PlexServers.IgnoreIsEnabledFilter().FirstAsync(x => x.Id == otherId, CancellationToken);
        target.OwnedOverride.ShouldBe(true);
        other.OwnedOverride.ShouldBe(servers[1].OwnedOverride);
    }

    [Test]
    public async Task ShouldInvalidateServerLibraries_WhenChangingOwnedOverride()
    {
        // Arrange
        await SetupDatabase(
            91116,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 2;
                config.PlexTvShowLibraryCount = 2;
            }
        );

        var dbContext = IDbContext;
        var server = await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .OrderBy(x => x.Id)
            .FirstAsync(CancellationToken);
        Mock.SetupCommand(It.IsAny<ScheduleAffectedLibraryComparisonJobsCommand>).ReturnsAsync(Result.Ok());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SetServerOwnedRequest { PlexServerId = server.Id, IsOwned = true });

        // Assert
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBe(true);
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(It.IsAny<ScheduleAffectedLibraryComparisonJobsCommand>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce());
    }

    [Test]
    public void ShouldRejectInvalidServerId_WhenValidatingSetOwnedRequest()
    {
        // Arrange
        var validator = new SetServerOwnedRequestValidator();

        // Act
        var result = validator.Validate(new SetServerOwnedRequest { PlexServerId = 0, IsOwned = true });

        // Assert
        result.IsValid.ShouldBe(false);
    }
}
