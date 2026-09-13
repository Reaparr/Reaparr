using Microsoft.Extensions.DependencyInjection;

namespace Reaparr.Application.UnitTests;

public class SetServerEnabledEndpointUnitTests
    : BaseEndpointUnitTest<SetServerEnabledEndpoint, SetServerEnabledRequest, ResultDTO<PlexServerDTO>>
{
    [Test]
    public async Task ShouldEnableServer_WhenServerIsDisabled()
    {
        // Arrange
        await SetupDatabase(91201, config => config.PlexServerCount = 1);

        var dbContext = IDbContext;
        var server = await dbContext.PlexServers.IgnoreIsEnabledFilter().FirstAsync(CancellationToken);

        await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .Where(x => x.Id == server.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, false), CancellationToken);
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SetServerEnabledRequest { PlexServerId = server.Id, IsEnabled = true }
        );

        // Assert
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBe(true);

        var updated = await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .FirstAsync(x => x.Id == server.Id, CancellationToken);
        updated.IsEnabled.ShouldBe(true);
    }

    [Test]
    public async Task ShouldDisableServer_WhenServerIsEnabled()
    {
        // Arrange
        await SetupDatabase(91203, config => config.PlexServerCount = 1);

        var dbContext = IDbContext;
        var server = await dbContext.PlexServers.IgnoreIsEnabledFilter().FirstAsync(CancellationToken);
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SetServerEnabledRequest { PlexServerId = server.Id, IsEnabled = false }
        );

        // Assert
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBe(true);

        var updated = await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .FirstAsync(x => x.Id == server.Id, CancellationToken);
        updated.IsEnabled.ShouldBeFalse();
    }

    [Test]
    public async Task ShouldNotChangeUnrelatedServer_WhenUpdatingTargetServer()
    {
        // Arrange
        await SetupDatabase(91204, config => config.PlexServerCount = 2);

        var dbContext = IDbContext;
        var servers = await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var targetId = servers[0].Id;
        var otherId = servers[1].Id;
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SetServerEnabledRequest { PlexServerId = targetId, IsEnabled = false }
        );

        // Assert
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBe(true);

        var target = await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .FirstAsync(x => x.Id == targetId, CancellationToken);
        var other = await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .FirstAsync(x => x.Id == otherId, CancellationToken);

        target.IsEnabled.ShouldBeFalse();
        other.IsEnabled.ShouldBe(true);
    }

    [Test]
    public async Task ShouldReturnFailedResult_WhenServerDoesNotExist()
    {
        // Arrange
        await SetupDatabase(91202, config => config.PlexServerCount = 0);

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SetServerEnabledRequest { PlexServerId = 9999, IsEnabled = true }
        );

        // Assert
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBeFalse();
    }

    [Test]
    public async Task ShouldKeepOwnedOverride_WhenTogglingIsEnabled()
    {
        // Arrange
        await SetupDatabase(91205, config => config.PlexServerCount = 1);
        var db = IDbContext;
        var server = await db.PlexServers.IgnoreIsEnabledFilter().FirstAsync(CancellationToken);

        await db
            .PlexServers.IgnoreIsEnabledFilter()
            .Where(x => x.Id == server.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.OwnedOverride, true), CancellationToken);
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SetServerEnabledRequest { PlexServerId = server.Id, IsEnabled = false }
        );

        // Assert
        endpointResult.ShouldNotBeNull();
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBe(true);
        var updated = await db
            .PlexServers.IgnoreIsEnabledFilter()
            .FirstAsync(x => x.Id == server.Id, CancellationToken);
        updated.OwnedOverride.ShouldBe(true);
        updated.IsEnabled.ShouldBeFalse();
    }

    [Test]
    public async Task ShouldKeepDownloadsPausedFlag_WhenTogglingIsEnabled()
    {
        // Arrange
        await SetupDatabase(91206, config => config.PlexServerCount = 1);
        var db = IDbContext;
        var server = await db.PlexServers.IgnoreIsEnabledFilter().FirstAsync(CancellationToken);

        await db
            .PlexServers.IgnoreIsEnabledFilter()
            .Where(x => x.Id == server.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsDownloadsPausedByUser, true), CancellationToken);
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SetServerEnabledRequest { PlexServerId = server.Id, IsEnabled = false }
        );

        // Assert
        endpointResult.ShouldNotBeNull();
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBe(true);
        var updated = await db
            .PlexServers.IgnoreIsEnabledFilter()
            .FirstAsync(x => x.Id == server.Id, CancellationToken);
        updated.IsDownloadsPausedByUser.ShouldBe(true);
    }

    [Test]
    public async Task ShouldBeIdempotent_WhenEnablingAlreadyEnabledServer()
    {
        // Arrange
        await SetupDatabase(91207, config => config.PlexServerCount = 1);
        var db = IDbContext;
        var server = await db.PlexServers.IgnoreIsEnabledFilter().FirstAsync(CancellationToken);
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SetServerEnabledRequest { PlexServerId = server.Id, IsEnabled = true }
        );

        // Assert
        endpointResult.ShouldNotBeNull();
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBe(true);
        var updated = await db
            .PlexServers.IgnoreIsEnabledFilter()
            .FirstAsync(x => x.Id == server.Id, CancellationToken);
        updated.IsEnabled.ShouldBe(true);
    }

    [Test]
    public async Task ShouldBeIdempotent_WhenDisablingAlreadyDisabledServer()
    {
        // Arrange
        await SetupDatabase(91208, config => config.PlexServerCount = 1);
        var db = IDbContext;
        var server = await db.PlexServers.IgnoreIsEnabledFilter().FirstAsync(CancellationToken);
        await db
            .PlexServers.IgnoreIsEnabledFilter()
            .Where(x => x.Id == server.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, false), CancellationToken);
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SetServerEnabledRequest { PlexServerId = server.Id, IsEnabled = false }
        );

        // Assert
        endpointResult.ShouldNotBeNull();
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBe(true);
        var updated = await db
            .PlexServers.IgnoreIsEnabledFilter()
            .FirstAsync(x => x.Id == server.Id, CancellationToken);
        updated.IsEnabled.ShouldBeFalse();
    }

    [Test]
    public async Task ShouldToggleEnabledStateTwice_WhenTwoRequestsAreSent()
    {
        // Arrange
        await SetupDatabase(91209, config => config.PlexServerCount = 1);
        var db = IDbContext;
        var server = await db.PlexServers.IgnoreIsEnabledFilter().FirstAsync(CancellationToken);
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Exactly(2));

        // Act
        await TestEndpointHandleAsync(new SetServerEnabledRequest { PlexServerId = server.Id, IsEnabled = false });
        await TestEndpointHandleAsync(new SetServerEnabledRequest { PlexServerId = server.Id, IsEnabled = true });

        // Assert
        var updated = await db
            .PlexServers.IgnoreIsEnabledFilter()
            .FirstAsync(x => x.Id == server.Id, CancellationToken);
        updated.IsEnabled.ShouldBe(true);
    }

    [Test]
    public async Task ShouldReturnSuccessResponse_WhenDisablingTargetServer()
    {
        // Arrange
        await SetupDatabase(91210, config => config.PlexServerCount = 1);
        var db = IDbContext;
        var server = await db.PlexServers.IgnoreIsEnabledFilter().FirstAsync(CancellationToken);
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SetServerEnabledRequest { PlexServerId = server.Id, IsEnabled = false }
        );

        // Assert
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBe(true);
    }

    [Test]
    public async Task ShouldInvalidateServerLibraries_WhenTogglingIsEnabled()
    {
        // Arrange
        await SetupDatabase(
            91211,
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
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SetServerEnabledRequest { PlexServerId = server.Id, IsEnabled = false }
        );

        // Assert
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBe(true);
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(It.IsAny<QueueMediaOverviewRebuildCommand>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Test]
    public void ShouldRejectInvalidPlexServerId_WhenValidatingRequest()
    {
        // Arrange
        var validator = new SetServerEnabledRequestValidator();

        // Act
        var result = validator.Validate(new SetServerEnabledRequest { PlexServerId = 0, IsEnabled = true });

        // Assert
        result.IsValid.ShouldBeFalse();
    }
}
